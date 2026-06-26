using System;
#if NETFRAMEWORK
using System.Runtime.Serialization;
#endif

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

#if NETFRAMEWORK
		protected WorkspaceLoadException(SerializationInfo serializationInfo, StreamingContext streamingContext)
			: base(serializationInfo, streamingContext)
		{
		}
#endif
	}
}
