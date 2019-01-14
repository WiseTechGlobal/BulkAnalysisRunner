using System;
using System.Runtime.Serialization;

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

		protected CodeFixException(SerializationInfo serializationInfo, StreamingContext streamingContext)
			: base(serializationInfo, streamingContext)
		{
		}
	}
}
