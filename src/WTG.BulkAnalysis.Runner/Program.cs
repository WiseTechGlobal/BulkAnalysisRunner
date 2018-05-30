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
			AppDomain.CurrentDomain.AssemblyResolve += OnAppDomainAssemblyResolve;

			using (var cts = new CancellationTokenSource())
			{
				Console.CancelKeyPress += (sender, e) =>
				{
					e.Cancel = true;
					cts.Cancel();
				};

				await MainAsync(args, ConsoleLog.Instance, cts.Token).ConfigureAwait(false);
			}
		}

		static async Task MainAsync(string[] args, ILog log, CancellationToken cancellationToken)
		{
			var value = ParseArguments(args);

			MSBuildLocator.RegisterDefaults();

			if (value != null)
			{
				if (value.Pause)
				{
					Console.WriteLine("Press any key to continue.");
					Console.ReadKey();
				}

				using (var reportGenerator = OpenReporter(value))
				using (var versionControl = TfsVersionControl.Create(new Uri(value.ServerUrl, UriKind.Absolute), value.Path))
				{
					var context = NewContext(value, reportGenerator, versionControl, log, cancellationToken);

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
		}

		static RunContext NewContext(CommandLineArgs value, XmlReportGenerator reportGenerator, TfsVersionControl versionControl, ILog log, CancellationToken cancellationToken)
		{
			return new RunContext(
				value.Path,
				versionControl,
				CreateFilter(value),
				value.Fix,
				ImmutableHashSet.CreateRange(value.RuleIDs),
				value.LoadDir,
				value.LoadList?.ToImmutableArray() ?? ImmutableArray<string>.Empty,
				log,
				reportGenerator,
				cancellationToken);
		}

		static XmlReportGenerator OpenReporter(CommandLineArgs value)
		{
			return string.IsNullOrEmpty(value.Report) ? null : XmlReportGenerator.New(value.Report);
		}

		static CommandLineArgs ParseArguments(string[] args)
		{
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

		static Func<string, bool> CreateFilter(CommandLineArgs value)
		{
			if (value.Filter == null)
			{
				return null;
			}

			var regex = new Regex(value.Filter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
			return regex.IsMatch;
		}
	}
}
