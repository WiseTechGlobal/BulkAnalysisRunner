using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.Runner
{
	static class Program
	{
		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.AssemblyResolve += OnAppDomainAssemblyResolve;

			using (var cts = new CancellationTokenSource())
			{
				Console.CancelKeyPress += (sender, e) =>
				{
					e.Cancel = true;
					cts.Cancel();
				};

				MainAsync(args, cts.Token).GetAwaiter().GetResult();
			}
		}

		static async Task MainAsync(string[] args, CancellationToken cancellationToken)
		{
			var value = ParseArguments(args);

			if (value != null)
			{
				using (var reportGenerator = OpenReporter(value))
				using (var versionControl = TfsVersionControl.Create(new Uri(value.ServerUrl, UriKind.Absolute), value.Path))
				{
					var context = NewContext(value, reportGenerator, versionControl, cancellationToken);

					var sw = new Stopwatch();
					sw.Start();

					try
					{
						await Processor.ProcessAsync(context).ConfigureAwait(false);
					}
					catch (InvalidConfigurationException ex)
					{
						Console.WriteLine(ex.Message);
					}
					catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
					{
						Console.WriteLine();
						Console.WriteLine("Aborted.");
					}
					finally
					{
						sw.Stop();
						Console.WriteLine($"Time taken: {sw.Elapsed.TotalSeconds} seconds");
					}
				}
			}
		}

		static RunContext NewContext(CommandLineArgs value, XmlReportGenerator reportGenerator, TfsVersionControl versionControl, CancellationToken cancellationToken)
		{
			return new RunContext(
				value.Path,
				versionControl,
				CreateFilter(value),
				value.Fix,
				ImmutableHashSet.CreateRange(value.RuleIDs),
				value.LoadList?.ToImmutableArray() ?? ImmutableArray<string>.Empty,
				ConsoleLog.Instance,
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

			return value.Filter.IsContainedIn;
		}

		static bool IsContainedIn(this string substring, string value) => value.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0;
	}
}
