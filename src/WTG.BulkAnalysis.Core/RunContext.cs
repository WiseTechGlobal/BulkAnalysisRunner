using System.Collections.Immutable;
using System.Threading;

namespace WTG.BulkAnalysis.Core
{
	public sealed class RunContext
	{
		public RunContext(
			ImmutableArray<string> solutionPaths,
			IVersionControl versionControl,
			bool applyFixes,
			ImmutableHashSet<string> ruleIds,
			string loadDir,
			ImmutableArray<string> loadList,
			ILog log,
			IReportGenerator reporter,
			bool debug,
			CancellationToken cancellationToken)
		{
			SolutionPaths = solutionPaths;
			VersionControl = versionControl;
			ApplyFixes = applyFixes;
			RuleIds = ruleIds;
			LoadDir = loadDir;
			LoadList = loadList;
			Log = log;
			Reporter = reporter;
			Debug = debug;
			CancellationToken = cancellationToken;
		}

		public ImmutableArray<string> SolutionPaths { get; }
		public IVersionControl VersionControl { get; }
		public bool ApplyFixes { get; }
		public ImmutableHashSet<string> RuleIds { get; }
		public string LoadDir { get; }
		public ImmutableArray<string> LoadList { get; }
		public ILog Log { get; }
		public IReportGenerator Reporter { get; }
		public bool Debug { get; }
		public CancellationToken CancellationToken { get; }
	}
}
