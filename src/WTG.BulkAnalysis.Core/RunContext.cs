using System;
using System.Collections.Immutable;
using System.Threading;

namespace WTG.BulkAnalysis.Core
{
	public sealed class RunContext
	{
		public RunContext(
			string pathToBranch,
			IVersionControl versionControl,
			Func<string, bool> solutionFilter,
			bool applyFixes,
			ImmutableHashSet<string> ruleIds,
			string loadDir,
			ImmutableArray<string> loadList,
			ILog log,
			IReportGenerator reporter,
			CancellationToken cancellationToken)
		{
			PathToBranch = pathToBranch;
			VersionControl = versionControl;
			SolutionFilter = solutionFilter;
			ApplyFixes = applyFixes;
			RuleIds = ruleIds;
			LoadDir = loadDir;
			LoadList = loadList;
			Log = log;
			Reporter = reporter;
			CancellationToken = cancellationToken;
		}

		public string PathToBranch { get; }
		public IVersionControl VersionControl { get; }
		public Func<string, bool> SolutionFilter { get; }
		public bool ApplyFixes { get; }
		public ImmutableHashSet<string> RuleIds { get; }
		public string LoadDir { get; }
		public ImmutableArray<string> LoadList { get; }
		public ILog Log { get; }
		public IReportGenerator Reporter { get; }
		public CancellationToken CancellationToken { get; }
	}
}
