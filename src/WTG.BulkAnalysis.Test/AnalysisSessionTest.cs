using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.Test;

[NonParallelizable]
public class AnalysisSessionTest
{
	[Test]
	public async Task ReportsAFixableRuleInAModernProject()
	{
		// SDK analyzer injection for the fixture project only works when the host process is .NET Core/.NET 5+.
		// On net472, MSBuildWorkspace does not populate AnalyzerReferences with the SDK's bundled analyzers,
		// so the analysis returns no diagnostics and this assertion would trivially fail.
		Assume.That(Environment.Version.Major >= 5, "SDK analyzer injection requires a .NET 5+ host process.");

		// The fixture uses a C# 12 collection expression; if the engine's Roslyn were too old to parse
		// it, analysis would be disrupted and CA1822 would not be found here.
		var path = CreateFixture(suppressCA1822: false);

		var diagnostics = await AnalyzeAsync(path).ConfigureAwait(false);

		var ca1822 = diagnostics.Where(d => d.Id == "CA1822").ToArray();
		Assert.That(ca1822, Is.Not.Empty, "Expected CA1822 to be reported in the modern fixture project.");
		Assert.That(ca1822.All(d => d.HasCodeFix), Is.True);
	}

	[Test]
	public async Task HonoursGlobalAnalyzerConfigSuppression()
	{
		// Same fixture, but with a global analyzer config (as WTG.BuildConfiguration delivers one)
		// suppressing CA1822. It must not be reported.
		var path = CreateFixture(suppressCA1822: true);

		var diagnostics = await AnalyzeAsync(path).ConfigureAwait(false);

		Assert.That(diagnostics.Any(d => d.Id == "CA1822"), Is.False, "CA1822 is suppressed by the global analyzer config and must not be reported.");
	}

	static async Task<ImmutableArray<DiagnosticInfo>> AnalyzeAsync(string projectPath)
	{
		var options = new AnalysisSessionOptions(projectPath, "Debug", loadDir: null, ImmutableArray<string>.Empty);
		using var session = await AnalysisSession.OpenAsync(options, NullLog.Instance, CancellationToken.None).ConfigureAwait(false);
		return await session.AnalyzeAsync(ImmutableHashSet.Create("CA1822"), CancellationToken.None).ConfigureAwait(false);
	}

	string CreateFixture(bool suppressCA1822)
	{
		var dir = Path.Combine(temporaryDirectory, suppressCA1822 ? "suppressed" : "plain");
		Directory.CreateDirectory(dir);

		var globalConfig = suppressCA1822
			? "    <ItemGroup><GlobalAnalyzerConfigFiles Include=\"suppress.globalconfig\" /></ItemGroup>" + Environment.NewLine
			: string.Empty;

		File.WriteAllText(
			Path.Combine(dir, "Fixture.csproj"),
			"<Project Sdk=\"Microsoft.NET.Sdk\">" + Environment.NewLine +
			"  <PropertyGroup>" + Environment.NewLine +
			"    <TargetFramework>net8.0</TargetFramework>" + Environment.NewLine +
			"    <Nullable>enable</Nullable>" + Environment.NewLine +
			"    <AnalysisMode>AllEnabledByDefault</AnalysisMode>" + Environment.NewLine +
			"    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>" + Environment.NewLine +
			"  </PropertyGroup>" + Environment.NewLine +
			globalConfig +
			"</Project>" + Environment.NewLine);

		File.WriteAllText(
			Path.Combine(dir, "Calc.cs"),
			"namespace Fixture;" + Environment.NewLine + Environment.NewLine +
			"public class Calc" + Environment.NewLine +
			"{" + Environment.NewLine +
			"    static readonly int[] Numbers = [1, 2, 3];" + Environment.NewLine +
			"    public int Total() => Numbers.Length;" + Environment.NewLine +
			"}" + Environment.NewLine);

		if (suppressCA1822)
		{
			File.WriteAllText(
				Path.Combine(dir, "suppress.globalconfig"),
				"is_global = true" + Environment.NewLine +
				"dotnet_diagnostic.CA1822.severity = none" + Environment.NewLine);
		}

		Restore(dir);
		return Path.Combine(dir, "Fixture.csproj");
	}

	static void Restore(string directory)
	{
		var info = new ProcessStartInfo("dotnet", "restore")
		{
			WorkingDirectory = directory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		using var process = Process.Start(info)!;
		process.WaitForExit();
		Assert.That(process.ExitCode, Is.Zero, "Fixture restore failed.");
	}

	[OneTimeSetUp]
	public void OneTimeSetUp()
	{
		AnalyzerAssemblyResolver.Register();

		if (!MSBuildLocator.IsRegistered)
		{
			MSBuildLocator.RegisterDefaults();
		}
	}

	[SetUp]
	public void Setup()
	{
		temporaryDirectory = Path.Combine(Path.GetTempPath(), "WTG.BulkAnalysis.AnalysisSessionTest", Guid.NewGuid().ToString());
		Directory.CreateDirectory(temporaryDirectory);
	}

	[TearDown]
	public void TearDown()
	{
		if (temporaryDirectory != null)
		{
			Directory.Delete(temporaryDirectory, recursive: true);
			temporaryDirectory = null!;
		}
	}

	string temporaryDirectory = null!;

	sealed class NullLog : ILog
	{
		public static readonly NullLog Instance = new NullLog();
		public void WriteFormatted(FormattableString message, LogLevel level = LogLevel.Normal) { }
		public void WriteLine(string message, LogLevel level = LogLevel.Normal) { }
		public void WriteLine() { }
	}
}
