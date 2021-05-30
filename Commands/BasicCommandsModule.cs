using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace ControlCopy.Commands
{
  public class BasicCommandsModule : BaseCommandModule
  {
    [Command( "connections" )]
    [Description( "Simple command to see how many servers the bot is on" )]
    public async Task Connections( CommandContext ctx )
    {
      await ctx.TriggerTypingAsync();

      var connections = ctx.Client.Guilds;
      await ctx.RespondAsync( $"Running on {connections} servers" );
    }

    [Command( "up" )]
    [Description( "Simple command to test if the bot is running" )]
    public async Task Alive( CommandContext ctx )
    {
      await ctx.TriggerTypingAsync();
      await ctx.RespondAsync( "Ready to process requests" );
    }

    [Command( "rmcat" )]
    [Description( "Command to delete category + content" )]
    public async Task RemoveCategory( CommandContext ctx, ulong categoryId )
    {
      if ( !ctx.Guild.GetChannel( categoryId ).IsCategory ) return;
      foreach( var c in ctx.Guild.GetChannel( categoryId ).Children )
      {
        await c.DeleteAsync();
      }
      await ctx.Guild.GetChannel( categoryId ).DeleteAsync();
      await ctx.RespondAsync( "Category deleted" );
    }
  }
}