using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordMusicBot
{
    class VoiceManager
    {
        object joinLock;
        bool joined = false;

        IAudioClient audioClient = null;
        IVoiceChannel currentChannel = null;

        Task voiceTask = Task.CompletedTask;
        System.Threading.CancellationTokenSource voiceCTS = null;

        private async Task JoinChannelAsync(SocketSlashCommand command, IVoiceChannel channel = null) {
            /*lock (joinLock) {
                joined = true;
            }*/
            // Get the audio channel
            channel = channel ?? (command.User as IGuildUser)?.VoiceChannel;
            if (channel == null) {
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Info, "JoinChannel", "User must be in a voice channel, or a voice channel must be passed as an argument."));
                await command.RespondAsync("User must be in a voice channel, or a voice channel must be passed as an argument.", ephemeral: true);
            }
            currentChannel = channel;

            audioClient = await channel.ConnectAsync();
        }

        private Process CreateFileStream(string path) {
            return Process.Start(new ProcessStartInfo {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }

        private async Task sendFromFileAsync(string path) {
            if (audioClient != null) {
                await audioClient.SetSpeakingAsync(true);

                try {
                    using (var ffmpeg = CreateFileStream(path))
                    using (var output = ffmpeg.StandardOutput.BaseStream)
                    using (var discord = audioClient.CreatePCMStream(AudioApplication.Mixed)) {
                        try { 
                            await output.CopyToAsync(discord); 
                        } finally { 
                            await discord.FlushAsync();
                            ffmpeg.Close();
                        }
                    }
                } catch (Exception e) {
                    await LoggingService.LogAsync(new LogMessage(LogSeverity.Error, "SendAsync", e.Message));
                    throw;
                }
                
                await audioClient.SetSpeakingAsync(false);
            } else {
                throw new Exception("AudioClient is null");
            }
        }

        public async Task SendFormFileAsync(string path, SocketSlashCommand command) {
            try {
                await CheckAudioClientAsync(command);
            } catch (Exception e) {
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Error, "SetupVoice", e.Message));
                return;
            }
            if (voiceTask.IsCompleted) {
                voiceTask = sendFromFileAsync(path);
            } else {
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Info, "SendAsync", "Previous stream was incomplete"));
            }
        }

        private async Task sendFromStreamAsync(System.Threading.CancellationToken cancellation) {
            if (audioClient != null) {
                await audioClient.SetSpeakingAsync(true);

                try {
                    MMDevice device = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)[Program.AudioDeviceId];
                    using (var capture = new WasapiLoopbackCapture(device))
                    using (var output = new System.IO.MemoryStream())
                    using (var discord = audioClient.CreatePCMStream(AudioApplication.Mixed)) {
                        await LoggingService.LogAsync(new LogMessage(LogSeverity.Info, "Stream", device.FriendlyName));
                        capture.WaveFormat = new WaveFormat(48000, 16, 2);
                        capture.ShareMode = AudioClientShareMode.Shared;

                        Task t = Task.CompletedTask;
                        capture.DataAvailable += async (s, a) => {
                            await output.WriteAsync(a.Buffer, 0, a.BytesRecorded);
                            output.Seek(-a.BytesRecorded, System.IO.SeekOrigin.Current);
                            try { 
                                await output.CopyToAsync(discord);
                            } finally {
                                t = discord.FlushAsync();
                                output.SetLength(0);
                                await t;
                            }
                        };

                        capture.RecordingStopped += (s, a) => {
                            //capture.Dispose();
                        };

                        capture.StartRecording();
                        while (capture.CaptureState != CaptureState.Stopped) {
                            await Task.Delay(500);
                            if (cancellation.IsCancellationRequested) {
                                capture.StopRecording();
                            }
                        }
                    }
                } catch (Exception e) {
                    await LoggingService.LogAsync(new LogMessage(LogSeverity.Error, "SendAsync", e.Message));
                    throw;
                }

                await audioClient.SetSpeakingAsync(false);
            } else {
                throw new Exception("AudioClient is null");
            }
        }

        public async Task<bool> SendFromStreamAsync(SocketSlashCommand command) {
            try {
                await CheckAudioClientAsync(command);
            } catch (Exception e) {
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Error, "SetupVoice", e.Message));
                return false;
            }
            if (voiceTask.IsCompleted) {
                voiceCTS = new System.Threading.CancellationTokenSource();
                voiceTask = sendFromStreamAsync(voiceCTS.Token);
                return true;
            } else {
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Warning, "SendAsync", "Previous stream was incomplete. Terminating"));
                return false;
            }
        }

        public async Task<bool> StopStreamAsync() {
            if (voiceCTS != null) {
                voiceCTS.Cancel();
                voiceCTS.Dispose();
                voiceCTS = null;
                return true;
            } else {
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Info, "StopStream", "Stream was not running"));
                return false;
            }
        }

        /// <summary>
        /// Lists all active audio devices on a system
        /// </summary>
        /// <returns>
        /// List of audio devices
        /// </returns>
        public async Task<string[]> GetAudioDevicesAsync() {
            List<Task<string>> tasks = new List<Task<string>>();

            var enumerator = new MMDeviceEnumerator();
            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)) {
                if (wasapi != null) {
                    tasks.Add(Task.Run(() => {
                        try {
                            return $"{wasapi.DataFlow} | {wasapi.FriendlyName} | {wasapi.DeviceFriendlyName} | {wasapi.State}";
                        } catch (Exception) {
                            throw;
                        }
                    }));
                }
            }

            return await Task.WhenAll(tasks);
        }

        public async Task SetupVoiceAsync(SocketSlashCommand command) {
            try {
                // Parafrased from documentation: This shouldn't be awaited as it will block the thread
                // There seems to be no need for moving in to a different thread though
                _ = JoinChannelAsync(command);
            } catch (Exception e) {
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Error, "SetupVoice", e.Message));
            }
        }

        public async Task DisconnectAsync() {
            if (currentChannel != null) {
                await currentChannel.DisconnectAsync();
                currentChannel = null;
                audioClient.Dispose();
                audioClient = null;
                /*lock (joinLock) {
                    joined = false;
                }*/
            }
        }

        /// <summary>
        /// Suspends execution until audio client is active or 10 seconds have passed.
        /// </summary>
        public Task CheckAudioClientAsync(SocketSlashCommand command) {
            var msg = new LogMessage(LogSeverity.Info, "CheckAudioClient", "audioClient is null");
            Task t = Task.Run(async () => {
                /*bool joined;
                lock (joinLock) {
                    joined = this.joined;
                }
                if (!joined) {
                    await SetupVoiceAsync(command);
                }*/
                int tries = 0;
                while (audioClient == null) {
                    await LoggingService.LogAsync(msg);
                    await Task.Delay(1000);

                    if ((tries += 1) > 10)
                        throw new Exception("Timed out waiting for audio client");
                }
                await LoggingService.LogAsync(new LogMessage(LogSeverity.Info, "CheckAudioClient", "audioClient is alive"));
            });
            return t;
        }
    }
}
