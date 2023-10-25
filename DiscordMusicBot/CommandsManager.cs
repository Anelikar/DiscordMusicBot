#undef GuildCommands

#undef TestCommand
#define Join
#define Disconnect
#undef Say
#undef GetDevices
#define Stream
#define Stop
#define NowPlaying

using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordMusicBot
{
    class CommandsManager
    {
        /// <summary>
        /// This is called automatically at launch with the argument "-c". There shouldn't be any reason to call it in any other situation.
        /// </summary>
        public static async Task InitCommandsAsync() {
            var testGuild = Program.Client.GetGuild(Program.TestGuildId);

            try {
                await testGuild.DeleteApplicationCommandsAsync();
                SlashCommandBuilder command = new SlashCommandBuilder();
                // Names have to be all lowercase and match the regular expression ^[\w-]{3,32}$
                // Descriptions can have a max length of 100.
                // Be careful with global commands as they are not easily removed.
#if TestCommand
                command.WithName("test-command");
                command.WithDescription("This is a test command");
#if GuildCommands
                await testGuild.CreateApplicationCommandAsync(command.Build());
#else
                await Program.Client.CreateGlobalApplicationCommandAsync(command.Build());
#endif
#endif
#if Join
                command.WithName("join");
                command.WithDescription("Joins the bot to the channel you are currently in");
#if GuildCommands
                await testGuild.CreateApplicationCommandAsync(command.Build());
#else
                await Program.Client.CreateGlobalApplicationCommandAsync(command.Build());
#endif
#endif
#if Disconnect
                command.WithName("disconnect");
                command.WithDescription("Disconnects the bot from the voice channel");
#if GuildCommands
                await testGuild.CreateApplicationCommandAsync(command.Build());
#else
                await Program.Client.CreateGlobalApplicationCommandAsync(command.Build());
#endif
#endif
                // "say" was used for testing, correct execution is not guaranteed
#if Say
                command.WithName("say");
                command.WithDescription("Says something");
#if GuildCommands
                await testGuild.CreateApplicationCommandAsync(command.Build());
#else
                await Program.Client.CreateGlobalApplicationCommandAsync(command.Build());
#endif
#endif
#if GetDevices
                command.WithName("get-devices");
                command.WithDescription("Lists all active audio devices");
#if GuildCommands
                await testGuild.CreateApplicationCommandAsync(command.Build());
#else
                await Program.Client.CreateGlobalApplicationCommandAsync(command.Build());
#endif
#endif
#if Stream
                command.WithName("stream");
                command.WithDescription("Starts audio stream");
#if GuildCommands
                await testGuild.CreateApplicationCommandAsync(command.Build());
#else
                await Program.Client.CreateGlobalApplicationCommandAsync(command.Build());
#endif
#endif
#if Stop
                command.WithName("stop");
                command.WithDescription("Stops audio stream");
#if GuildCommands
                await testGuild.CreateApplicationCommandAsync(command.Build());
#else
                await Program.Client.CreateGlobalApplicationCommandAsync(command.Build());
#endif
#endif
#if NowPlaying
                command.WithName("now-playing");
                command.WithDescription("Prints a message that dynamically displays current track");
#if GuildCommands
                await testGuild.CreateApplicationCommandAsync(command.Build());
#else
                await Program.Client.CreateGlobalApplicationCommandAsync(command.Build());
#endif
#endif
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Info, "InitCommands", "Commands added successfully"));
            } catch (HttpException exception) {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Critical, "InitCommands", json, exception));
            }
        }

        VoiceManager voiceManager;
        TrackManager trackManager;

        public CommandsManager() {
            voiceManager = new VoiceManager();
            trackManager = new TrackManager();
        }

        public async Task SlashCommandHandlerAsync(SocketSlashCommand command) {
            switch (command.CommandName) {
#if Join
                case "join":
                    await voiceManager.SetupVoiceAsync(command);
                    await Respond(command, $"Joined {(command.User as IGuildUser)?.VoiceChannel.Name}");
                    break;
#endif
#if Disconnect
                case "disconnect":
                    await voiceManager.StopStreamAsync();
                    await trackManager.StopUpdateLoopAsync();
                    await voiceManager.DisconnectAsync();
                    await Respond(command, $"Disconnected");
                    break;
#endif
                // "say" was used for testing connection, correct execution is not guaranteed
#if Say
                case "say":
                    await voiceManager.SendFormFileAsync("12. Roar of Dominion (Rain).mp3", command);
                    await Respond(command, $"Starting voice");
                    break;
#endif
#if GetDevices
                case "get-devices":
                    string[] devices = await voiceManager.GetAudioDevicesAsync();
                    string message = "";
                    for (int i = 0; i < devices.Length; i++) {
                        message += $"{i}: {devices[i]}; \n";
                    }
                    await Respond(command, message, ephemeral: true);
                    break;
#endif
#if Stream
                case "stream":
                    if (await voiceManager.SendFromStreamAsync(command)) {
                        await Respond(command, $"Starting stream");
                    } else {
                        await Respond(command, $"Failed to start stream");
                    }
                    break;
#endif
#if Stream
                case "stop":
                    if (await voiceManager.StopStreamAsync()) {
                        await Respond(command, $"Stopping stream");
                    } else {
                        await Respond(command, $"Failed to stop stream");
                    }
                    await trackManager.StopUpdateLoopAsync();
                    break;
#endif
#if NowPlaying
                case "now-playing":
                    await trackManager.StartUpdateLoopAsync(command);
                    await LoggingService.LogAsync(new LogMessage(LogSeverity.Info, "SlashCommands", "Starting displaying track"));
                    break;
#endif
                default:
                    await Respond(command, $"Unknown command {command.Data.Name}", severity: LogSeverity.Warning, ephemeral: true);
                    break;
            }
        }

        async Task Respond(SocketSlashCommand command, string message, LogSeverity severity = LogSeverity.Info, bool ephemeral = false) {
            await LoggingService.LogAsync(new LogMessage(severity, "SlashCommands", message));
            await command.RespondAsync(message, ephemeral: ephemeral);
        }
    }
}
