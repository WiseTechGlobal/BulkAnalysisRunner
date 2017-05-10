using System;

namespace WTG.BulkAnalysis.Core
{
	public interface ILog
	{
		void WriteFormatted(FormattableString message);
		void WriteLine(string message);
		void WriteLine();
	}
}
