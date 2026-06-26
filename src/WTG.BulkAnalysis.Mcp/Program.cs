using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WTG.BulkAnalysis.Mcp;

// Analyzer assemblies are loaded with Assembly.LoadFile, which does not probe for an assembly's
// dependencies. Resolve them from the requesting assembly's own directory so analyzers (e.g. those
// that ship a separate *.Utils.dll) can run instead of throwing at execution time (reported as AD0001).
AppDomain.CurrentDomain.AssemblyResolve += static (_, args) =>
{
	var requester = args.RequestingAssembly;

	if (requester == null || string.IsNullOrEmpty(requester.Location))
	{
		return null;
	}

	var directory = Path.GetDirectoryName(requester.Location);
	var name = new AssemblyName(args.Name).Name;

	if (directory == null || name == null)
	{
		return null;
	}

	var candidate = Path.Combine(directory, name + ".dll");
	return File.Exists(candidate) ? Assembly.LoadFile(candidate) : null;
};

// MSBuildWorkspace (Roslyn 4.7) loads MSBuild in-process, so register the installed SDK's MSBuild
// before any project is opened. (4.7 is used deliberately: 4.8+ moved to an out-of-process BuildHost
// that fails to connect when hosted under an stdio server.)
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
			builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

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
