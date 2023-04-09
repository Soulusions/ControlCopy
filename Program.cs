using System;
using System.Threading.Tasks;

using ControlCopy.Commands;

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity.Extensions;

using Microsoft.Extensions.Logging;

namespace ControlCopy
{
  public class Program
  {
    public readonly EventId BotEventId = new EventId(42, "ControlCopy");

    public DiscordClient Client { get; set; }

    public CommandsNextExtension Commands { get; set; }

    public static void Main( string[] args )
    {
      var prog = new Program();
      prog.RunBotAsync().GetAwaiter().GetResult();
    }

    public async Task RunBotAsync()
    {
      var cfg = new DiscordConfiguration
      {
        //Token = Environment.GetEnvironmentVariable("TOKEN"),
        Token = "INSERT_TOKEN_HERE",
        TokenType = TokenType.Bot,

        AutoReconnect = true,
        MinimumLogLevel = LogLevel.Debug
      };

      this.Client = new DiscordClient( cfg );

      this.Client.Ready += this.Client_Ready;
      this.Client.GuildAvailable += this.Client_GuildAvailable;
      this.Client.ClientErrored += this.Client_ClientError;

      this.Client.UseInteractivity();

      var ccfg = new CommandsNextConfiguration
      {
        StringPrefixes = new[] { "cc." },

        EnableDms = false,

        EnableMentionPrefix = true
      };

      this.Commands = this.Client.UseCommandsNext( ccfg );

      this.Commands.CommandExecuted += this.Commands_CommandExecuted;
      this.Commands.CommandErrored += this.Commands_CommandErrored;

      // let's add a converter for a custom type and a name
      // var mathopcvt = new MathOperationConverter();
      // Commands.RegisterConverter(mathopcvt);
      // Commands.RegisterUserFriendlyTypeName<MathOperation>("operation");

      this.Commands.RegisterCommands<BasicCommandsModule>();
      this.Commands.RegisterCommands<CopyChannelCommand>();

      // set up our custom help formatter
      // this.Commands.SetHelpFormatter<SimpleHelpFormatter>();

      await this.Client.ConnectAsync();

      await Task.Delay( -1 );
    }

    private Task Client_Ready( DiscordClient sender, ReadyEventArgs e )
    {
      sender.Logger.LogInformation( BotEventId, "Client is ready to process events." );

      return Task.CompletedTask;
    }

    private Task Client_GuildAvailable( DiscordClient sender, GuildCreateEventArgs e )
    {
      sender.Logger.LogInformation( BotEventId, $"Guild available: {e.Guild.Name}" );

      return Task.CompletedTask;
    }

    private Task Client_ClientError( DiscordClient sender, ClientErrorEventArgs e )
    {
      sender.Logger.LogError( BotEventId, e.Exception, "Exception occured" );

      return Task.CompletedTask;
    }

    private Task Commands_CommandExecuted( CommandsNextExtension sender, CommandExecutionEventArgs e )
    {
      e.Context.Client.Logger.LogInformation( BotEventId, $"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}'" );

      return Task.CompletedTask;
    }

    private async Task Commands_CommandErrored( CommandsNextExtension sender, CommandErrorEventArgs e )
    {
      e.Context.Client.Logger.LogError( BotEventId, $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}", DateTime.Now );

      if ( e.Exception is ChecksFailedException ex )
      {
        var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");

        var embed = new DiscordEmbedBuilder
        {
          Title = "Access denied",
          Description = $"{emoji} You do not have the permissions required to execute this command.",
          Color = new DiscordColor(0xFF0000) // red
        };
        await e.Context.RespondAsync( embed );
      }
    }
  }
}