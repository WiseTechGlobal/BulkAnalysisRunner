using System;

namespace WTG.BulkAnalysis.Core
{
	public class WorkspaceLoadException : Exception
	{
		public WorkspaceLoadException()
		{
		}

		public WorkspaceLoadException(string message)
			: base(message)
		{
		}

		public WorkspaceLoadException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
