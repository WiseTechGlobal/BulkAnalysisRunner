using System;
using System.Collections.Immutable;
using System.Threading;

namespace WTG.BulkAnalysis.Core
{
	public sealed class RunContext
	{
		public RunContext(
			string pathToBranch,
			Uri tfsServer,
			Func<string, bool> solutionFilter,
			bool applyFixes,
			ImmutableHashSet<string> ruleIds,
			ILog log,
			IReportGenerator reporter,
			CancellationToken cancellationToken)
		{
			PathToBranch = pathToBranch;
			TfsServer = tfsServer;
			SolutionFilter = solutionFilter;
			ApplyFixes = applyFixes;
			RuleIds = ruleIds;
			Log = log;
			Reporter = reporter;
			CancellationToken = cancellationToken;
		}

		public string PathToBranch { get; }
		public Uri TfsServer { get; }
		public Func<string, bool> SolutionFilter { get; }
		public bool ApplyFixes { get; }
		public ImmutableHashSet<string> RuleIds { get; }
		public ILog Log { get; }
		public IReportGenerator Reporter { get; }
		public CancellationToken CancellationToken { get; }
	}
}
