using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using static ControlCopy.Utilities;
using static DSharpPlus.Entities.DiscordEmbedBuilder;

namespace ControlCopy.Commands
{
    [RequirePermissions(
      Permissions.SendMessages &
      Permissions.AccessChannels &
      Permissions.ManageChannels &
      Permissions.ReadMessageHistory &
      Permissions.EmbedLinks &
      Permissions.AttachFiles
    )]
    public class CopyChannelCommand : BaseCommandModule
    {
        private Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [Command("wizard")]
        [Description("Lists channels and categories, and formats the copy command based on user input")]
        public async Task Wizard(CommandContext ctx)
        {
            if (isMemberDisallowed(ctx.Member))
                return; // User isn't an administrator or doesn't have the "Archiver" role in any server. Aborting

            await ctx.TriggerTypingAsync();

            await ctx.RespondAsync(
              "Listing all available channels and categories from this server. Please click the :arrow_up_small: reaction under the channel or category you wish to copy");

            await ctx.TriggerTypingAsync();
            var elements = Utilities.allSupportedChannels(ctx).OrderBy(c => c.Position).ToList();

            await ctx.Channel.SendMessageAsync("**Available channels:**");
            DiscordChannel selectedElement = null;
            List<Tuple<DiscordMessage, ulong>> listedChannels = new List<Tuple<DiscordMessage, ulong>>();
            Utilities.Binder selectedElementBinder = () =>
            {
                foreach (var l in listedChannels)
                {
                    if (l.Item1.GetReactionsAsync(DiscordEmoji.FromName(ctx.Client, ":arrow_up_small:")).Result
                   .FirstOrDefault(u => u == ctx.Message.Author) != null)
                    {
                        selectedElement = ctx.Guild.GetChannel(l.Item2);
                        return true;
                    }
                }

                return false;
            };
            foreach (var e in elements)
            {
                await ctx.TriggerTypingAsync();
                listedChannels.Add(new Tuple<DiscordMessage, ulong>(
                                     await ctx.Channel.SendMessageAsync(
                                       (e.Type != ChannelType.Category ? "`┕ " : "`") + $"{e.Name}`"), e.Id));
                await listedChannels.Last().Item1.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":arrow_up_small:"));

                if (selectedElementBinder())
                    break;
            }

            await ctx.Channel.SendMessageAsync("Done listing");

            if (selectedElement == null)
            {
                Utilities.executeFor((() => selectedElementBinder()), TimeSpan.FromSeconds(60));
                if (selectedElement == null)
                {
                    await ctx.Channel.SendMessageAsync("No reaction provided. Aborting.");
                    return;
                }
            }

            bool elementIsCategory = selectedElement.Type == ChannelType.Category;
            string elementType = elementIsCategory ? "category" : "channel";

            var keepNameMessage =
              await ctx.RespondAsync(
                $"Would you like to keep original {elementType} name (so you won't be prompted to name the new one)?");

            bool keepName = true;

            var keepNameResult = await polarReaction(ctx, keepNameMessage);
            if (keepNameResult != PolarReactionState.TimedOut)
            {
                keepName = (keepNameResult == PolarReactionState.Yes);
            }
            else
                await ctx.Channel.SendMessageAsync($"No reaction given. The {elementType} name will be kept by default");

            await ctx.RespondAsync("Copy the following command:");
            await ctx.Channel.SendMessageAsync(
              $"`cc.copy {selectedElement.GuildId} {selectedElement.Id} {keepName.ToString()}`");
        }

        [Command("copy")]
        [Description(
          "Copies channel or category from given guild and channel ID. Note you may need to enable developer mode to copy the IDs directly")]
        public async Task Copy(CommandContext ctx,
                               [Description("The source guild ID. (Right click / three-dot menu of guild >> Copy ID)")]
                           ulong guildId,
                               [Description(
                             "The source element ID. (Right click / long press channel or category >> Copy ID)")]
                           ulong elementId,
                               [Description(
                             "\"True\" if user wishes to keep original channel or category name, \"False\" otherwise")]
                           bool keepName
        )
        {
            DiscordGuild guild;
            try
            {
                guild = await ctx.Client.GetGuildAsync(guildId);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                await ctx.RespondAsync("Guild not found.");
                return;
            }

            if (isMemberDisallowed(ctx.Member) || isMemberDisallowed(await guild.GetMemberAsync(ctx.Member.Id)))
                return; // User isn't an administrator or doesn't have the "Archivist" role in any server. Aborting

            DiscordChannel selectedElement;
            try
            {
                selectedElement = guild.GetChannel(elementId);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                await ctx.RespondAsync("Channel not found.");
                return;
            }

            string elementName;
            bool elementIsCategory = selectedElement.Type == ChannelType.Category;
            string elementType = elementIsCategory ? "category" : "channel";

            if (keepName) // If user wishes to keep original element name
                elementName = selectedElement.Name;
            else
            {
                await ctx.RespondAsync($"What do you want the new {elementType} to be named?");

                var response = await ctx.Message.GetNextMessageAsync(
                  c => c.Author.Id == ctx.Message.Author.Id,
                  TimeSpan.FromSeconds(60)
                );

                if (response.TimedOut)
                {
                    await ctx.Channel.SendMessageAsync("No input given. Aborting.");
                    return;
                }

                if (string.IsNullOrEmpty(response.Result.Content))
                {
                    await ctx.Channel.SendMessageAsync("Name cannot be empty.");
                    return;
                }

                elementName = response.Result.Content;
            }

            await ctx.Channel.SendMessageAsync("**Starting copy**");

            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);

            DiscordChannel newCategory = null;
            if (elementIsCategory)
                newCategory = await ctx.Guild.CreateChannelAsync(elementName, ChannelType.Category);

            await ctx.Channel.SendMessageAsync("Collecting messages...");

            foreach (var channel in (elementIsCategory
              ? onlySupported(selectedElement).OrderBy(c => c.Position).ToArray()
              : new DiscordChannel[] { selectedElement }))
            {
                var newChannel =
                  await ctx.Guild.CreateChannelAsync((elementIsCategory ? channel.Name : elementName), channel.Type);
                await ctx.Channel.SendMessageAsync($"Creating channel <#{newChannel.Id}>...");
                await newChannel.ModifyAsync(c => c.Topic = channel.Topic);
                if (channel.IsNSFW)
                    await newChannel.ModifyAsync(c => c.Nsfw = true);
                foreach (DiscordOverwrite overwrite in channel.PermissionOverwrites)
                {
                    try
                    {
                        if (overwrite.Type == OverwriteType.Member)
                        {
                            DiscordMember overwriteMember = await overwrite.GetMemberAsync();
                            if (ctx.Guild.Members.Where(r => r.Value.Id == overwriteMember.Id).Count() > 0)
                                await newChannel.AddOverwriteAsync((await ctx.Guild.GetMemberAsync(overwriteMember.Id)),
                                                                   overwrite.Allowed, overwrite.Denied);
                        }
                        else
                        {
                            DiscordRole overwriteRole = await overwrite.GetRoleAsync();
                            var matchingRole = ctx.Guild.Roles.Where(r => r.Value.Name == overwriteRole.Name);
                            if (matchingRole.Count() > 0)
                                await newChannel.AddOverwriteAsync(ctx.Guild.GetRole(matchingRole.First().Value.Id), overwrite.Allowed,
                                                                   overwrite.Denied);
                        }
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                    }
                }

                var firstFetchedMessages = await channel.GetMessagesAsync();

                if (firstFetchedMessages.Count == 0)
                {
                    if (elementIsCategory)
                        await newChannel.ModifyAsync(c => c.Parent = newCategory);
                    continue; // Empty channel - skip
                }

                List<DiscordMessage> bufferedMessagesList = firstFetchedMessages.ToList();

                var more = await channel.GetMessagesBeforeAsync(firstFetchedMessages.Last().Id, 100);
                int messageCount = firstFetchedMessages.Count();

                while (more.Count > 0)
                {
                    messageCount += more.Count();
                    bufferedMessagesList = more.ToList();
                    more = await channel.GetMessagesBeforeAsync(more.First().Id, 100); // "Scrolling" to the first message
                }

                await ctx.Channel.SendMessageAsync(
                  $"Posting {messageCount} messages in <#{newChannel.Id}>... (this might take a while)");

                do
                {
                    bufferedMessagesList.AddRange(more);
                    bufferedMessagesList.Reverse();
                    foreach (var mes in bufferedMessagesList)
                    {
                        if (mes.MessageType != MessageType.Default)
                        {
                            switch (mes.MessageType)
                            {
                                case MessageType.ChannelPinnedMessage:
                                    var time = new DiscordEmbedBuilder
                                    {
                                        Description = $":pushpin: {mes.Author.Username} has pinned a message in this channel",
                                        Timestamp = mes.Timestamp
                                    };
                                    await newChannel.SendMessageAsync(null, time);
                                    break;
                            }

                            continue;
                        }

                        EmbedAuthor embedAuthor = null;
                        DiscordEmbedBuilder embed = null;
                        DiscordMessage newMessage = null;
                        if (mes.Embeds.Count > 0)
                        {
                            if (mes.Author == ctx.Client.CurrentUser && mes.Embeds.First().Type == "rich")
                            {
                                DiscordEmbed CCEmbed = mes.Embeds.First();
                                embedAuthor = new EmbedAuthor { Name = CCEmbed.Author.Name, IconUrl = CCEmbed.Author.IconUrl.ToString() };
                                embed = new DiscordEmbedBuilder
                                { Description = CCEmbed.Description, Author = embedAuthor, Timestamp = CCEmbed.Timestamp };
                                newMessage = await newChannel.SendMessageAsync(null, embed);
                            }
                            else
                            {
                                newMessage = await newChannel.SendMessageAsync($"{mes.Author.Username}'s attached embed(s):");
                                foreach (DiscordEmbed userEmbed in mes.Embeds)
                                {
                                    if (new String[] { "image", "gifv" }.Contains(userEmbed.Type))
                                        await newChannel.SendMessageAsync(userEmbed.Url.ToString());
                                    else
                                        await newChannel.SendMessageAsync(null, userEmbed);
                                }
                            }
                        }
                        else
                        {
                            if (mes.Content.Length < 1999)
                            {
                                embedAuthor = new EmbedAuthor { Name = mes.Author.Username, IconUrl = mes.Author.AvatarUrl };
                                embed = new DiscordEmbedBuilder
                                { Description = mes.Content, Author = embedAuthor, Timestamp = mes.Timestamp };
                                newMessage = await newChannel.SendMessageAsync(null, embed);
                            }
                            else
                            {
                                await using var fs = GenerateStreamFromString(mes.Content);
                                var msg = await new DiscordMessageBuilder()
                                                .WithContent($"From: {mes.Author.Username}")
                                                .AddFiles(new Dictionary<string, Stream>() { { "message.txt", fs } })
                                                .SendAsync(newChannel);
                            }
                        }

                        if (mes.Pinned)
                        {
                            await newMessage.PinAsync();
                            var lastMessage = (await newChannel.GetMessagesAsync(1)).First();
                            if (lastMessage.MessageType == MessageType.ChannelPinnedMessage)
                                await newChannel.DeleteMessageAsync(lastMessage);
                        }

                        if (mes.Attachments.Count > 0)
                        {
                            foreach (var att in mes.Attachments)
                            {
                                {
                                    using (HttpClient client = new HttpClient())
                                    {
                                        var response = await client.GetAsync(att.Url);
                                        var content = await response.Content.ReadAsByteArrayAsync();
                                        using (var stream = new MemoryStream(content))
                                        {
                                            await newChannel.SendMessageAsync((new DiscordMessageBuilder()).AddFile(att.FileName, stream));
                                        }
                                    }

                                    using (FileStream fs = File.OpenRead($"{tempDirectory}/{att.FileName}"))
                                    {
                                        await newChannel.SendMessageAsync((new DiscordMessageBuilder()).AddFile(att.FileName, fs));
                                    }

                                    File.Delete($"{tempDirectory}/{att.FileName}");
                                }
                            }
                        }
                    }


                    more = await channel.GetMessagesAfterAsync(bufferedMessagesList.Last().Id);
                    bufferedMessagesList.Clear();
                } while (more.Count > 0);

                if (elementIsCategory)
                    await newChannel.ModifyAsync(c => c.Parent = newCategory);
            }


          await ctx.Channel.SendMessageAsync($"Copy of {elementType} {elementName} complete!") ;
        }
    }
}