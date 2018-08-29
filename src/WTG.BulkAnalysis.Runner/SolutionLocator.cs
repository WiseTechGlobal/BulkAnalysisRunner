using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using WTG.BulkAnalysis.Core;
using WTG.DevTools.Common;

namespace WTG.BulkAnalysis.Runner
{
	static class SolutionLocator
	{
		public static ImmutableArray<string> Locate(string path, Func<string, bool> solutionFilter)
		{
			var pathToBranch = Path.GetFullPath(path);
			var (buildXml, pathToBuildXml) = LocateBuildXml(pathToBranch);

			var solutionPaths =
				from solution in buildXml.Solutions
				let solutionPath = Path.GetFullPath(Path.Combine(pathToBuildXml, solution.Filename))
				where solutionPath.StartsWith(pathToBranch, StringComparison.OrdinalIgnoreCase)
				select solutionPath;

			if (solutionFilter != null)
			{
				solutionPaths = solutionPaths.Where(solutionFilter);
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
