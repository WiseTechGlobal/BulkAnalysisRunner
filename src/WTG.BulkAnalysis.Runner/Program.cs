using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Build.Locator;
using WTG.BulkAnalysis.Core;
using WTG.BulkAnalysis.TFS;

namespace WTG.BulkAnalysis.Runner
{
	static class Program
	{
		static async Task Main(string[] args)
		{
			if (args == null || args.Length == 0)
			{
				// Workaround for https://github.com/commandlineparser/commandline/issues/115
				args = new[] { "--help" };
			}

			var arguments = ParseArguments(args);

			if (arguments == null)
			{
				return;
			}

			AppDomain.CurrentDomain.AssemblyResolve += OnAppDomainAssemblyResolve;

			using (var cts = new CancellationTokenSource())
			{
				Console.CancelKeyPress += (sender, e) =>
				{
					e.Cancel = true;
					cts.Cancel();
				};

				await MainAsync(arguments, ConsoleLog.Instance, cts.Token).ConfigureAwait(false);
			}
		}

		static async Task MainAsync(CommandLineArgs arguments, ILog log, CancellationToken cancellationToken)
		{
			MSBuildLocator.RegisterDefaults();

			if (arguments.Pause)
			{
				Console.WriteLine("Press any key to continue.");
				Console.ReadKey();
			}

			using (var reportGenerator = OpenReporter(arguments))
			using (var versionControl = TfsVersionControl.Create(arguments.Path))
			{
				if (versionControl == null)
				{
					log.WriteLine($"No workspace mapping found for '{arguments.Path}'.", LogLevel.Error);
					return;
				}

				var context = NewContext(arguments, reportGenerator, versionControl, log, cancellationToken);

				var sw = new Stopwatch();
				sw.Start();

				try
				{
					await Processor.ProcessAsync(context).ConfigureAwait(false);
				}
				catch (InvalidConfigurationException ex)
				{
					log.WriteLine(ex.Message, LogLevel.Error);
				}
				catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
				{
					log.WriteLine();
					log.WriteLine("Aborted.", LogLevel.Error);
				}
				finally
				{
					sw.Stop();
					log.WriteFormatted($"Time taken: {sw.Elapsed.TotalSeconds} seconds");
				}
			}
		}

		static RunContext NewContext(CommandLineArgs arguments, XmlReportGenerator reportGenerator, IVersionControl versionControl, ILog log, CancellationToken cancellationToken)
		{
			return new RunContext(
				arguments.Path,
				versionControl,
				CreateFilter(arguments),
				arguments.Fix,
				ImmutableHashSet.CreateRange(arguments.RuleIDs),
				arguments.LoadDir,
				arguments.LoadList?.ToImmutableArray() ?? ImmutableArray<string>.Empty,
				log,
				reportGenerator,
				cancellationToken);
		}

		static XmlReportGenerator OpenReporter(CommandLineArgs arguments)
		{
			return string.IsNullOrEmpty(arguments.Report) ? null : XmlReportGenerator.New(arguments.Report);
		}

		static CommandLineArgs ParseArguments(string[] args)
		{
			if (args == null || args.Length == 0)
			{
				args = new[] { "--help" };
			}

			var parseResult = Parser.Default.ParseArguments<CommandLineArgs>(args);

			if (parseResult.Tag != ParserResultType.Parsed)
			{
				return null;
			}

			return ((Parsed<CommandLineArgs>)parseResult).Value;
		}

		static Assembly OnAppDomainAssemblyResolve(object sender, ResolveEventArgs args)
		{
			if (args.RequestingAssembly == null)
			{
				return null;
			}

			var requesterPath = new Uri(args.RequestingAssembly.CodeBase, UriKind.Absolute).LocalPath;
			var directory = Path.GetDirectoryName(requesterPath);
			var assemblyPath = Path.Combine(directory, new AssemblyName(args.Name).Name + ".dll");
			return Assembly.LoadFile(assemblyPath);
		}

		static Func<string, bool> CreateFilter(CommandLineArgs arguments)
		{
			if (arguments.Filter == null)
			{
				return null;
			}

			var regex = new Regex(arguments.Filter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
			return regex.IsMatch;
		}
	}
}
