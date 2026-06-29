using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WTG.BulkAnalysis.Core;
using WTG.BulkAnalysis.Mcp;

// Resolve analyzer dependencies (loaded via Assembly.LoadFile) from their own directory.
AnalyzerAssemblyResolver.Register();

// Register the installed .NET SDK's MSBuild before any project is opened so MSBuildWorkspace
// (Roslyn 5.x) can locate it.
if (!MSBuildLocator.IsRegistered)
{
	MSBuildLocator.RegisterDefaults();
}

await Server.RunAsync(args).ConfigureAwait(false);

namespace WTG.BulkAnalysis.Mcp
{
	static class Server
	{
		public static async Task RunAsync(string[] args)
		{
			var builder = Host.CreateApplicationBuilder(args);

			// stdout is reserved for the MCP protocol over stdio; route all logging to stderr.
			builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Trace);

			builder.Services.AddSingleton<SessionManager>();
			builder.Services
				.AddMcpServer()
				.WithStdioServerTransport()
				.WithToolsFromAssembly();

			using var host = builder.Build();
			await host.RunAsync().ConfigureAwait(false);
		}
	}
}
