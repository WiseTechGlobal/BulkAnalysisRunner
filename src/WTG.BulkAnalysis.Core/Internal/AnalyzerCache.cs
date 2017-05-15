using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WTG.BulkAnalysis.Core
{
	abstract class AnalyzerCache
	{
		public static AnalyzerCache Create(ImmutableHashSet<string> diagnosticIds, ImmutableArray<string> loadList)
		{
			if (loadList.Length > 0)
			{
				return new Explicit(diagnosticIds, loadList);
			}
			else
			{
				return new Implicit(diagnosticIds);
			}
		}

		public abstract ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(Solution solution);
		public abstract ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllCodeFixProviders(Solution solution);

		static IEnumerable<T> Get<T>(string assemblyPath, Predicate<T> filter)
		{
			var assembly = Assembly.LoadFile(assemblyPath);

			foreach (var type in assembly.GetTypes())
			{
				if (!type.IsAbstract && type.IsSubclassOf(typeof(T)))
				{
					var instance = (T)Activator.CreateInstance(type);

					if (filter(instance))
					{
						yield return instance;
					}
				}
			}
		}

		sealed class Implicit : AnalyzerCache
		{
			public Implicit(ImmutableHashSet<string> diagnosticIds)
			{
				analyzerFilter = a => a.SupportedDiagnostics.Any(x => diagnosticIds.Contains(x.Id));
				providerFilter = p => p.FixableDiagnosticIds.Any(diagnosticIds.Contains);
				analyzerLookup = new ConcurrentDictionary<string, ImmutableArray<DiagnosticAnalyzer>>();
				providerLookup = new ConcurrentDictionary<string, ImmutableArray<CodeFixProvider>>();
			}

			public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(Solution solution)
			{
				return ImmutableArray.CreateRange(
					from analyzerRef in GetAnalyzerRefs(solution)
					from analyzer in GetAnalyzers(analyzerRef)
					select analyzer);
			}

			public override ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllCodeFixProviders(Solution solution)
			{
				return ImmutableDictionary.ToImmutableDictionary(
					from analyzerRef in GetAnalyzerRefs(solution)
					from codeFixProvider in GetCodeFixProviders(analyzerRef)
					from diagnosticId in codeFixProvider.FixableDiagnosticIds
					group codeFixProvider by diagnosticId into g
					select g,
					x => x.Key,
					x => x.ToImmutableList());
			}

			ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string assemblyName) => GetCached(assemblyName, analyzerLookup, analyzerFilter);
			ImmutableArray<CodeFixProvider> GetCodeFixProviders(string assemblyName) => GetCached(assemblyName, providerLookup, providerFilter);

			static ImmutableArray<T> GetCached<T>(string assemblyName, ConcurrentDictionary<string, ImmutableArray<T>> lookup, Predicate<T> filter)
			{
				if (!lookup.TryGetValue(assemblyName, out var result))
				{
					result = Get(assemblyName, filter).ToImmutableArray();
					result = lookup.GetOrAdd(assemblyName, result);
				}

				return result;
			}

			static IEnumerable<string> GetAnalyzerRefs(Solution solution)
			{
				return solution
					.Projects
					.SelectMany(p => p.AnalyzerReferences)
					.Select(a => a.FullPath)
					.Distinct();
			}

			readonly Predicate<DiagnosticAnalyzer> analyzerFilter;
			readonly Predicate<CodeFixProvider> providerFilter;
			readonly ConcurrentDictionary<string, ImmutableArray<DiagnosticAnalyzer>> analyzerLookup;
			readonly ConcurrentDictionary<string, ImmutableArray<CodeFixProvider>> providerLookup;
		}

		sealed class Explicit : AnalyzerCache
		{
			public Explicit(ImmutableHashSet<string> diagnosticIds, ImmutableArray<string> loadList)
			{
				Predicate<DiagnosticAnalyzer> analyzerFilter = a => a.SupportedDiagnostics.Any(x => diagnosticIds.Contains(x.Id));
				Predicate<CodeFixProvider> providerFilter = p => p.FixableDiagnosticIds.Any(diagnosticIds.Contains);

				analyzers = loadList.SelectMany(x => Get(x, analyzerFilter)).ToImmutableArray();

				providers = ImmutableDictionary.ToImmutableDictionary(
					from path in loadList
					from provider in Get(path, providerFilter)
					from id in provider.FixableDiagnosticIds
					where diagnosticIds.Contains(id)
					group provider by id into g
					select g,
					x => x.Key,
					x => x.ToImmutableList());
			}

			public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(Solution solution) => analyzers;
			public override ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllCodeFixProviders(Solution solution) => providers;

			readonly ImmutableArray<DiagnosticAnalyzer> analyzers;
			readonly ImmutableDictionary<string, ImmutableList<CodeFixProvider>> providers;
		}
	}
}
