using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace WTG.BulkAnalysis.Core
{
	sealed class CodeFixEquivalenceGroup
	{
		CodeFixEquivalenceGroup(
			string equivalenceKey,
			Solution solution,
			FixAllProvider fixAllProvider,
			CodeFixProvider codeFixProvider,
			ImmutableDictionary<ProjectId, ImmutableDictionary<string, ImmutableArray<Diagnostic>>> documentDiagnosticsToFix,
			ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> projectDiagnosticsToFix)
		{
			CodeFixEquivalenceKey = equivalenceKey;
			Solution = solution;
			FixAllProvider = fixAllProvider;
			CodeFixProvider = codeFixProvider;
			DocumentDiagnosticsToFix = documentDiagnosticsToFix;
			ProjectDiagnosticsToFix = projectDiagnosticsToFix;

			NumberOfDiagnostics = documentDiagnosticsToFix
				.SelectMany(x => x.Value)
				.Sum(y => y.Value.Length)
				+ projectDiagnosticsToFix.Sum(x => x.Value.Length);
		}

		public string CodeFixEquivalenceKey { get; }
		public Solution Solution { get; }
		public FixAllProvider FixAllProvider { get; }
		public CodeFixProvider CodeFixProvider { get; }
		public ImmutableDictionary<ProjectId, ImmutableDictionary<string, ImmutableArray<Diagnostic>>> DocumentDiagnosticsToFix { get; }
		public ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> ProjectDiagnosticsToFix { get; }
		public int NumberOfDiagnostics { get; }

		public static async Task<ImmutableArray<CodeFixEquivalenceGroup>> CreateAsync(CodeFixProvider codeFixProvider, ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> allDiagnostics, Solution solution, CancellationToken cancellationToken)
		{
			var fixAllProvider = codeFixProvider.GetFixAllProvider();

			if (fixAllProvider == null)
			{
				return ImmutableArray.Create<CodeFixEquivalenceGroup>();
			}

			var groupLookup = new Dictionary<string, Builder>();

			foreach (var projectDiagnostics in allDiagnostics)
			{
				foreach (var diagnostic in projectDiagnostics.Value)
				{
					if (!codeFixProvider.FixableDiagnosticIds.Contains(diagnostic.Id))
					{
						continue;
					}

					var codeActions = await GetFixesAsync(solution, codeFixProvider, diagnostic, cancellationToken).ConfigureAwait(false);

					foreach (var key in codeActions.Select(x => x.EquivalenceKey).Distinct())
					{
						if (!groupLookup.TryGetValue(key, out var group))
						{
							groupLookup.Add(key, group = new Builder(key, solution, fixAllProvider, codeFixProvider));
						}

						group.AddDiagnostic(projectDiagnostics.Key, diagnostic);
					}
				}
			}

			return groupLookup.Select(x => x.Value.ToEquivalenceGroup()).ToImmutableArray();
		}

		public async Task<ImmutableArray<CodeActionOperation>> GetOperationsAsync(CancellationToken cancellationToken)
		{
			var diagnostic = DocumentDiagnosticsToFix
				.Values
				.SelectMany(i => i.Values)
				.Concat(ProjectDiagnosticsToFix.Values)
				.First()
				.First();

			var document = Solution.GetDocument(diagnostic.Location.SourceTree);

			var diagnosticIds = new HashSet<string>(
				DocumentDiagnosticsToFix
					.Values
					.SelectMany(i => i.Values)
					.Concat(ProjectDiagnosticsToFix.Values)
					.SelectMany(i => i)
					.Select(j => j.Id));

			var diagnosticsProvider = new TesterDiagnosticProvider(
				DocumentDiagnosticsToFix,
				ProjectDiagnosticsToFix);

			var context = new FixAllContext(
				document,
				CodeFixProvider,
				FixAllScope.Solution,
				CodeFixEquivalenceKey,
				diagnosticIds,
				diagnosticsProvider,
				cancellationToken);

			var action = await FixAllProvider
				.GetFixAsync(context)
				.ConfigureAwait(false);

			return await action
				.GetOperationsAsync(cancellationToken)
				.ConfigureAwait(false);
		}

		static async Task<IEnumerable<CodeAction>> GetFixesAsync(Solution solution, CodeFixProvider codeFixProvider, Diagnostic diagnostic, CancellationToken cancellationToken)
		{
			var codeActions = new List<CodeAction>();
			await codeFixProvider
				.RegisterCodeFixesAsync(
					new CodeFixContext(
						solution.GetDocument(diagnostic.Location.SourceTree),
						diagnostic,
						(a, d) => codeActions.Add(a),
						cancellationToken))
				.ConfigureAwait(false);
			return codeActions;
		}

		sealed class Builder
		{
			public Builder(string equivalenceKey, Solution solution, FixAllProvider fixAllProvider, CodeFixProvider codeFixProvider)
			{
				this.equivalenceKey = equivalenceKey;
				this.solution = solution;
				this.fixAllProvider = fixAllProvider;
				this.codeFixProvider = codeFixProvider;
				documentDiagnostics = new Dictionary<ProjectId, Dictionary<string, List<Diagnostic>>>();
				projectDiagnostics = new Dictionary<ProjectId, List<Diagnostic>>();
			}

			public void AddDiagnostic(ProjectId projectId, Diagnostic diagnostic)
			{
				if (diagnostic.Location.IsInSource)
				{
					var sourcePath = diagnostic.Location.GetLineSpan().Path;

					if (!documentDiagnostics.TryGetValue(projectId, out var projectDocumentDiagnostics))
					{
						projectDocumentDiagnostics = new Dictionary<string, List<Diagnostic>>();
						documentDiagnostics.Add(projectId, projectDocumentDiagnostics);
					}

					if (!projectDocumentDiagnostics.TryGetValue(sourcePath, out var diagnosticsInFile))
					{
						diagnosticsInFile = new List<Diagnostic>();
						projectDocumentDiagnostics.Add(sourcePath, diagnosticsInFile);
					}

					diagnosticsInFile.Add(diagnostic);
				}
				else
				{
					if (!projectDiagnostics.TryGetValue(projectId, out var diagnosticsInProject))
					{
						diagnosticsInProject = new List<Diagnostic>();
						projectDiagnostics.Add(projectId, diagnosticsInProject);
					}

					diagnosticsInProject.Add(diagnostic);
				}
			}

			public CodeFixEquivalenceGroup ToEquivalenceGroup()
			{
				var documentDiagnosticsToFix = documentDiagnostics.ToImmutableDictionary(i => i.Key, i => i.Value.ToImmutableDictionary(j => j.Key, j => j.Value.ToImmutableArray(), StringComparer.OrdinalIgnoreCase));
				var projectDiagnosticsToFix = projectDiagnostics.ToImmutableDictionary(i => i.Key, i => i.Value.ToImmutableArray());

				return new CodeFixEquivalenceGroup(
					equivalenceKey,
					solution,
					fixAllProvider,
					codeFixProvider,
					documentDiagnosticsToFix,
					projectDiagnosticsToFix);
			}

			readonly string equivalenceKey;
			readonly Solution solution;
			readonly FixAllProvider fixAllProvider;
			readonly CodeFixProvider codeFixProvider;
			readonly Dictionary<ProjectId, Dictionary<string, List<Diagnostic>>> documentDiagnostics;
			readonly Dictionary<ProjectId, List<Diagnostic>> projectDiagnostics;
		}
	}
}
