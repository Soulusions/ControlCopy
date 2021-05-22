using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using DSharpPlus.Interactivity.Extensions;
using System.Threading.Tasks;

namespace ControlCopy {
		public static class Utilities {
				public delegate IEnumerable<DiscordChannel> Filter(CommandContext ctx);
				public delegate IEnumerable<DiscordChannel> FilterChannel(DiscordChannel cat);
				public delegate bool Binder();
				public delegate Task<bool> AsyncBinder();
				// public delegate void Printer(CommandContext ctx, string s);

		public static void executeFor(Func<bool> func, TimeSpan timeSpan) {
			Stopwatch s = Stopwatch.StartNew();
			while (s.ElapsedMilliseconds < timeSpan.TotalMilliseconds && !func());
				// func();
		}
		public static async Task asyncExecuteFor(Func<Task<bool>> func, TimeSpan timeSpan) {
			Stopwatch s = Stopwatch.StartNew();
			while (s.ElapsedMilliseconds < timeSpan.TotalMilliseconds && !(await func()));
		}

		// public static Printer send = (ctx, s) => {
		// 	ctx.Channel.SendMessageAsync(s);
		// };

		public enum PolarReactionState {Yes, No, TimedOut}
		public static async Task<PolarReactionState> polarReaction(CommandContext ctx, DiscordMessage mes) {
			await mes.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
			await mes.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":x:"));
			// var collectedReactions = await mes.CollectReactionsAsync(TimeSpan.FromSeconds(60));
			// System.Collections.ObjectModel.ReadOnlyCollection<DSharpPlus.Interactivity.EventHandling.Reaction> collectedReactions;
			System.Collections.ObjectModel.ReadOnlyCollection<DSharpPlus.Interactivity.EventHandling.Reaction> collectedReactions;
			IEnumerable<DSharpPlus.Interactivity.EventHandling.Reaction> result = null;
			AsyncBinder checkReaction = async () => {
				collectedReactions = await mes.CollectReactionsAsync(TimeSpan.FromMilliseconds(500));
				result = collectedReactions.Where(r => new string[]{":white_check_mark:", ":x:"}.Contains(r.Emoji.GetDiscordName()) && r.Users.Contains(ctx.Message.Author));
				return result.Count() > 0;
			};
			await asyncExecuteFor(() => checkReaction(), TimeSpan.FromSeconds(60));
			if (result != null)
			{
				if (result.FirstOrDefault(r => r.Emoji.GetDiscordName() == ":white_check_mark:") != null)
					return PolarReactionState.Yes;
				else
					return PolarReactionState.No;
			}
			else
				return PolarReactionState.TimedOut;
		}

				public static Filter allSupportedChannels = (ctx) => {
			var firstFiltering = ctx.Guild.Channels.Select(e => e.Value).Where(c => !(new ChannelType[]{ChannelType.Voice, ChannelType.Unknown}.Contains(c.Type)));
			var secondFiltering = firstFiltering.Except(
				firstFiltering.Where(c => c.Type == ChannelType.Category)
				.Where(c => c.Children.All(s => !firstFiltering.Contains(s)))
			);
			return secondFiltering;
		};

		public static FilterChannel onlySupported = (cat) => {
			return cat.Children.Where(c => !(new ChannelType[]{ChannelType.Voice, ChannelType.Unknown}.Contains(c.Type)));
		};

		public static Predicate<DiscordMember> isMemberDisallowed = (m) => m.Roles.Where(r => r.Permissions.HasPermission(Permissions.Administrator) || r.Name == "Archiviste").Count() == 0 && !m.IsOwner;
	}
}
