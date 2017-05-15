using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommandLine;
using CommandLine.Text;

[assembly: AssemblyUsage("  BulkAnalysisRunner <options> <path-to-branch> <rule-id> [<rule-id> ...]")]

namespace WTG.BulkAnalysis.Runner
{
	[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses")]
	sealed class CommandLineArgs
	{
		[Value(0, Required = true, Hidden = true)]
		public string Path { get; set; }

		[Value(1, Required = true, Hidden = true)]
		public IEnumerable<string> RuleIDs { get; set; }

		[Option("filter", Required = false, HelpText = "When specified, only solutions matching this filter will be processed.")]
		public string Filter { get; set; }

		[Option("fix", Required = false, HelpText = "Attempt to apply code fixes.")]
		public bool Fix { get; set; }

		[Option("report", Required = false, HelpText = "Specifies a file where detected issues should be recorded.")]
		public string Report { get; set; }

		[Option("tfs", Required = false, Default = "http://tfs.wtg.zone:8080/tfs/CargoWise", HelpText = "Specifies the tfs project collection uri.")]
		public string ServerUrl { get; set; }

		[Option("load", Required = false, HelpText = "Specifies the assemblies to load and search for analyzers and code fixes. If none is specified, then it will attempt to use whatever the project specifies.", Separator = ';')]
		public IEnumerable<string> LoadList { get; set; }
	}
}
