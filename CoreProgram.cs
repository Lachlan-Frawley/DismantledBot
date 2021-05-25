using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace DismantledBot
{
    public class CoreProgram
    {        
        public static void Main(string[] args)
            => new CoreProgram().MainAsync().GetAwaiter().GetResult();

        private DiscordSocketClient client;
        public static BotControl settings = BotControl.Load("config.json");

        public async Task MainAsync()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                AlwaysDownloadUsers = true,
                LargeThreshold = 250               
            });
            client.Log += Log;

            await client.LoginAsync(TokenType.Bot, settings.Token);
            await client.StartAsync();

            CommandHandler handler = new CommandHandler(client, new CommandService());
            await handler.InstallCommandsAsync();

            client.Ready += () =>
            {
                Console.WriteLine("Bot running....");
                return Task.CompletedTask;
            };

            await Task.Delay(-1);
        }

        private Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }
    }

    public class CommandHandler
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;

        public CommandHandler(DiscordSocketClient client, CommandService service)
        {
            this.client = client;
            commands = service;
        }

        public async Task InstallCommandsAsync()
        {
            client.MessageReceived += Client_MessageReceived;
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            if (message == null)
                return;

            int argPos = 0;

            if (!(message.HasCharPrefix(CoreProgram.settings.Prefix, ref argPos)) || message.HasMentionPrefix(client.CurrentUser, ref argPos) || message.Author.IsBot)
                return;

            var context = new SocketCommandContext(client, message);            

            await commands.ExecuteAsync(context, argPos, null);
        }
    }
}
