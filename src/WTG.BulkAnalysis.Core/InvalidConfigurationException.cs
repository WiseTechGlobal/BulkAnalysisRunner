using System;
#if NETFRAMEWORK
using System.Runtime.Serialization;
#endif

namespace WTG.BulkAnalysis.Core
{
	[Serializable]
	public class InvalidConfigurationException : Exception
	{
		public InvalidConfigurationException()
		{
		}

		public InvalidConfigurationException(string message)
			: base(message)
		{
		}

		public InvalidConfigurationException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

#if NETFRAMEWORK
		protected InvalidConfigurationException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
#endif
	}
}
