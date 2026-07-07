using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.Test;

sealed class AnalyzerCacheTest
{
	[Test]
	public void LoadsAnalyzersFromAssembly()
	{
		var cache = CreateCache(SampleAnalyzer.DiagnosticId);

		using var workspace = new AdhocWorkspace();
		var project = workspace.AddProject("Sample", LanguageNames.CSharp);

		var analyzers = cache.GetAnalyzers(project);

		Assert.That(analyzers.Select(a => a.GetType()), Has.Member(typeof(SampleAnalyzer)));
	}

	[Test]
	public void OnlyReturnsAnalyzersForTheRequestedDiagnosticIds()
	{
		var cache = CreateCache("SomeOtherIdThatNothingSupports");

		using var workspace = new AdhocWorkspace();
		var project = workspace.AddProject("Sample", LanguageNames.CSharp);

		var analyzers = cache.GetAnalyzers(project);

		Assert.That(analyzers.Select(a => a.GetType()), Has.No.Member(typeof(SampleAnalyzer)));
	}

	static AnalyzerCache CreateCache(string diagnosticId)
	{
		return AnalyzerCache.Create(
			ImmutableHashSet.Create(diagnosticId),
			loadDir: string.Empty,
			loadList: ImmutableArray.Create(typeof(AnalyzerCacheTest).Assembly.Location),
			log: NullLog.Instance);
	}

	sealed class NullLog : ILog
	{
		public static readonly NullLog Instance = new NullLog();

		public void WriteFormatted(FormattableString message, LogLevel level = LogLevel.Normal)
		{
		}

		public void WriteLine(string message, LogLevel level = LogLevel.Normal)
		{
		}

		public void WriteLine()
		{
		}
	}
}

[DiagnosticAnalyzer(LanguageNames.CSharp)]
sealed class SampleAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "WTGTEST01";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

	public override void Initialize(AnalysisContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
	}

	static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
		DiagnosticId,
		"Sample",
		"Sample",
		"Test",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true);
}
