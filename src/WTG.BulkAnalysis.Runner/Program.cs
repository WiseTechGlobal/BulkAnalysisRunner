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

namespace WTG.BulkAnalysis.Runner
{
	static class Program
	{
		static async Task Main(string[] args)
		{
			var arguments = ParseArguments(args);

			if (arguments == null)
			{
				return;
			}

			AppDomain.CurrentDomain.AssemblyResolve += OnAppDomainAssemblyResolve;

			using var cts = new CancellationTokenSource();

			Console.CancelKeyPress += (sender, e) =>
			{
				e.Cancel = true;
				cts.Cancel();
			};

			await MainAsync(arguments, ConsoleLog.Instance, cts.Token).ConfigureAwait(false);
		}

		static async Task MainAsync(CommandLineArgs arguments, ILog log, CancellationToken cancellationToken)
		{
			if (!RegisterMSBuild(log))
			{
				return;
			}

			if (arguments.Pause)
			{
				Console.WriteLine("Press any key to continue.");
				Console.ReadKey();
			}

			using var reportGenerator = OpenReporter(arguments);

			var sw = new Stopwatch();
			sw.Start();

			try
			{
				var context = NewContext(arguments, reportGenerator, log, cancellationToken);
				await Processor.ProcessAsync(context).ConfigureAwait(false);
			}
			catch (InvalidConfigurationException ex)
			{
				log.WriteLine(ex.Message, LogLevel.Error);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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

		static bool RegisterMSBuild(ILog log)
		{
			VisualStudioInstance? selected = null;

			log.WriteLine("Checking for VS instances:");

			foreach (var instance in MSBuildLocator.QueryVisualStudioInstances())
			{
				log.WriteFormatted($"  {instance.Version}");

				if (selected == null || selected.Version < instance.Version)
				{
					selected = instance;
				}
			}

			if (selected != null)
			{
				log.WriteFormatted($"Selected: {selected.Version}");
				MSBuildLocator.RegisterInstance(selected);
				return true;
			}
			else
			{
				log.WriteFormatted($"No instances found!", LogLevel.Error);
				return false;
			}
		}

		static RunContext NewContext(CommandLineArgs arguments, XmlReportGenerator? reportGenerator, ILog log, CancellationToken cancellationToken)
		{
			return new RunContext(
				SolutionLocator.Locate(arguments.Path, CreateFilter(arguments)),
				NullVersionControl.Instance,
				arguments.Fix,
				ImmutableHashSet.CreateRange(arguments.RuleIDs),
				arguments.LoadDir,
				arguments.LoadList?.ToImmutableArray() ?? ImmutableArray<string>.Empty,
				log,
				reportGenerator,
				arguments.Configuration,
				arguments.Debug,
				cancellationToken);
		}

		static XmlReportGenerator? OpenReporter(CommandLineArgs arguments)
		{
			return string.IsNullOrEmpty(arguments.Report) ? null : XmlReportGenerator.New(arguments.Report);
		}

		static CommandLineArgs? ParseArguments(string[] args)
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

		static Assembly? OnAppDomainAssemblyResolve(object sender, ResolveEventArgs args)
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

		static Func<string, bool>? CreateFilter(CommandLineArgs arguments)
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
