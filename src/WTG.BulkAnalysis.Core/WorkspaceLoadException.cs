using System;
using System.Runtime.Serialization;

namespace WTG.BulkAnalysis.Core
{
	[Serializable]
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

		protected WorkspaceLoadException(SerializationInfo serializationInfo, StreamingContext streamingContext)
			: base(serializationInfo, streamingContext)
		{
		}
	}
}
