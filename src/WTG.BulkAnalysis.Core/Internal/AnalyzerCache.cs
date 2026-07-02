using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WTG.BulkAnalysis.Core
{
	public abstract class AnalyzerCache
	{
		protected AnalyzerCache(ImmutableHashSet<string> diagnosticIds, ILog log)
		{
			this.diagnosticIds = diagnosticIds;
			this.log = log;
			analyzerFilter = a => a.SupportedDiagnostics.Any(x => diagnosticIds.Contains(x.Id));
			providerFilter = p => p.FixableDiagnosticIds.Any(diagnosticIds.Contains);
			providerLookup = new ConcurrentDictionary<string, ImmutableArray<CodeFixProvider>>();
			referenceCache = new ConcurrentDictionary<string, AnalyzerFileReference>(StringComparer.OrdinalIgnoreCase);
			subscribed = new HashSet<AnalyzerFileReference>();
		}

		readonly ImmutableHashSet<string> diagnosticIds;
		readonly ILog log;
		readonly Predicate<DiagnosticAnalyzer> analyzerFilter;
		readonly Predicate<CodeFixProvider> providerFilter;
		readonly ConcurrentDictionary<string, ImmutableArray<CodeFixProvider>> providerLookup;
		readonly ConcurrentDictionary<string, AnalyzerFileReference> referenceCache;
		readonly HashSet<AnalyzerFileReference> subscribed;

		public static AnalyzerCache Create(ImmutableHashSet<string> diagnosticIds, string loadDir, ImmutableArray<string> loadList, ILog log)
		{
			if (loadList.Length > 0)
			{
				return new Explicit(diagnosticIds, loadDir, loadList, log);
			}
			else
			{
				return new Implicit(diagnosticIds, loadDir, log);
			}
		}

		public abstract ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(Project project);
		public abstract ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllCodeFixProviders(Project project);

		protected ImmutableArray<DiagnosticAnalyzer> CollectAnalyzers(IEnumerable<AnalyzerFileReference> references, string? language)
		{
			var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

			foreach (var reference in references)
			{
				EnsureSubscribed(reference);

				var analyzers = language is not null
					? reference.GetAnalyzers(language)
					: reference.GetAnalyzersForAllLanguages();

				builder.AddRange(analyzers.Where(a => analyzerFilter(a)));
			}

			return builder.ToImmutable();
		}

		protected ImmutableDictionary<string, ImmutableList<CodeFixProvider>> CollectCodeFixProviders(IEnumerable<AnalyzerFileReference> references)
		{
			return ImmutableDictionary.ToImmutableDictionary(
				from reference in references
				from provider in GetCodeFixProviders(reference)
				from id in provider.FixableDiagnosticIds
				where diagnosticIds.Contains(id)
				group provider by id into g
				select g,
				x => x.Key,
				x => x.ToImmutableList());
		}

		protected AnalyzerFileReference GetOrCreateReference(string path, IAnalyzerAssemblyLoader loader)
			=> referenceCache.GetOrAdd(path, p => new AnalyzerFileReference(p, loader));

		ImmutableArray<CodeFixProvider> GetCodeFixProviders(AnalyzerFileReference reference)
			=> providerLookup.GetOrAdd(reference.FullPath, key => LoadCodeFixProviders(reference));

		ImmutableArray<CodeFixProvider> LoadCodeFixProviders(AnalyzerFileReference reference)
		{
			var builder = ImmutableArray.CreateBuilder<CodeFixProvider>();

			foreach (var type in GetLoadableTypes(reference))
			{
				if (!type.IsAbstract && type.IsSubclassOf(typeof(CodeFixProvider)))
				{
					var provider = (CodeFixProvider)Activator.CreateInstance(type);

					if (providerFilter(provider))
					{
						builder.Add(provider);
					}
				}
			}

			return builder.ToImmutable();
		}

		IEnumerable<Type> GetLoadableTypes(AnalyzerFileReference reference)
		{
			Assembly assembly;

			try
			{
				assembly = reference.GetAssembly();
			}
			catch (Exception ex)
			{
				log.WriteFormatted($"  - Unable to load code fixes from '{reference.FullPath}': {ex.Message}", LogLevel.Warning);
				return [];
			}

			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				// A provider whose dependencies can't be resolved appears as a null entry here;
				// keep the types that did load. This mirrors how Roslyn's AnalyzerFileReference
				// tolerates partial load failures when enumerating analyzers.
				return ex.Types.Where(t => t != null).ToArray()!;
			}
		}

		void EnsureSubscribed(AnalyzerFileReference reference)
		{
			if (subscribed.Add(reference))
			{
				reference.AnalyzerLoadFailed += OnAnalyzerLoadFailed;
			}
		}

		void OnAnalyzerLoadFailed(object? sender, AnalyzerLoadFailureEventArgs e)
		{
			var path = (sender as AnalyzerFileReference)?.FullPath;
			var detail = string.IsNullOrEmpty(e.Message) ? e.Exception?.Message : e.Message;
			var suffix = string.IsNullOrEmpty(detail) ? string.Empty : $": {detail}";
			log.WriteFormatted($"  - Skipping an analyzer in '{path}' ({e.ErrorCode}){suffix}", LogLevel.Warning);
		}

		sealed class Implicit : AnalyzerCache
		{
			public Implicit(ImmutableHashSet<string> diagnosticIds, string loadDir, ILog log)
				: base(diagnosticIds, log)
			{
				this.loadDir = loadDir;
			}

			public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(Project project)
				=> CollectAnalyzers(GetReferences(project), project.Language);

			public override ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllCodeFixProviders(Project project)
				=> CollectCodeFixProviders(GetReferences(project));

			IEnumerable<AnalyzerFileReference> GetReferences(Project project)
			{
				if (string.IsNullOrEmpty(loadDir))
				{
					return project.AnalyzerReferences.OfType<AnalyzerFileReference>();
				}

				return Remap(project, loadDir);
			}

			IEnumerable<AnalyzerFileReference> Remap(Project project, string loadDir)
			{
				foreach (var reference in project.AnalyzerReferences.OfType<AnalyzerFileReference>())
				{
					if (string.IsNullOrEmpty(reference.FullPath))
					{
						continue;
					}

					var proposal = Path.Combine(loadDir, Path.GetFileName(reference.FullPath));

					if (File.Exists(proposal))
					{
						var loader = reference.AssemblyLoader;
						loader.AddDependencyLocation(proposal);
						yield return GetOrCreateReference(proposal, loader);
					}
				}
			}

			readonly string loadDir;
		}

		sealed class Explicit : AnalyzerCache
		{
			public Explicit(ImmutableHashSet<string> diagnosticIds, string loadDir, ImmutableArray<string> loadList, ILog log)
				: base(diagnosticIds, log)
			{
				// The explicit load list stands alone rather than tracking a project, so resolve it up
				// front. There is no project reference to borrow a loader from, and no project language,
				// so use the fallback loader and ask each reference for analyzers across all languages.
				var references = PrefixPaths(loadDir, loadList)
					.Select(path => GetOrCreateReference(path, FallbackAssemblyLoader.Instance))
					.ToImmutableArray();

				analyzers = CollectAnalyzers(references, language: null);
				providers = CollectCodeFixProviders(references);
			}

			public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(Project project) => analyzers;
			public override ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllCodeFixProviders(Project project) => providers;

			static IEnumerable<string> PrefixPaths(string loadDir, ImmutableArray<string> loadList)
			{
				var paths = loadList.AsEnumerable();

				if (loadDir != null)
				{
					paths = paths.Select(x => Path.Combine(loadDir, x));
				}

				return paths;
			}

			readonly ImmutableArray<DiagnosticAnalyzer> analyzers;
			readonly ImmutableDictionary<string, ImmutableList<CodeFixProvider>> providers;
		}

		sealed class FallbackAssemblyLoader : IAnalyzerAssemblyLoader
		{
			public static readonly FallbackAssemblyLoader Instance = new FallbackAssemblyLoader();

			public void AddDependencyLocation(string fullPath)
			{
			}

			public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
		}
	}
}
