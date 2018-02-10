using System;

namespace WTG.BulkAnalysis.Core
{
	public interface ILog
	{
		void WriteFormatted(FormattableString message, LogLevel level = LogLevel.Normal);
		void WriteLine(string message, LogLevel level = LogLevel.Normal);
		void WriteLine();
	}
}
