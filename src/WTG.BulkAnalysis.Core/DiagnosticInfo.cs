using System.Globalization;
using Microsoft.CodeAnalysis;

namespace WTG.BulkAnalysis.Core
{
	public sealed class DiagnosticInfo
	{
		public DiagnosticInfo(
			string id,
			string title,
			string message,
			string severity,
			string category,
			string projectName,
			string? filePath,
			int startLine,
			int startColumn,
			int endLine,
			int endColumn,
			bool hasCodeFix)
		{
			Id = id;
			Title = title;
			Message = message;
			Severity = severity;
			Category = category;
			ProjectName = projectName;
			FilePath = filePath;
			StartLine = startLine;
			StartColumn = startColumn;
			EndLine = endLine;
			EndColumn = endColumn;
			HasCodeFix = hasCodeFix;
		}

		public string Id { get; }
		public string Title { get; }
		public string Message { get; }
		public string Severity { get; }
		public string Category { get; }
		public string ProjectName { get; }

		/// <summary>The source file the diagnostic points at, or <c>null</c> for project-level diagnostics.</summary>
		public string? FilePath { get; }

		// Positions are 1-based for human/agent friendliness (Roslyn reports 0-based), or 0 when not in source.
		public int StartLine { get; }
		public int StartColumn { get; }
		public int EndLine { get; }
		public int EndColumn { get; }

		/// <summary>Whether at least one loaded code fix provider can fix this diagnostic's rule.</summary>
		public bool HasCodeFix { get; }

		public static DiagnosticInfo Create(Diagnostic diagnostic, string projectName, bool hasCodeFix)
		{
			var inSource = diagnostic.Location.IsInSource;
			var span = diagnostic.Location.GetLineSpan();
			var start = span.StartLinePosition;
			var end = span.EndLinePosition;

			return new DiagnosticInfo(
				diagnostic.Id,
				diagnostic.Descriptor.Title.ToString(CultureInfo.CurrentCulture),
				diagnostic.GetMessage(CultureInfo.CurrentCulture),
				diagnostic.Severity.ToString(),
				diagnostic.Descriptor.Category,
				projectName,
				inSource ? span.Path : null,
				inSource ? start.Line + 1 : 0,
				inSource ? start.Character + 1 : 0,
				inSource ? end.Line + 1 : 0,
				inSource ? end.Character + 1 : 0,
				hasCodeFix);
		}
	}
}
