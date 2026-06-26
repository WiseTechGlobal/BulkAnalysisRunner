using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

namespace WTG.BulkAnalysis.Core
{
	/// <summary>
	/// A stateful, in-memory analysis session over a single solution or project. The workspace and
	/// analyzers are loaded once (an expensive operation) and reused across analyze/fix calls.
	/// Callers must register an MSBuild instance (e.g. via MSBuildLocator) before opening a session.
	/// </summary>
	public sealed class AnalysisSession : IDisposable
	{
		AnalysisSession(MSBuildWorkspace workspace, AnalyzerCache cache, AnalysisSessionOptions options, ILog log)
		{
			this.workspace = workspace;
			this.cache = cache;
			this.options = options;
			this.log = log;
		}

		public string TargetPath => options.Path;

		public ImmutableArray<string> ProjectNames => workspace.CurrentSolution.Projects
			.Where(p => p.Language == LanguageNames.CSharp)
			.Select(p => p.Name)
			.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
			.ToImmutableArray();

		public static async Task<AnalysisSession> OpenAsync(AnalysisSessionOptions options, ILog log, CancellationToken cancellationToken)
		{
			if (options == null)
			{
				throw new ArgumentNullException(nameof(options));
			}

			if (log == null)
			{
				throw new ArgumentNullException(nameof(log));
			}

			var properties = new Dictionary<string, string>()
			{
				{ "Configuration", options.Configuration },
			};

			var workspace = MSBuildWorkspace.Create(properties);

			try
			{
				await LoadAsync(workspace, options.Path, log, cancellationToken).ConfigureAwait(false);
				Processor.ConfigureWorkspace(workspace);

				var cache = AnalyzerCache.CreateForDiscovery(options.LoadDir ?? string.Empty, options.LoadList);
				return new AnalysisSession(workspace, cache, options, log);
			}
			catch
			{
				workspace.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Runs every loaded analyzer over the C# projects and returns the resulting diagnostics.
		/// When <paramref name="ruleIds"/> is <c>null</c> all diagnostics are returned (discovery);
		/// otherwise the result is limited to the given rule ids.
		/// </summary>
		public async Task<ImmutableArray<DiagnosticInfo>> AnalyzeAsync(ImmutableHashSet<string>? ruleIds, CancellationToken cancellationToken)
		{
			await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

			try
			{
				return await AnalyzeCoreAsync(workspace.CurrentSolution, ruleIds, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				gate.Release();
			}
		}

		/// <summary>
		/// Applies fix-all operations for the requested rule ids until no further changes are made,
		/// writing the results to disk, and reports what changed and what remains.
		/// </summary>
		public async Task<FixResult> ApplyFixesAsync(ImmutableHashSet<string> ruleIds, CancellationToken cancellationToken)
		{
			if (ruleIds == null || ruleIds.Count == 0)
			{
				throw new ArgumentException("At least one rule id is required.", nameof(ruleIds));
			}

			await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

			try
			{
				var before = workspace.CurrentSolution;

				// Reuse the proven CLI fix loop (analyze -> fix-all -> repeat until stable), scoped to the requested rules.
				var context = new RunContext(
					ImmutableArray<string>.Empty,
					applyFixes: true,
					ruleIds,
					options.LoadDir ?? string.Empty,
					options.LoadList,
					log,
					reporter: null,
					options.Configuration,
					debug: false,
					cancellationToken);

				var processor = new SolutionProcessor(context, cache, workspace);
				var operationsApplied = await processor.ProcessSolutionAsync().ConfigureAwait(false);

				var after = workspace.CurrentSolution;
				var changedFiles = GetChangedFiles(before, after);

				var remaining = await AnalyzeCoreAsync(after, ruleIds, cancellationToken).ConfigureAwait(false);
				var remainingByRule = remaining
					.GroupBy(d => d.Id)
					.ToImmutableDictionary(g => g.Key, g => g.Count());

				return new FixResult(operationsApplied, changedFiles, remainingByRule);
			}
			finally
			{
				gate.Release();
			}
		}

		async Task<ImmutableArray<DiagnosticInfo>> AnalyzeCoreAsync(Solution solution, ImmutableHashSet<string>? ruleIds, CancellationToken cancellationToken)
		{
			var builder = ImmutableArray.CreateBuilder<DiagnosticInfo>();

			foreach (var project in solution.Projects)
			{
				if (project.Language != LanguageNames.CSharp)
				{
					continue;
				}

				var analyzers = cache.GetAnalyzers(project);

				if (analyzers.Length == 0)
				{
					continue;
				}

				var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

				if (compilation == null)
				{
					continue;
				}

				var providers = cache.GetAllCodeFixProviders(project);

				var diagnostics = await compilation
					.WithAnalyzers(analyzers, EmptyCompilationWithAnalyzersOptions)
					.GetAnalyzerDiagnosticsAsync(cancellationToken)
					.ConfigureAwait(false);

				foreach (var diagnostic in diagnostics)
				{
					if (ruleIds != null && !ruleIds.Contains(diagnostic.Id))
					{
						continue;
					}

					builder.Add(DiagnosticInfo.Create(diagnostic, project.Name, providers.ContainsKey(diagnostic.Id)));
				}
			}

			return builder.ToImmutable();
		}

		static async Task LoadAsync(MSBuildWorkspace workspace, string path, ILog log, CancellationToken cancellationToken)
		{
			try
			{
				if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
				{
					await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
				}
				else if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
				{
					await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
				}
				else
				{
					throw new InvalidConfigurationException("Unsupported target '" + path + "'. Expected a .sln, .slnx or .csproj path.");
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (InvalidConfigurationException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new WorkspaceLoadException("Error loading '" + path + "'.", ex);
			}

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
		}

		static ImmutableArray<string> GetChangedFiles(Solution before, Solution after)
		{
			var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var projectChange in after.GetChanges(before).GetProjectChanges())
			{
				foreach (var documentId in projectChange.GetChangedDocuments().Concat(projectChange.GetAddedDocuments()))
				{
					var path = after.GetDocument(documentId)?.FilePath;

					if (!string.IsNullOrEmpty(path))
					{
						files.Add(path!);
					}
				}
			}

			return files.ToImmutableArray();
		}

		public void Dispose()
		{
			workspace.Dispose();
			gate.Dispose();
		}

		readonly MSBuildWorkspace workspace;
		readonly AnalyzerCache cache;
		readonly AnalysisSessionOptions options;
		readonly ILog log;
		readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

		static readonly CompilationWithAnalyzersOptions EmptyCompilationWithAnalyzersOptions = new CompilationWithAnalyzersOptions(
			new AnalyzerOptions(ImmutableArray.Create<AdditionalText>()),
			null,
			true,
			false);
	}
}
