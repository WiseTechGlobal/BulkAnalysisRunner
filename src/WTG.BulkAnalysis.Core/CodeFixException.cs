using System;
#if NETFRAMEWORK
using System.Runtime.Serialization;
#endif

namespace WTG.BulkAnalysis.Core
{
	[Serializable]
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

#if NETFRAMEWORK
		protected CodeFixException(SerializationInfo serializationInfo, StreamingContext streamingContext)
			: base(serializationInfo, streamingContext)
		{
		}
#endif
	}
}
