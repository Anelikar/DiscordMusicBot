using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordMusicBot
{
    class Program {
        // This can be set to your testing guild id if you are using GuildCommands directive
        public const ulong TestGuildId = 123456789123456789;
        // This has to be set to the path to you token file if you are not using "-t <token>" argument
        // token file must contain your secret bot token
        private const string tokenPath = "token.txt";
        // This should be set to the id of the audio device you are using to stream sound.
        // You can get this id by defining GetDevices directive in CommandsManager.cs, 
        // reinitializing commands with "-r" and using "get-devices" command in discord.
        // (or just type get-devices in console)
        public const int AudioDeviceId = 7;
        // This is required for "now-playing" functionality.
        // partialTiltle        is a partial title of you audio program that displays the name of the current track in it's title
        // truncation           is a direction of title truncation
        // symbolsToTruncate    is an amoint of symbols to truncate. Easiest way to find this number is to count. Carefully.
        //
        // Common examples of usage would be:
        // "Google Chrome", WindowManager.Window.TruncationDirection.end, 16
        // "foobar2000", WindowManager.Window.TruncationDirection.end, 12
        // "Mozilla Firefox", WindowManager.Window.TruncationDirection.end, 18
        //
        // If you don't care about truncating title, you can use WindowManager.Window.TruncationDirection.none
        public static readonly WindowManager.Window PlayerWindow = new WindowManager.Window(
            "foobar2000", 
            WindowManager.Window.TruncationDirection.end, 
            //12 // For normal
            20 // For portable
            );

        public static DiscordSocketClient Client { get; private set; }
        CommandsManager commandsManager;
        string token = null;

        public static Task Main(string[] args) => new Program().MainAsync(args);
        public async Task MainAsync(string[] args) {
            Client = new DiscordSocketClient();
            commandsManager = new CommandsManager();
            LoggingService.SetupLogging(Client, new Discord.Commands.CommandService());

            processArgs(args);

            if (token == null) {
                token = System.IO.File.ReadAllText(tokenPath);
            }
            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

            Client.SlashCommandExecuted += commandsManager.SlashCommandHandlerAsync;


            await ReadConsoleLoopAsync();
        }

        /* Arguments:
         * -c           Forces discord to reinitialize bot commands. Should be set on first launch, 
         *              when commands are added or their names changed.
         * -t <token>   Can be used instead of keeping the secret token in a file.
         */
        void processArgs(string[] args) {
            if (args == null)
                return;
            if (args.Length == 0)
                return;

            for (int i = 0; i < args.Length; i++) {
                string arg = args[i];
                switch (arg) {
                    case "-c":
                        Client.Ready += CommandsManager.InitCommandsAsync;
                        break;
                    case "-t":
                        if ((i + 1) < args.Length) {
                            i++;
                            token = args[i];
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /* Commands:
         * stop         Disconnects the bot and terminates the program
         * get-devices  Lists all active audio devices on your system
         */
        private async Task ReadConsoleLoopAsync() {
            string s;
            while (true) {
                s = Console.ReadLine();
                switch (s) {
                    case "stop":
                        await Client.StopAsync();
                        await Client.LogoutAsync();
                        return;
                    case "get-devices":
                        string[] devices = await new VoiceManager().GetAudioDevicesAsync();
                        string message = "";
                        for (int i = 0; i < devices.Length; i++) {
                            message += $"{i}: {devices[i]}; \n";
                        }
                        await LoggingService.LogAsync(new LogMessage(LogSeverity.Info, "Console", message));
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
