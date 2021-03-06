using System.Collections.Immutable;
using System.Threading;

namespace WTG.BulkAnalysis.Core
{
	public sealed class RunContext
	{
		public RunContext(
			ImmutableArray<string> solutionPaths,
			bool applyFixes,
			ImmutableHashSet<string> ruleIds,
			string loadDir,
			ImmutableArray<string> loadList,
			ILog log,
			IReportGenerator? reporter,
			string configuration,
			bool debug,
			CancellationToken cancellationToken)
		{
			SolutionPaths = solutionPaths;
			ApplyFixes = applyFixes;
			RuleIds = ruleIds;
			LoadDir = loadDir;
			LoadList = loadList;
			Log = log;
			Reporter = reporter;
			Configuration = configuration;
			Debug = debug;
			CancellationToken = cancellationToken;
		}

		public ImmutableArray<string> SolutionPaths { get; }
		public bool ApplyFixes { get; }
		public ImmutableHashSet<string> RuleIds { get; }
		public string LoadDir { get; }
		public ImmutableArray<string> LoadList { get; }
		public ILog Log { get; }
		public IReportGenerator? Reporter { get; }
		public string Configuration { get; }
		public bool Debug { get; }
		public CancellationToken CancellationToken { get; }
	}
}
