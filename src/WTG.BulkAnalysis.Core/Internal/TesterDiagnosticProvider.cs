using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace WTG.BulkAnalysis.Core
{
	sealed class TesterDiagnosticProvider : FixAllContext.DiagnosticProvider
	{
		public TesterDiagnosticProvider(ImmutableDictionary<ProjectId, ImmutableDictionary<string, ImmutableArray<Diagnostic>>> documentDiagnostics, ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> projectDiagnostics)
		{
			this.documentDiagnostics = documentDiagnostics;
			this.projectDiagnostics = projectDiagnostics;
		}

		public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
		{
			if (!projectDiagnostics.TryGetValue(project.Id, out var filteredProjectDiagnostics))
			{
				filteredProjectDiagnostics = ImmutableArray<Diagnostic>.Empty;
			}

			if (!documentDiagnostics.TryGetValue(project.Id, out var filteredDocumentDiagnostics))
			{
				return Task.FromResult(filteredProjectDiagnostics.AsEnumerable());
			}

			return Task.FromResult(filteredProjectDiagnostics.Concat(filteredDocumentDiagnostics.Values.SelectMany(i => i)));
		}

		public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
		{
			if (document.FilePath != null &&
				documentDiagnostics.TryGetValue(document.Project.Id, out var projectDocumentDiagnostics) &&
				projectDocumentDiagnostics.TryGetValue(document.FilePath, out var diagnostics))
			{
				return Task.FromResult(diagnostics.AsEnumerable());
			}

			return Task.FromResult(Enumerable.Empty<Diagnostic>());
		}

		public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
		{
			if (!projectDiagnostics.TryGetValue(project.Id, out var diagnostics))
			{
				return Task.FromResult(Enumerable.Empty<Diagnostic>());
			}

			return Task.FromResult(diagnostics.AsEnumerable());
		}

		readonly ImmutableDictionary<ProjectId, ImmutableDictionary<string, ImmutableArray<Diagnostic>>> documentDiagnostics;
		readonly ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> projectDiagnostics;
	}
}
