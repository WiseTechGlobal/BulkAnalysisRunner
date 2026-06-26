using System.Collections.Immutable;

namespace WTG.BulkAnalysis.Core
{
	public sealed class FixResult
	{
		public FixResult(
			int operationsApplied,
			ImmutableArray<string> changedFiles,
			ImmutableDictionary<string, int> remainingByRule)
		{
			OperationsApplied = operationsApplied;
			ChangedFiles = changedFiles;
			RemainingByRule = remainingByRule;
		}

		/// <summary>Number of fix-all operations applied to the workspace (and written to disk).</summary>
		public int OperationsApplied { get; }

		/// <summary>Absolute paths of the source files that were changed on disk.</summary>
		public ImmutableArray<string> ChangedFiles { get; }

		/// <summary>For each requested rule id, the number of diagnostics still remaining after fixing.</summary>
		public ImmutableDictionary<string, int> RemainingByRule { get; }
	}
}
