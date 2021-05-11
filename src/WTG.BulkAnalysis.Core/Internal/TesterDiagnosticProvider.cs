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
		public TesterDiagnosticProvider(ProjectId projectId, ImmutableDictionary<string, ImmutableArray<Diagnostic>> documentDiagnostics, ImmutableArray<Diagnostic> projectDiagnostics)
		{
			this.projectId = projectId;
			this.documentDiagnostics = documentDiagnostics;
			this.projectDiagnostics = projectDiagnostics;
		}

		public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
		{
			if (projectId != project.Id)
			{
				return Task.FromResult(Enumerable.Empty<Diagnostic>());
			}

			if (documentDiagnostics.Count == 0)
			{
				return Task.FromResult(projectDiagnostics.AsEnumerable());
			}

			var result = documentDiagnostics.Values.SelectMany(i => i);

			if (projectDiagnostics.Length > 0)
			{
				result = projectDiagnostics.Concat(result);
			}

			return Task.FromResult(result);
		}

		public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
		{
			if (document.FilePath != null &&
				document.Project.Id == projectId &&
				documentDiagnostics.TryGetValue(document.FilePath, out var diagnostics))
			{
				return Task.FromResult(diagnostics.AsEnumerable());
			}

			return Task.FromResult(Enumerable.Empty<Diagnostic>());
		}

		public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
		{
			if (project.Id != projectId)
			{
				return Task.FromResult(Enumerable.Empty<Diagnostic>());
			}

			return Task.FromResult(projectDiagnostics.AsEnumerable());
		}

		readonly ProjectId projectId;
		readonly ImmutableDictionary<string, ImmutableArray<Diagnostic>> documentDiagnostics;
		readonly ImmutableArray<Diagnostic> projectDiagnostics;
	}
}
