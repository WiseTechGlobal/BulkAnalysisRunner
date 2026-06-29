using System;
using System.IO;
using System.Reflection;

namespace WTG.BulkAnalysis.Core
{
	/// <summary>
	/// Analyzer assemblies are loaded with <see cref="Assembly.LoadFile(string)"/>, which does not probe
	/// for an assembly's dependencies. This resolves them from the requesting assembly's own directory so
	/// analyzers that ship a separate dependency (e.g. a *.Utils.dll) can run instead of throwing at
	/// execution time (which surfaces as AD0001). Call <see cref="Register"/> once at host start-up.
	/// </summary>
	public static class AnalyzerAssemblyResolver
	{
		public static void Register()
		{
			if (registered)
			{
				return;
			}

			registered = true;
			AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
		}

		static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
		{
			var requester = args.RequestingAssembly;

			if (requester == null || string.IsNullOrEmpty(requester.Location))
			{
				return null;
			}

			var directory = Path.GetDirectoryName(requester.Location);
			var name = new AssemblyName(args.Name).Name;

			if (directory == null || name == null)
			{
				return null;
			}

			var candidate = Path.Combine(directory, name + ".dll");
			return File.Exists(candidate) ? Assembly.LoadFile(candidate) : null;
		}

		static bool registered;
	}
}
