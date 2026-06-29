using System;
using System.Globalization;
using Microsoft.Extensions.Logging;
using WTG.BulkAnalysis.Core;
using CoreLogLevel = WTG.BulkAnalysis.Core.LogLevel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace WTG.BulkAnalysis.Mcp
{
	/// <summary>Bridges the engine's <see cref="ILog"/> to an <see cref="ILogger"/> (which writes to stderr).</summary>
	public sealed class McpLog : ILog
	{
		public McpLog(ILogger logger)
		{
			this.logger = logger;
		}

		public void WriteFormatted(FormattableString message, CoreLogLevel level = CoreLogLevel.Normal)
			=> logger.Log(Map(level), "{Message}", message.ToString(CultureInfo.CurrentCulture));

		public void WriteLine(string message, CoreLogLevel level = CoreLogLevel.Normal)
			=> logger.Log(Map(level), "{Message}", message);

		public void WriteLine()
		{
		}

		static MsLogLevel Map(CoreLogLevel level) => level switch
		{
			CoreLogLevel.Error => MsLogLevel.Error,
			CoreLogLevel.Warning => MsLogLevel.Warning,
			_ => MsLogLevel.Information,
		};

		readonly ILogger logger;
	}
}
