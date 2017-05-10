using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace WTG.BulkAnalysis.Core
{
	public interface IReportGenerator
	{
		void Report(Solution solution, ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> diagnostics);
	}
}
