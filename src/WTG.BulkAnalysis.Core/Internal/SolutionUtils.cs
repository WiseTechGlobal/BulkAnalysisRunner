using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WTG.BulkAnalysis.Core
{
	static class SolutionUtils
	{
		public static ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(this AnalyzerCache cache, Solution solution)
		{
			return ImmutableArray.CreateRange(
				from analyzerRef in GetAnalyzerRefs(solution)
				from analyzer in cache.GetAnalyzers(analyzerRef)
				select analyzer);
		}

		public static ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllCodeFixProviders(this AnalyzerCache cache, Solution solution)
		{
			return ImmutableDictionary.ToImmutableDictionary(
				from analyzerRef in GetAnalyzerRefs(solution)
				from codeFixProvider in cache.GetCodeFixProviders(analyzerRef)
				from diagnosticId in codeFixProvider.FixableDiagnosticIds
				group codeFixProvider by diagnosticId into g
				select g,
				x => x.Key,
				x => x.ToImmutableList());
		}

		static IEnumerable<string> GetAnalyzerRefs(Solution solution)
		{
			return solution
				.Projects
				.SelectMany(p => p.AnalyzerReferences)
				.Select(a => a.FullPath)
				.Distinct();
		}
	}
}
