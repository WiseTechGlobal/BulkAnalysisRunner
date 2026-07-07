using System;

namespace WTG.BulkAnalysis.Core
{
	public class CodeFixException : Exception
	{
		public CodeFixException()
		{
		}

		public CodeFixException(string message)
			: base(message)
		{
		}

		public CodeFixException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
