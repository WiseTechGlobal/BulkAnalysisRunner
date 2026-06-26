using System.Collections.Immutable;

namespace WTG.BulkAnalysis.Core
{
	public sealed class AnalysisSessionOptions
	{
		public AnalysisSessionOptions(
			string path,
			string configuration,
			string? loadDir,
			ImmutableArray<string> loadList)
		{
			Path = path;
			Configuration = configuration;
			LoadDir = loadDir;
			LoadList = loadList.IsDefault ? ImmutableArray<string>.Empty : loadList;
		}

		/// <summary>Path to the solution (.sln/.slnx) or project (.csproj) to open.</summary>
		public string Path { get; }

		/// <summary>The build configuration to load, e.g. "Debug" or "Release".</summary>
		public string Configuration { get; }

		/// <summary>Optional directory to load analyzer/code-fix assemblies from instead of the project's references.</summary>
		public string? LoadDir { get; }

		/// <summary>Optional explicit list of analyzer assemblies to load. When empty, the project's references are used.</summary>
		public ImmutableArray<string> LoadList { get; }
	}
}
