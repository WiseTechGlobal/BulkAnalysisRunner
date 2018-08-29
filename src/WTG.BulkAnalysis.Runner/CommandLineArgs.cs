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
		public CommandLineArgs(
			string path,
			IEnumerable<string> ruleIDs,
			string filter,
			bool fix,
			string report,
			string loadDir,
			IEnumerable<string> loadList,
			bool pause)
		{
			Path = path;
			RuleIDs = ruleIDs;
			Filter = filter;
			Fix = fix;
			Report = report;
			LoadDir = loadDir;
			LoadList = loadList;
			Pause = pause;
		}

		[Value(0, Required = true, Hidden = true)]
		public string Path { get; }

		[Value(1, Required = true, Hidden = true)]
		public IEnumerable<string> RuleIDs { get; }

		[Option("filter", Required = false, HelpText = "When specified, only solutions matching this regular expression will be processed.")]
		public string Filter { get; }

		[Option("fix", Required = false, HelpText = "Attempt to apply code fixes.")]
		public bool Fix { get; }

		[Option("report", Required = false, HelpText = "Specifies a file where detected issues should be recorded.")]
		public string Report { get; }

		[Option("loadDir", Required = false, HelpText = "Specifies a directory where analyzer assemblies can be found.")]
		public string LoadDir { get; }

		[Option("load", Required = false, HelpText = "Specifies the assemblies to load and search for analyzers and code fixes. If none is specified, then it will attempt to use whatever the project specifies.", Separator = ';')]
		public IEnumerable<string> LoadList { get; }

		[Option("pause", Required = false, Hidden = true)]
		public bool Pause { get; }
	}
}
