using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Options;

namespace WTG.BulkAnalysis.Core
{
	public static class Processor
	{
		public static async Task ProcessAsync(RunContext context)
		{
			context.Log.WriteLine("Rule IDs:");

			foreach (var rule in context.RuleIds)
			{
				context.Log.WriteFormatted($"  - {rule}");
			}

			context.Log.WriteLine();

			var counter = 0;
			var numSolutions = context.SolutionPaths.Length;
			var cache = AnalyzerCache.Create(context.RuleIds, context.LoadDir, context.LoadList);

			foreach (var solutionPath in context.SolutionPaths)
			{
				context.Log.WriteFormatted($"* Solution: {Path.GetFileName(solutionPath)} ({counter + 1}/{numSolutions})...");

				using (var workspace = await LoadSolutionIntoNewWorkspace(context.Log, solutionPath, context.CancellationToken).ConfigureAwait(false))
				{
					ConfigureWorkspace(workspace);

					var solution = workspace.CurrentSolution;
					var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToArray();

					if (csharpProjects.Length == 0)
					{
						context.Log.WriteLine("  - No C# projects! Moving on to next solution...", LogLevel.Warning);
					}
					else
					{
						context.Log.WriteFormatted($"  - Found {csharpProjects.Length} projects.", LogLevel.Info);

						var processor = new SolutionProcessor(context, cache, workspace);
						await processor.ProcessSolutionAsync().ConfigureAwait(false);
					}
				}

				counter++;
			}
		}

		static async Task<Workspace> LoadSolutionIntoNewWorkspace(ILog log, string path, CancellationToken cancellationToken)
		{
			var workspace = MSBuildWorkspace.Create();
			try
			{
				await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);

				foreach (var diagnostic in workspace.Diagnostics)
				{
					switch (diagnostic.Kind)
					{
						case WorkspaceDiagnosticKind.Failure:
							log.WriteLine(diagnostic.Message, LogLevel.Error);
							break;

						case WorkspaceDiagnosticKind.Warning:
							log.WriteLine(diagnostic.Message, LogLevel.Warning);
							break;
					}
				}

				return workspace;
			}
			catch
			{
				workspace.Dispose();
				throw;
			}
		}

		static void ConfigureWorkspace(Workspace workspace)
		{
			workspace.Options = workspace.Options
				.WithChangedOption(UseTabsOptionKey, true)
				.WithChangedOption(TabSizeOptionKey, 4)
				.WithChangedOption(IndentationSizeOptionKey, 4);
		}

		static readonly OptionKey UseTabsOptionKey = new OptionKey(FormattingOptions.UseTabs, LanguageNames.CSharp);
		static readonly OptionKey TabSizeOptionKey = new OptionKey(FormattingOptions.TabSize, LanguageNames.CSharp);
		static readonly OptionKey IndentationSizeOptionKey = new OptionKey(FormattingOptions.IndentationSize, LanguageNames.CSharp);
	}
}
