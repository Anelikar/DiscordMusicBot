
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordMusicBot
{
	public class LoggingService
	{
		public static void SetupLogging(DiscordSocketClient client, CommandService command) {
			client.Log += LogAsync;
			command.Log += LogAsync;
		}

		public static Task LogAsync(LogMessage message) {
			if (message.Exception is CommandException cmdException) {
				Console.WriteLine($"[Command/{message.Severity}] {cmdException.Command.Aliases.First()}"
					+ $" failed to execute in {cmdException.Context.Channel}.");
				Console.WriteLine(cmdException);
			} else
				Console.WriteLine($"[General/{message.Severity}] {message}");

			return Task.CompletedTask;
		}
	}
}
