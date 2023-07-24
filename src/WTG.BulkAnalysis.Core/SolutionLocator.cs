using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace WTG.BulkAnalysis.Core
{
	public static class SolutionLocator
	{
		public static ImmutableArray<string> Locate(string path, Func<string, bool>? solutionFilter)
		{
			var pathToBranch = Path.GetFullPath(path);
			var (solutions, pathToBuildXml) = LocateBuildXml(pathToBranch);

			var solutionPaths =
				from solution in solutions
				let solutionPath = Path.GetFullPath(Path.Combine(pathToBuildXml, solution))
				where IsPathPrefix(solutionPath, pathToBranch)
				select solutionPath;

			if (solutionFilter != null)
			{
				solutionPaths = solutionPaths.Where(solutionFilter);
			}

			return solutionPaths.ToImmutableArray();
		}

		static (IEnumerable<string> solutions, string path) LocateBuildXml(string pathToBranch)
		{
			while (true)
			{
				var buildXmlPath = Path.Combine(pathToBranch, BuildXmlFileName);

				try
				{
					var solutions = Load(buildXmlPath);
					return (solutions, pathToBranch);
				}
				catch (FileNotFoundException)
				{
				}
				catch (DirectoryNotFoundException)
				{
					throw new InvalidConfigurationException("Directory does not exist, '" + pathToBranch + "'.");
				}

				pathToBranch = Path.GetDirectoryName(pathToBranch);

				if (pathToBranch == null)
				{
					throw new InvalidConfigurationException("Build.xml could be found.");
				}
			}
		}

		static IEnumerable<string> Load(string path)
		{
			XElement root;

			try
			{
				root = XElement.Load(path);
			}
			catch (XmlException ex)
			{
				throw new InvalidConfigurationException("Error reading '" + path + "': " + ex.Message);
			}

			// Unfortunately we can't rely on any particular namespace as there isn't much consistency in the use cases.

			if (root.Name.LocalName != "Build")
			{
				throw new InvalidConfigurationException("Error reading '" + path + "': Invalid root element.");
			}

			return
				from solutionsElement in root.Elements()
				where solutionsElement.Name.LocalName == "Solutions"
				from solutionElement in solutionsElement.Elements()
				where solutionElement.Name.LocalName == "Solution"
				let filename = solutionElement.Attribute("Filename")?.Value
				where filename != null
				select filename;
		}

		static bool IsPathPrefix(string path, string prefix)
		{
			if (path.Length <= prefix.Length)
			{
				return false;
			}

			if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			if (path[prefix.Length] != Path.DirectorySeparatorChar)
			{
				return false;
			}

			return true;
		}

		const string BuildXmlFileName = "Build.xml";
	}
}
