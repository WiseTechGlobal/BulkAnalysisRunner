using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.TeamFoundation.Client;
using WTG.DevTools.Common;
using VC = Microsoft.TeamFoundation.VersionControl.Client;

namespace WTG.BulkAnalysis.Core
{
	public static class Processor
	{
		public static async Task ProcessAsync(RunContext context)
		{
			context.Log.WriteLine("Rule IDs:");

			foreach (var rule in context.RuleIds)
			{
				context.Log.WriteFormatted($"  - {rule}");
			}

			context.Log.WriteLine();

			var solutionPaths = GetSolutionPaths(context);
			var counter = 0;
			var numSolutions = solutionPaths.Count;
			var cache = new AnalyzerCache(context.RuleIds);

			using (var tfs = new TfsTeamProjectCollection(context.TfsServer))
			{
				tfs.EnsureAuthenticated();
				var vcs = tfs.GetService<VC.VersionControlServer>();

				var vcsWorkspace = vcs.TryGetWorkspace(context.PathToBranch);

				if (vcsWorkspace == null)
				{
					throw new InvalidConfigurationException($"No workspace mapping found for '{context.PathToBranch}'.");
				}

				foreach (var solutionPath in solutionPaths)
				{
					context.Log.WriteFormatted($"* Solution: {Path.GetFileName(solutionPath)} ({counter + 1}/{numSolutions})...");

					using (var workspace = MSBuildWorkspace.Create())
					{
						var solutionFullPath = Path.GetFullPath(Path.Combine(context.PathToBranch, solutionPath));
						var solution = await workspace.OpenSolutionAsync(solutionFullPath, context.CancellationToken).ConfigureAwait(false);
						var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToArray();

						if (csharpProjects.Length == 0)
						{
							context.Log.WriteLine("  - No C# projects! Moving on to next solution...");
						}
						else
						{
							context.Log.WriteFormatted($"  - Found {csharpProjects.Length} projects.");

							var processor = new SolutionProcessor(context, cache, workspace, vcsWorkspace.PendEdit);
							await processor.ProcessSolutionAsync().ConfigureAwait(false);
						}
					}

					counter++;
				}
			}
		}

		static IList<string> GetSolutionPaths(RunContext context)
		{
			var buildXml = BuildXmlFile.Deserialize(Path.Combine(context.PathToBranch, BuildXmlFile.FileName));
			var solutionPaths = buildXml.Solutions.Select(x => x.Filename);

			if (context.SolutionFilter != null)
			{
				solutionPaths = solutionPaths.Where(context.SolutionFilter);
			}

			return solutionPaths.ToList();
		}
	}
}
