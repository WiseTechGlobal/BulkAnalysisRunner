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

		public void WriteFormatted(FormattableString message) => Console.Error.WriteLine(message.Format, message.GetArguments());
		public void WriteLine(string text) => Console.Error.WriteLine(text);
		public void WriteLine() => Console.Error.WriteLine();
	}
}
