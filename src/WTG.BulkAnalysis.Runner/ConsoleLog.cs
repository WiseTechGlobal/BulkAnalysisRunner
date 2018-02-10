using System;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.Runner
{
	sealed class ConsoleLog : ILog
	{
		public static ILog Instance { get; } = new ConsoleLog();

		ConsoleLog()
		{
		}

		public void WriteFormatted(FormattableString message, LogLevel level = LogLevel.Normal)
		{
			if (GetConsoleColor(level, out var newColor))
			{
				var oldColor = Console.ForegroundColor;
				Console.ForegroundColor = newColor;
				Console.Error.WriteLine(message.Format, message.GetArguments());
				Console.ForegroundColor = oldColor;
			}
			else
			{
				Console.Error.WriteLine(message.Format, message.GetArguments());
			}
		}

		public void WriteLine(string text, LogLevel level = LogLevel.Normal)
		{
			if (GetConsoleColor(level, out var newColor))
			{
				var oldColor = Console.ForegroundColor;
				Console.ForegroundColor = newColor;
				Console.Error.WriteLine(text);
				Console.ForegroundColor = oldColor;
			}
			else
			{
				Console.Error.WriteLine(text);
			}
		}

		public void WriteLine() => Console.Error.WriteLine();

		static bool GetConsoleColor(LogLevel level, out ConsoleColor color)
		{
			switch (level)
			{
				case LogLevel.Error:
					color = ConsoleColor.Red;
					break;

				case LogLevel.Warning:
					color = ConsoleColor.Yellow;
					break;

				case LogLevel.Success:
					color = ConsoleColor.Green;
					break;

				default:
					color = default;
					return false;
			}

			return true;
		}
	}
}
