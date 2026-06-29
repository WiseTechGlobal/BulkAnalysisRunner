using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.Test;

public class DiagnosticInfoTest
{
	[Test]
	public void MapsFieldsAndConvertsPositionsToOneBased()
	{
		// 'class C' starts at line 2, column 1 (0-based: line 1, char 0).
		var tree = CSharpSyntaxTree.ParseText("\nclass C\n{\n}\n", path: "Widget.cs");
		var start = tree.GetText().Lines[1].Start;
		var location = Location.Create(tree, TextSpan.FromBounds(start, start + "class C".Length));

		var descriptor = new DiagnosticDescriptor(
			"WTG9999",
			"The title",
			"The message {0}",
			"Naming",
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true);

		var diagnostic = Diagnostic.Create(descriptor, location, "detail");

		var info = DiagnosticInfo.Create(diagnostic, "MyProject", hasCodeFix: true);

		Assert.Multiple(() =>
		{
			Assert.That(info.Id, Is.EqualTo("WTG9999"));
			Assert.That(info.Title, Is.EqualTo("The title"));
			Assert.That(info.Message, Is.EqualTo("The message detail"));
			Assert.That(info.Severity, Is.EqualTo("Warning"));
			Assert.That(info.Category, Is.EqualTo("Naming"));
			Assert.That(info.ProjectName, Is.EqualTo("MyProject"));
			Assert.That(info.FilePath, Is.EqualTo("Widget.cs"));
			Assert.That(info.HasCodeFix, Is.True);

			// Roslyn reports 0-based positions; DiagnosticInfo exposes 1-based.
			Assert.That(info.StartLine, Is.EqualTo(2));
			Assert.That(info.StartColumn, Is.EqualTo(1));
		});
	}

	[Test]
	public void RepresentsNonSourceDiagnosticsWithoutAFilePath()
	{
		var descriptor = new DiagnosticDescriptor(
			"WTG0001",
			"No location",
			"No location",
			"Usage",
			DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		var diagnostic = Diagnostic.Create(descriptor, Location.None);

		var info = DiagnosticInfo.Create(diagnostic, "MyProject", hasCodeFix: false);

		Assert.Multiple(() =>
		{
			Assert.That(info.FilePath, Is.Null);
			Assert.That(info.StartLine, Is.EqualTo(0));
			Assert.That(info.HasCodeFix, Is.False);
		});
	}
}
