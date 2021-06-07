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
			Project project,
			FixAllProvider fixAllProvider,
			CodeFixProvider codeFixProvider,
			ImmutableDictionary<string, ImmutableArray<Diagnostic>> documentDiagnosticsToFix,
			ImmutableArray<Diagnostic> projectDiagnosticsToFix)
		{
			codeFixEquivalenceKey = equivalenceKey;
			this.project = project;
			this.fixAllProvider = fixAllProvider;
			this.codeFixProvider = codeFixProvider;
			this.documentDiagnosticsToFix = documentDiagnosticsToFix;
			this.projectDiagnosticsToFix = projectDiagnosticsToFix;

			NumberOfDiagnostics = documentDiagnosticsToFix
				.Sum(y => y.Value.Length)
				+ projectDiagnosticsToFix.Length;
		}

		public int NumberOfDiagnostics { get; }

		public static async Task<ImmutableArray<CodeFixEquivalenceGroup>> CreateAsync(CodeFixProvider codeFixProvider, ImmutableArray<Diagnostic> allDiagnostics, Project project, CancellationToken cancellationToken)
		{
			var fixAllProvider = codeFixProvider.GetFixAllProvider();

			if (fixAllProvider == null)
			{
				return ImmutableArray.Create<CodeFixEquivalenceGroup>();
			}

			var groupLookup = new Dictionary<string, Builder>();
			var equivalenceKeys = new HashSet<string>();

			foreach (var diagnostic in allDiagnostics)
			{
				if (!codeFixProvider.FixableDiagnosticIds.Contains(diagnostic.Id))
				{
					continue;
				}

				var codeActions = await GetFixesAsync(project, codeFixProvider, diagnostic, cancellationToken).ConfigureAwait(false);
				equivalenceKeys.Clear();

				foreach (var action in codeActions)
				{
					if (action.EquivalenceKey != null)
					{
						equivalenceKeys.Add(action.EquivalenceKey);
					}
				}

				foreach (var key in equivalenceKeys)
				{
					if (!groupLookup.TryGetValue(key, out var group))
					{
						groupLookup.Add(key, group = new Builder(key, project, fixAllProvider, codeFixProvider));
					}

					group.AddDiagnostic(diagnostic);
				}
			}

			return groupLookup.Select(x => x.Value.ToEquivalenceGroup()).ToImmutableArray();
		}

		public async Task<ImmutableArray<CodeActionOperation>> GetOperationsAsync(CancellationToken cancellationToken)
		{
			var diagnostic = documentDiagnosticsToFix
				.Values
				.SelectMany(i => i)
				.Concat(projectDiagnosticsToFix)
				.First();

			var document = project.GetDocument(diagnostic.Location.SourceTree);

			if (document == null)
			{
				return ImmutableArray<CodeActionOperation>.Empty;
			}

			var diagnosticIds = new HashSet<string>(
				documentDiagnosticsToFix
					.Values
					.SelectMany(i => i)
					.Concat(projectDiagnosticsToFix)
					.Select(j => j.Id));

			var diagnosticsProvider = new TesterDiagnosticProvider(
				project.Id,
				documentDiagnosticsToFix,
				projectDiagnosticsToFix);

			var context = new FixAllContext(
				document,
				codeFixProvider,
				FixAllScope.Solution,
				codeFixEquivalenceKey,
				diagnosticIds,
				diagnosticsProvider,
				cancellationToken);

			try
			{
				var action = await fixAllProvider
					.GetFixAsync(context)
					.ConfigureAwait(false);

				if (action == null)
				{
					return ImmutableArray<CodeActionOperation>.Empty;
				}

				return await action
					.GetOperationsAsync(cancellationToken)
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new CodeFixException("Exception running code fixer.", ex);
			}
		}

		static async Task<IEnumerable<CodeAction>> GetFixesAsync(Project project, CodeFixProvider codeFixProvider, Diagnostic diagnostic, CancellationToken cancellationToken)
		{
			var document = project.GetDocument(diagnostic.Location.SourceTree);

			if (document == null)
			{
				return Enumerable.Empty<CodeAction>();
			}

			var codeActions = new List<CodeAction>();
			try
			{
				await codeFixProvider
					.RegisterCodeFixesAsync(
						new CodeFixContext(
							document,
							diagnostic,
							(a, d) => codeActions.Add(a),
							cancellationToken))
					.ConfigureAwait(false);
				return codeActions;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new CodeFixException("Exception collecting code fixes.", ex);
			}
		}

		readonly string codeFixEquivalenceKey;
		readonly Project project;
		readonly FixAllProvider fixAllProvider;
		readonly CodeFixProvider codeFixProvider;
		readonly ImmutableArray<Diagnostic> projectDiagnosticsToFix;
		readonly ImmutableDictionary<string, ImmutableArray<Diagnostic>> documentDiagnosticsToFix;

		sealed class Builder
		{
			public Builder(string equivalenceKey, Project project, FixAllProvider fixAllProvider, CodeFixProvider codeFixProvider)
			{
				this.equivalenceKey = equivalenceKey;
				this.project = project;
				this.fixAllProvider = fixAllProvider;
				this.codeFixProvider = codeFixProvider;
				documentDiagnostics = new Dictionary<string, List<Diagnostic>>();
				projectDiagnostics = new List<Diagnostic>();
			}

			public void AddDiagnostic(Diagnostic diagnostic)
			{
				if (diagnostic.Location.IsInSource)
				{
					var sourcePath = diagnostic.Location.GetLineSpan().Path;

					if (!documentDiagnostics.TryGetValue(sourcePath, out var diagnosticsInFile))
					{
						diagnosticsInFile = new List<Diagnostic>();
						documentDiagnostics.Add(sourcePath, diagnosticsInFile);
					}

					diagnosticsInFile.Add(diagnostic);
				}
				else
				{
					projectDiagnostics.Add(diagnostic);
				}
			}

			public CodeFixEquivalenceGroup ToEquivalenceGroup()
			{
				var documentDiagnosticsToFix = documentDiagnostics.ToImmutableDictionary(j => j.Key, j => j.Value.ToImmutableArray(), StringComparer.OrdinalIgnoreCase);
				var projectDiagnosticsToFix = projectDiagnostics.ToImmutableArray();

				return new CodeFixEquivalenceGroup(
					equivalenceKey,
					project,
					fixAllProvider,
					codeFixProvider,
					documentDiagnosticsToFix,
					projectDiagnosticsToFix);
			}

			readonly string equivalenceKey;
			readonly Project project;
			readonly FixAllProvider fixAllProvider;
			readonly CodeFixProvider codeFixProvider;
			readonly Dictionary<string, List<Diagnostic>> documentDiagnostics;
			readonly List<Diagnostic> projectDiagnostics;
		}
	}
}
