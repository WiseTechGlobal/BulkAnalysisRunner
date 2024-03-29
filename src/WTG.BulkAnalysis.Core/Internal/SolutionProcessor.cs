using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WTG.BulkAnalysis.Core
{
	readonly struct SolutionProcessor
	{
		public SolutionProcessor(RunContext context, AnalyzerCache cache, Workspace workspace)
		{
			this.context = context;
			this.cache = cache;
			this.workspace = workspace;
		}

		public async Task ProcessSolutionAsync()
		{
			var operationsCounter = 0;
			var numPreviousDiagnostics = 0;
			var reported = false;

			do
			{
				var solution = workspace.CurrentSolution;
				var diagnostics = await GetAnalyzerDiagnosticsAsync(solution, cache).ConfigureAwait(false);
				var numDiagnostics = diagnostics.Sum(kvp => kvp.Value.Length);

				if (!reported)
				{
					context.Reporter?.Report(solution, diagnostics);
				}

				if (numDiagnostics == 0)
				{
					context.Log.WriteLine("  - No issues found in this solution. Moving on...", LogLevel.Info);
					break;
				}
				else if (numPreviousDiagnostics == numDiagnostics)
				{
					context.Log.WriteLine("  - Previous run did not apply changes. Moving on...", LogLevel.Info);
					break;
				}

				numPreviousDiagnostics = numDiagnostics;

				if (!context.ApplyFixes)
				{
					break;
				}

				var count = await ApplyFixesAsync(solution, diagnostics).ConfigureAwait(false);
				operationsCounter += count;

				if (count == 0)
				{
					break;
				}
			}
			while (true);

			if (context.ApplyFixes)
			{
				context.Log.WriteFormatted($"  - Applied {operationsCounter} fix-all operations to resolve errors.", LogLevel.Info);
			}
		}

		async Task<int> ApplyFixesAsync(Solution solution, ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> diagnostics)
		{
			var count = 0;

			foreach (var pair in diagnostics)
			{
				var project = solution.GetProject(pair.Key);

				if (project == null)
				{
					continue;
				}

				foreach (var provider in GetApplicableFixProviders(project, pair.Value))
				{
					var equivalenceGroups = await CodeFixEquivalenceGroup.CreateAsync(
						provider,
						pair.Value,
						project,
						context.CancellationToken).ConfigureAwait(false);

					foreach (var equivalence in equivalenceGroups)
					{
						var operations = await equivalence
							.GetOperationsAsync(context.CancellationToken)
							.ConfigureAwait(false);

						context.Log.WriteFormatted($"  - Applying {operations.Length} operations to resolve {equivalence.NumberOfDiagnostics} errors...", LogLevel.Success);

						foreach (var operation in operations)
						{
							operation.Apply(workspace, context.CancellationToken);
						}

						count += operations.Length;
					}
				}
			}

			return count;
		}

		async Task<ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>>> GetAnalyzerDiagnosticsAsync(Solution solution, AnalyzerCache cache)
		{
			var projectDiagnosticTasks = new List<KeyValuePair<ProjectId, Task<ImmutableArray<Diagnostic>>>>();

			foreach (var project in solution.Projects)
			{
				if (project.Language != LanguageNames.CSharp)
				{
					context.Log.WriteFormatted($"  - Skipping {project.Name} as it is not a C# project. (Language == '{project.Language}')", LogLevel.Warning);
					continue;
				}

				var analyzers = cache.GetAnalyzers(project);

				if (analyzers.Length == 0)
				{
					context.Log.WriteLine($"  - Skipping {project.Name} as it has no analyzers registered.", LogLevel.Warning);
					continue;
				}

				var task = GetProjectAnalyzerDiagnosticsAsync(project, analyzers);
				projectDiagnosticTasks.Add(new KeyValuePair<ProjectId, Task<ImmutableArray<Diagnostic>>>(project.Id, task));
			}

			var projectDiagnosticBuilder = ImmutableDictionary.CreateBuilder<ProjectId, ImmutableArray<Diagnostic>>();

			foreach (var task in projectDiagnosticTasks)
			{
				projectDiagnosticBuilder.Add(task.Key, await task.Value.ConfigureAwait(false));
			}

			return projectDiagnosticBuilder.ToImmutable();
		}

		async Task<ImmutableArray<Diagnostic>> GetProjectAnalyzerDiagnosticsAsync(Project project, ImmutableArray<DiagnosticAnalyzer> analyzers)
		{
			var options = project.CompilationOptions;

			if (options != null)
			{
				var modifiedSpecificDiagnosticOptions = options.SpecificDiagnosticOptions;

				foreach (var id in analyzerErrorIds)
				{
					modifiedSpecificDiagnosticOptions = modifiedSpecificDiagnosticOptions.Add(id, ReportDiagnostic.Error);
				}

				var modifiedCompilationOptions = options
					.WithSpecificDiagnosticOptions(modifiedSpecificDiagnosticOptions)
					.WithWarningLevel(4);

				project = project
					.WithCompilationOptions(modifiedCompilationOptions);
			}

			var compilation = await project
				.GetCompilationAsync(context.CancellationToken)
				.ConfigureAwait(false);

			if (compilation == null)
			{
				context.Log.WriteFormatted($"  - Skipping {project.Name} as it does not support compilation.", LogLevel.Warning);
				return ImmutableArray<Diagnostic>.Empty;
			}

			var compilationWithAnalyzers = compilation
				.WithAnalyzers(
					analyzers,
					EmptyCompilationWithAnalyzersOptions);

			var diagnostics = await compilationWithAnalyzers
				.GetAllDiagnosticsAsync(context.CancellationToken)
				.ConfigureAwait(false);

			var ruleIds = context.RuleIds;
			ImmutableArray<Diagnostic>.Builder? builder = null;

			foreach (var diagnostic in diagnostics)
			{
				if (analyzerErrorIds.Contains(diagnostic.Id))
				{
					if (context.Debug)
					{
						context.Log.WriteLine(diagnostic.Descriptor.Description.ToString(CultureInfo.CurrentCulture), LogLevel.Error);
					}
					else
					{
						context.Log.WriteLine(diagnostic.GetMessage(), LogLevel.Error);
					}
				}
				else if (ruleIds.Contains(diagnostic.Id))
				{
					builder ??= ImmutableArray.CreateBuilder<Diagnostic>();
					builder.Add(diagnostic);
				}
			}

			return builder == null ? ImmutableArray<Diagnostic>.Empty : builder.ToImmutable();
		}

		IEnumerable<CodeFixProvider> GetApplicableFixProviders(Project project, ImmutableArray<Diagnostic> diagnostics)
		{
			var codeFixProviders = cache.GetAllCodeFixProviders(project);

			return diagnostics
				.Select(y => y.Id)
				.Distinct()
				.SelectMany(x => ImmutableDictionary.GetValueOrDefault(codeFixProviders, x, EmptyCodeFixProviderList))
				.Distinct();
		}

		static readonly ImmutableList<CodeFixProvider> EmptyCodeFixProviderList = ImmutableList.Create<CodeFixProvider>();

		static readonly CompilationWithAnalyzersOptions EmptyCompilationWithAnalyzersOptions = new CompilationWithAnalyzersOptions(
			new AnalyzerOptions(ImmutableArray.Create<AdditionalText>()),
			null,
			true,
			false);

		readonly RunContext context;
		readonly AnalyzerCache cache;
		readonly Workspace workspace;

		static readonly ImmutableHashSet<string> analyzerErrorIds = ImmutableHashSet.Create("AD0001");
	}
}
