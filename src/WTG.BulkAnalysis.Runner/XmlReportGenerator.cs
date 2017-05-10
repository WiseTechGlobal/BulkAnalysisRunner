using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.Runner
{
	sealed class XmlReportGenerator : IReportGenerator, IDisposable
	{
		[SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
		public static XmlReportGenerator New(string path)
		{
			return New(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
		}

		public static XmlReportGenerator New(Stream stream)
		{
			var settings = new XmlWriterSettings
			{
				CloseOutput = true,
				Indent = true,
				IndentChars = "\t",
			};

			return new XmlReportGenerator(XmlWriter.Create(stream, settings));
		}

		XmlReportGenerator(XmlWriter writer)
		{
			this.writer = writer;
			WriteStart();
		}

		public void Report(Solution solution, ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> diagnostics)
		{
			if (writer == null)
			{
				throw new ObjectDisposedException(nameof(XmlReportGenerator));
			}

			lock (writer)
			{
				writer.WriteStartElement("solution");
				writer.WriteAttributeString("path", solution.FilePath);

				foreach (var pair in diagnostics)
				{
					var project = solution.GetProject(pair.Key);

					WriteProjectElement(project, pair.Value);
				}

				writer.WriteEndElement();
			}
		}

		public void Dispose()
		{
			if (writer != null)
			{
				WriteEnd();
				writer.Flush();
				writer.Close();
				writer = null;
			}
		}

		void WriteStart()
		{
			writer.WriteStartDocument();
			writer.WriteProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"analysis.xslt\"");
			writer.WriteStartElement("report", XmlNamespace);
		}

		void WriteEnd()
		{
			writer.WriteEndElement();
			writer.WriteEndDocument();
			writer.Flush();
		}

		void WriteProjectElement(Project project, ImmutableArray<Diagnostic> diagnostics)
		{
			writer.WriteStartElement("project");
			writer.WriteAttributeString("name", project.Name);

			var groupedDiagnostics = diagnostics.GroupBy(x => x.Location?.SourceTree?.FilePath ?? string.Empty);

			foreach (var g in groupedDiagnostics)
			{
				WriteFileElement(g.Key, g);
			}

			writer.WriteEndElement();
		}

		void WriteFileElement(string path, IEnumerable<Diagnostic> diagnostics)
		{
			writer.WriteStartElement("file");

			if (!string.IsNullOrEmpty(path))
			{
				writer.WriteAttributeString("path", path);
			}

			foreach (var diagnostic in diagnostics)
			{
				WriteDiagnosticElement(diagnostic);
			}

			writer.WriteEndElement();
		}

		void WriteDiagnosticElement(Diagnostic diagnostic)
		{
			writer.WriteStartElement("diagnostic");
			writer.WriteAttributeString("id", diagnostic.Id);

			var location = diagnostic.Location;
			var span = location.GetLineSpan();
			writer.WriteAttributeString("from", FormatPosition(span.StartLinePosition));
			writer.WriteAttributeString("to", FormatPosition(span.EndLinePosition));
			writer.WriteAttributeString("message", diagnostic.GetMessage());
			writer.WriteEndElement();
		}

		static string FormatPosition(LinePosition pos) => (pos.Line + 1) + "," + (pos.Character + 1);

		XmlWriter writer;

		const string XmlNamespace = "http://wisetechglobal.com/staticanalysis/2017/04/09/analysis.xsd";
	}
}
