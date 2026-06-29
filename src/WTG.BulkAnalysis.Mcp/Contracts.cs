using System.Collections.Generic;

namespace WTG.BulkAnalysis.Mcp
{
	/// <summary>Result of opening an analysis session.</summary>
	public sealed record OpenResult(
		string? SessionId,
		string Target,
		IReadOnlyList<string> Projects,
		IReadOnlyList<string>? CandidateSolutions,
		string Message);

	/// <summary>A grouped summary of diagnostics.</summary>
	public sealed record DiagnosticGroup(
		string Key,
		string? Title,
		string? Severity,
		string? Category,
		int Count,
		bool HasCodeFix);

	/// <summary>Result of listing/grouping diagnostics.</summary>
	public sealed record ListDiagnosticsResult(
		int Total,
		string GroupBy,
		IReadOnlyList<DiagnosticGroup> Groups);

	/// <summary>A single diagnostic occurrence.</summary>
	public sealed record DiagnosticOccurrence(
		string Id,
		string Severity,
		string Message,
		string ProjectName,
		string? FilePath,
		int StartLine,
		int StartColumn,
		int EndLine,
		int EndColumn,
		bool HasCodeFix);

	/// <summary>Paged list of individual diagnostic occurrences for a rule.</summary>
	public sealed record GetDiagnosticsResult(
		string RuleId,
		int Total,
		int Skip,
		int Take,
		IReadOnlyList<DiagnosticOccurrence> Occurrences);

	/// <summary>Result of applying fixes.</summary>
	public sealed record ApplyFixesResult(
		int OperationsApplied,
		IReadOnlyList<string> ChangedFiles,
		IReadOnlyDictionary<string, int> RemainingByRule);

	/// <summary>Result of closing a session.</summary>
	public sealed record CloseResult(
		bool Closed,
		string Message);
}
