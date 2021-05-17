using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Threading.Tasks;

namespace ControlCopy.Commands
{
    public class BasicCommandsModule : BaseCommandModule
    {
        [Command("connections")]
        [Description("Simple command to see how many servers the bot is on")]
        public async Task Connections(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            var connections = ctx.Client.Guilds;
            await ctx.RespondAsync($"Running on {connections} servers");
        }

        [Command("up")]
        [Description("Simple command to test if the bot is running")]
        public async Task Alive(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();
            await ctx.RespondAsync("Ready to process requests");
        }
    }
}