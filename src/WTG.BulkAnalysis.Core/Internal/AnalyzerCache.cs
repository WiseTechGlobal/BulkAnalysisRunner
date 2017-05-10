using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WTG.BulkAnalysis.Core
{
	sealed class AnalyzerCache
	{
		public AnalyzerCache(ImmutableHashSet<string> diagnosticIds)
		{
			analyzerFilter = a => a.SupportedDiagnostics.Any(x => diagnosticIds.Contains(x.Id));
			providerFilter = p => p.FixableDiagnosticIds.Any(diagnosticIds.Contains);
			analyzerLookup = new ConcurrentDictionary<string, ImmutableArray<DiagnosticAnalyzer>>();
			providerLookup = new ConcurrentDictionary<string, ImmutableArray<CodeFixProvider>>();
		}

		public ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string assemblyName) => GetCached(assemblyName, analyzerLookup, analyzerFilter);
		public ImmutableArray<CodeFixProvider> GetCodeFixProviders(string assemblyName) => GetCached(assemblyName, providerLookup, providerFilter);

		static ImmutableArray<T> GetCached<T>(string assemblyName, ConcurrentDictionary<string, ImmutableArray<T>> lookup, Predicate<T> filter)
		{
			if (!lookup.TryGetValue(assemblyName, out var result))
			{
				result = Get(assemblyName, filter);
				result = lookup.GetOrAdd(assemblyName, result);
			}

			return result;
		}

		static ImmutableArray<T> Get<T>(string assemblyName, Predicate<T> filter)
		{
			var assembly = Assembly.LoadFile(assemblyName);

			ImmutableArray<T>.Builder builder = null;

			foreach (var type in assembly.GetTypes())
			{
				if (!type.IsAbstract && type.IsSubclassOf(typeof(T)))
				{
					var instance = (T)Activator.CreateInstance(type);

					if (filter(instance))
					{
						if (builder == null)
						{
							builder = ImmutableArray.CreateBuilder<T>();
						}

						builder.Add(instance);
					}
				}
			}

			return builder?.ToImmutable() ?? ImmutableArray<T>.Empty;
		}

		readonly Predicate<DiagnosticAnalyzer> analyzerFilter;
		readonly Predicate<CodeFixProvider> providerFilter;
		readonly ConcurrentDictionary<string, ImmutableArray<DiagnosticAnalyzer>> analyzerLookup;
		readonly ConcurrentDictionary<string, ImmutableArray<CodeFixProvider>> providerLookup;
	}
}
