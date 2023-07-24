using Microsoft.Extensions.FileProviders;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.Test;

public class SolutionLocatorTest
{
	[Test]
	public void ReadsSolutionListFromBuildXml()
	{
		WriteFile("Build.xml.sample", "Build.xml");
		var solutions = SolutionLocator.Locate(temporaryDirectory, solutionFilter: null);

		Assert.That(
			solutions.ToArray(),
			Is.EqualTo(new[]
			{
				Path.Combine("src", "BulkAnalysisRunner.sln"),
				"AnotherSln.sln",
			}.Select(path => Path.Combine(temporaryDirectory, path))));
	}

	[Test]
	public void ReadsApplicableSolutionListFromParentBuildXmlWhenValid()
	{
		WriteFile("Build.xml.sample", "Build.xml");

		var subdirPath = Path.Combine(temporaryDirectory, "src");
		Directory.CreateDirectory(subdirPath);

		var solutions = SolutionLocator.Locate(subdirPath, solutionFilter: null);

		Assert.That(
			solutions.ToArray(),
			Is.EqualTo(new[]
			{
				Path.Combine("src", "BulkAnalysisRunner.sln")
			}.Select(path => Path.Combine(temporaryDirectory, path))));
	}

	[Test]
	public void ReadsApplicableSolutionListFromParentBuildXmlWhenEmpty()
	{
		WriteFile("Build.xml.sample", "Build.xml");

		var subdirPath = Path.Combine(temporaryDirectory, "subdir");
		Directory.CreateDirectory(subdirPath);

		var solutions = SolutionLocator.Locate(subdirPath, solutionFilter: null);

		Assert.That(solutions, Is.Empty);
	}

	[TestCase("src_abc")]
	[TestCase("s")]
	[TestCase("subdir")]
	public void ReadsApplicableSolutionListFromParentBuildXmlWhenNoMatch(string subdirName)
	{
		WriteFile("Build.xml.sample", "Build.xml");

		var subdirPath = Path.Combine(temporaryDirectory, subdirName);
		Directory.CreateDirectory(subdirPath);

		var solutions = SolutionLocator.Locate(subdirPath, solutionFilter: null);
		Assert.That(solutions, Is.Empty);
	}

	IFileProvider fileProvider;
	string temporaryDirectory;

	void WriteFile(string sourcePath, string destinationPath)
	{
		var fileInfo = fileProvider.GetFileInfo(sourcePath);
		using var source = fileInfo.CreateReadStream();
		using var destination = File.OpenWrite(Path.Combine(temporaryDirectory, destinationPath));
		source.CopyTo(destination);
	}

	[SetUp]
	public void Setup()
	{
		fileProvider = new EmbeddedFileProvider(GetType().Assembly);
		temporaryDirectory = Path.Combine(Path.GetTempPath(), "WTG.BulkAnalysis.Test", Guid.NewGuid().ToString());
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
}
