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
	abstract class AnalyzerCache
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

		public ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(Project project)
		{
			var language = GetLanguage(project);
			var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

			foreach (var reference in GetReferences(project))
			{
				EnsureSubscribed(reference);

				// Let Roslyn's own analyzer loader resolve the analyzer and its dependencies.
				// It binds the analyzer against the compiler assemblies already loaded in this
				// process and reports unloadable analyzers via AnalyzerLoadFailed instead of
				// throwing - so we never have to reflect over the assembly ourselves.
				var analyzers = language == null
					? reference.GetAnalyzersForAllLanguages()
					: reference.GetAnalyzers(language);

				foreach (var analyzer in analyzers)
				{
					if (analyzerFilter(analyzer))
					{
						builder.Add(analyzer);
					}
				}
			}

			return builder.ToImmutable();
		}

		public ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllCodeFixProviders(Project project)
		{
			return ImmutableDictionary.ToImmutableDictionary(
				from reference in GetReferences(project)
				from provider in GetCodeFixProviders(reference)
				from id in provider.FixableDiagnosticIds
				where diagnosticIds.Contains(id)
				group provider by id into g
				select g,
				x => x.Key,
				x => x.ToImmutableList());
		}

		protected static IAnalyzerAssemblyLoader GetLoader(Project project)
		{
			// Reuse the assembly loader the workspace configured for this project's analyzer
			// references, so assemblies we load from an alternate location share the same
			// dependency-resolution behaviour.
			foreach (var reference in project.AnalyzerReferences)
			{
				if (reference is AnalyzerFileReference fileReference)
				{
					return fileReference.AssemblyLoader;
				}
			}

			return FallbackAssemblyLoader.Instance;
		}

		protected AnalyzerFileReference GetOrCreateReference(string path, IAnalyzerAssemblyLoader loader)
			=> referenceCache.GetOrAdd(path, p => new AnalyzerFileReference(p, loader));

		protected abstract IEnumerable<AnalyzerFileReference> GetReferences(Project project);
		protected abstract string? GetLanguage(Project project);

		ImmutableArray<CodeFixProvider> GetCodeFixProviders(AnalyzerFileReference reference)
			=> providerLookup.GetOrAdd(reference.FullPath, key => LoadCodeFixProviders(reference));

		ImmutableArray<CodeFixProvider> LoadCodeFixProviders(AnalyzerFileReference reference)
		{
			var builder = ImmutableArray.CreateBuilder<CodeFixProvider>();

			// Roslyn has no discovery API for code fix providers, so we reflect over the
			// assembly. We use the reference's own GetAssembly() rather than a fresh
			// Assembly.LoadFile so the code fixes bind against the same, already-loaded copy
			// Roslyn used for the analyzers.
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

		Type[] GetLoadableTypes(AnalyzerFileReference reference)
		{
			Assembly assembly;

			try
			{
				assembly = reference.GetAssembly();
			}
			catch (Exception ex)
			{
				log.WriteFormatted($"  - Unable to load code fixes from '{reference.FullPath}': {ex.Message}", LogLevel.Warning);
				return Array.Empty<Type>();
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

		readonly ImmutableHashSet<string> diagnosticIds;
		readonly ILog log;
		readonly Predicate<DiagnosticAnalyzer> analyzerFilter;
		readonly Predicate<CodeFixProvider> providerFilter;
		readonly ConcurrentDictionary<string, ImmutableArray<CodeFixProvider>> providerLookup;
		readonly ConcurrentDictionary<string, AnalyzerFileReference> referenceCache;
		readonly HashSet<AnalyzerFileReference> subscribed;

		sealed class Implicit : AnalyzerCache
		{
			public Implicit(ImmutableHashSet<string> diagnosticIds, string loadDir, ILog log)
				: base(diagnosticIds, log)
			{
				this.loadDir = loadDir;
			}

			protected override string? GetLanguage(Project project) => project.Language;

			protected override IEnumerable<AnalyzerFileReference> GetReferences(Project project)
			{
				if (string.IsNullOrEmpty(loadDir))
				{
					return project.AnalyzerReferences.OfType<AnalyzerFileReference>();
				}

				return Remap(project, loadDir);
			}

			IEnumerable<AnalyzerFileReference> Remap(Project project, string loadDir)
			{
				var loader = GetLoader(project);

				foreach (var reference in project.AnalyzerReferences)
				{
					if (string.IsNullOrEmpty(reference.FullPath))
					{
						continue;
					}

					var proposal = Path.Combine(loadDir, Path.GetFileName(reference.FullPath));

					if (File.Exists(proposal))
					{
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
				paths = PrefixPaths(loadDir, loadList).ToImmutableArray();
			}

			// No project context, so ask each reference for analyzers across all languages.
			protected override string? GetLanguage(Project project) => null;

			protected override IEnumerable<AnalyzerFileReference> GetReferences(Project project)
			{
				var loader = GetLoader(project);
				return paths.Select(path => GetOrCreateReference(path, loader));
			}

			static IEnumerable<string> PrefixPaths(string loadDir, ImmutableArray<string> loadList)
			{
				var paths = loadList.AsEnumerable();

				if (loadDir != null)
				{
					paths = paths.Select(x => Path.Combine(loadDir, x));
				}

				return paths;
			}

			readonly ImmutableArray<string> paths;
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
