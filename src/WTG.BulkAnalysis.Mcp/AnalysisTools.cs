using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.Mcp
{
	[McpServerToolType]
	public static class AnalysisTools
	{
		[McpServerTool(Name = "open_analysis")]
		[Description("Open a solution or project for analysis and keep it loaded in memory for subsequent calls. " +
			"Accepts a .sln/.slnx/.csproj path, or a directory/branch containing a Build.xml to discover solutions. " +
			"Returns a sessionId used by the other tools.")]
		public static async Task<OpenResult> OpenAnalysisAsync(
			SessionManager sessions,
			ILoggerFactory loggerFactory,
			[Description("Path to a .sln/.slnx/.csproj file, or a directory/branch containing a Build.xml.")] string path,
			[Description("Build configuration to load (e.g. Debug or Release).")] string configuration = "Debug",
			[Description("Optional directory to load analyzer/code-fix assemblies from instead of the project's references.")] string? loadDir = null,
			[Description("Optional ';'-separated list of analyzer assemblies to load.")] string? load = null,
			[Description("Optional regex to filter discovered solutions when a directory/branch is given.")] string? filter = null,
			CancellationToken cancellationToken = default)
		{
			var full = Path.GetFullPath(path);
			string resolved;

			if (IsProjectOrSolutionFile(full))
			{
				resolved = full;
			}
			else
			{
				var found = SolutionLocator.Locate(full, BuildFilter(filter));

				if (found.Length == 0)
				{
					return new OpenResult(null, path, Array.Empty<string>(), null, "No solutions were found. Provide a .sln/.csproj path, or a directory containing a Build.xml.");
				}

				if (found.Length > 1)
				{
					return new OpenResult(null, path, Array.Empty<string>(), found, "Multiple solutions were found. Re-run with a specific .sln path or a 'filter' regex.");
				}

				resolved = found[0];
			}

			var loadList = string.IsNullOrWhiteSpace(load)
				? ImmutableArray<string>.Empty
				: load.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableArray();

			var options = new AnalysisSessionOptions(resolved, configuration, loadDir, loadList);
			var log = new McpLog(loggerFactory.CreateLogger("WTG.BulkAnalysis"));

			var session = await AnalysisSession.OpenAsync(options, log, cancellationToken).ConfigureAwait(false);
			var projects = session.ProjectNames;
			var id = sessions.Add(session);

			return new OpenResult(id, resolved, projects, null, $"Opened with {projects.Length} C# project(s).");
		}

		[McpServerTool(Name = "list_diagnostics")]
		[Description("Run all analyzers and return a grouped summary of the diagnostics. Each group reports its count " +
			"and whether a code fix provider is available (hasCodeFix), so you can tell what is bulk-fixable.")]
		public static async Task<ListDiagnosticsResult> ListDiagnosticsAsync(
			SessionManager sessions,
			[Description("Session id from open_analysis.")] string sessionId,
			[Description("Optional ';'-separated rule ids to limit the results to.")] string? ruleIds = null,
			[Description("Optional minimum severity: Hidden, Info, Warning or Error.")] string? minSeverity = null,
			[Description("How to group results: rule (default), severity, project or fixability.")] string groupBy = "rule",
			CancellationToken cancellationToken = default)
		{
			var session = Get(sessions, sessionId);
			var diagnostics = await session.AnalyzeAsync(ParseIds(ruleIds), cancellationToken).ConfigureAwait(false);
			var filtered = ApplyMinSeverity(diagnostics, minSeverity);

			var groups = GroupDiagnostics(filtered, groupBy);
			return new ListDiagnosticsResult(filtered.Length, groupBy, groups);
		}

		[McpServerTool(Name = "get_diagnostics")]
		[Description("List the individual occurrences (file, line, column, message) for a single rule id, paged.")]
		public static async Task<GetDiagnosticsResult> GetDiagnosticsAsync(
			SessionManager sessions,
			[Description("Session id from open_analysis.")] string sessionId,
			[Description("The rule id to list occurrences for.")] string ruleId,
			[Description("Number of occurrences to skip (for paging).")] int skip = 0,
			[Description("Maximum number of occurrences to return.")] int take = 50,
			CancellationToken cancellationToken = default)
		{
			var session = Get(sessions, sessionId);
			var ids = ImmutableHashSet.Create(StringComparer.Ordinal, ruleId);
			var diagnostics = await session.AnalyzeAsync(ids, cancellationToken).ConfigureAwait(false);

			var occurrences = diagnostics
				.OrderBy(d => d.FilePath, StringComparer.OrdinalIgnoreCase)
				.ThenBy(d => d.StartLine)
				.ThenBy(d => d.StartColumn)
				.Skip(Math.Max(0, skip))
				.Take(Math.Max(0, take))
				.Select(d => new DiagnosticOccurrence(d.Id, d.Severity, d.Message, d.ProjectName, d.FilePath, d.StartLine, d.StartColumn, d.EndLine, d.EndColumn, d.HasCodeFix))
				.ToArray();

			return new GetDiagnosticsResult(ruleId, diagnostics.Length, skip, take, occurrences);
		}

		[McpServerTool(Name = "apply_fixes")]
		[Description("Apply fix-all code fixes for the given rule ids until no further changes are made, writing the " +
			"results to disk. Only rules that have a code fix provider can be fixed. Returns what changed and what remains.")]
		public static async Task<ApplyFixesResult> ApplyFixesAsync(
			SessionManager sessions,
			[Description("Session id from open_analysis.")] string sessionId,
			[Description("';'-separated rule ids to fix.")] string ruleIds,
			CancellationToken cancellationToken = default)
		{
			var session = Get(sessions, sessionId);
			var ids = ParseIds(ruleIds);

			if (ids == null || ids.Count == 0)
			{
				throw new ArgumentException("At least one rule id is required.", nameof(ruleIds));
			}

			var result = await session.ApplyFixesAsync(ids, cancellationToken).ConfigureAwait(false);
			return new ApplyFixesResult(result.OperationsApplied, result.ChangedFiles, result.RemainingByRule);
		}

		[McpServerTool(Name = "close_analysis")]
		[Description("Close an analysis session and release its workspace from memory.")]
		public static CloseResult CloseAnalysis(
			SessionManager sessions,
			[Description("Session id from open_analysis.")] string sessionId)
		{
			if (sessions.Remove(sessionId, out var session))
			{
				session.Dispose();
				return new CloseResult(true, "Session closed.");
			}

			return new CloseResult(false, $"No open session with id '{sessionId}'.");
		}

		static AnalysisSession Get(SessionManager sessions, string sessionId)
		{
			if (!sessions.TryGet(sessionId, out var session))
			{
				throw new ArgumentException($"No open session with id '{sessionId}'. Call open_analysis first.", nameof(sessionId));
			}

			return session;
		}

		static ImmutableHashSet<string>? ParseIds(string? ruleIds)
		{
			if (string.IsNullOrWhiteSpace(ruleIds))
			{
				return null;
			}

			return ruleIds
				.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToImmutableHashSet(StringComparer.Ordinal);
		}

		static Func<string, bool>? BuildFilter(string? filter)
		{
			if (string.IsNullOrWhiteSpace(filter))
			{
				return null;
			}

			var regex = new Regex(filter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			return regex.IsMatch;
		}

		static bool IsProjectOrSolutionFile(string path)
			=> path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
				|| path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
				|| path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

		static ImmutableArray<DiagnosticInfo> ApplyMinSeverity(ImmutableArray<DiagnosticInfo> diagnostics, string? minSeverity)
		{
			if (string.IsNullOrWhiteSpace(minSeverity))
			{
				return diagnostics;
			}

			var threshold = SeverityRank(minSeverity);
			return diagnostics.Where(d => SeverityRank(d.Severity) >= threshold).ToImmutableArray();
		}

		static int SeverityRank(string severity) => severity.ToUpperInvariant() switch
		{
			"ERROR" => 3,
			"WARNING" => 2,
			"INFO" => 1,
			_ => 0,
		};

		static IReadOnlyList<DiagnosticGroup> GroupDiagnostics(ImmutableArray<DiagnosticInfo> diagnostics, string groupBy)
		{
			switch ((groupBy ?? "rule").ToUpperInvariant())
			{
				case "SEVERITY":
					return diagnostics
						.GroupBy(d => d.Severity, StringComparer.OrdinalIgnoreCase)
						.Select(g => new DiagnosticGroup(g.Key, null, g.Key, null, g.Count(), g.Any(d => d.HasCodeFix)))
						.OrderByDescending(g => SeverityRank(g.Key))
						.ToArray();

				case "PROJECT":
					return diagnostics
						.GroupBy(d => d.ProjectName, StringComparer.OrdinalIgnoreCase)
						.Select(g => new DiagnosticGroup(g.Key, null, null, null, g.Count(), g.Any(d => d.HasCodeFix)))
						.OrderByDescending(g => g.Count)
						.ToArray();

				case "FIXABILITY":
					return diagnostics
						.GroupBy(d => d.HasCodeFix)
						.Select(g => new DiagnosticGroup(g.Key ? "fixable" : "not-fixable", null, null, null, g.Count(), g.Key))
						.OrderByDescending(g => g.HasCodeFix)
						.ToArray();

				default:
					return diagnostics
						.GroupBy(d => d.Id, StringComparer.Ordinal)
						.Select(g =>
						{
							var first = g.First();
							return new DiagnosticGroup(g.Key, first.Title, first.Severity, first.Category, g.Count(), first.HasCodeFix);
						})
						.OrderByDescending(g => g.Count)
						.ThenBy(g => g.Key, StringComparer.Ordinal)
						.ToArray();
			}
		}
	}
}
