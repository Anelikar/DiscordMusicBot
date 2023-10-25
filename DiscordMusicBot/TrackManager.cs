using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordMusicBot
{
    class TrackManager
    {
        SocketSlashCommand originalCommand;
        CancellationTokenSource trackCTS = null;

        async Task<bool> printTrackAsync(SocketSlashCommand command) {
            originalCommand = command;
            await Program.PlayerWindow.UpdateTitleAsync();
            if (Program.PlayerWindow.Title != null) {
                await originalCommand.RespondAsync($"Now playing:\n{await Program.PlayerWindow.GetTruncatedTitleAsync()}");
                return true;
            }
            return false;
        }

        async Task updateTrackAsync() {
            if (await Program.PlayerWindow.UpdateTitleAsync()) {
                var message = await originalCommand.GetOriginalResponseAsync();
                await message.ModifyAsync(async (x) => { x.Content = $"Now playing:\n{await Program.PlayerWindow.GetTruncatedTitleAsync()}"; });
            }
        }

        async Task updateLoopAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                await updateTrackAsync();
                await Task.Delay(5000);
            }
        }

        public async Task<bool> StartUpdateLoopAsync(SocketSlashCommand command) {
            if (await printTrackAsync(command)) {
                trackCTS = new CancellationTokenSource();
                _ = Task.Run(async () => await updateLoopAsync(trackCTS.Token));
                return true;
            }
            return false;
        }

        public Task StopUpdateLoopAsync() {
            if (trackCTS != null) {
                trackCTS.Cancel();
                trackCTS.Dispose();
                trackCTS = null;
            }
            return Task.CompletedTask;
        }
    }
}
