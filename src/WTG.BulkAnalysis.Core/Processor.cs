using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using WTG.DevTools.Common;

namespace WTG.BulkAnalysis.Core
{
	public static class Processor
	{
		public static Task ProcessAsync(RunContext context)
		{
			return ProcessAsync(context, GetSolutionPaths(context));
		}

		static async Task ProcessAsync(RunContext context, ImmutableArray<string> solutionPaths)
		{
			context.Log.WriteLine("Rule IDs:");

			foreach (var rule in context.RuleIds)
			{
				context.Log.WriteFormatted($"  - {rule}");
			}

			context.Log.WriteLine();

			var counter = 0;
			var numSolutions = solutionPaths.Length;
			var cache = AnalyzerCache.Create(context.RuleIds, context.LoadList);

			foreach (var solutionPath in solutionPaths)
			{
				context.Log.WriteFormatted($"* Solution: {Path.GetFileName(solutionPath)} ({counter + 1}/{numSolutions})...");

				using (var workspace = MSBuildWorkspace.Create())
				{
					var solution = await workspace.OpenSolutionAsync(solutionPath, context.CancellationToken).ConfigureAwait(false);
					var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToArray();

					if (csharpProjects.Length == 0)
					{
						context.Log.WriteLine("  - No C# projects! Moving on to next solution...");
					}
					else
					{
						context.Log.WriteFormatted($"  - Found {csharpProjects.Length} projects.");

						var processor = new SolutionProcessor(context, cache, workspace);
						await processor.ProcessSolutionAsync().ConfigureAwait(false);
					}
				}

				counter++;
			}
		}

		static ImmutableArray<string> GetSolutionPaths(RunContext context)
		{
			var pathToBranch = Path.GetFullPath(context.PathToBranch);
			var (buildXml, path) = LocateBuildXml(pathToBranch);

			var solutionPaths =
				from solution in buildXml.Solutions
				let solutionPath = Path.GetFullPath(Path.Combine(path, solution.Filename))
				where solutionPath.StartsWith(pathToBranch, StringComparison.OrdinalIgnoreCase)
				select solutionPath;

			if (context.SolutionFilter != null)
			{
				solutionPaths = solutionPaths.Where(context.SolutionFilter);
			}

			return solutionPaths.ToImmutableArray();
		}

		static (BuildXml buildXml, string path) LocateBuildXml(string pathToBranch)
		{
			while (true)
			{
				var buildXmlPath = Path.Combine(pathToBranch, BuildXmlFile.FileName);

				try
				{
					var buildXml = BuildXmlFile.Deserialize(buildXmlPath);
					return (buildXml, pathToBranch);
				}
				catch (FileNotFoundException)
				{
				}

				pathToBranch = Path.GetDirectoryName(pathToBranch);

				if (pathToBranch == null)
				{
					throw new InvalidConfigurationException("Build.xml could be found.");
				}
			}
		}
	}
}
