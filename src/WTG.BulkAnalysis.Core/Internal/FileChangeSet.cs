using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;

namespace WTG.BulkAnalysis.Core
{
	sealed class FileChangeSet
	{
		FileChangeSet()
		{
			filesToEdit = new HashSet<string>();
			filesToDelete = new HashSet<string>();
			filesToAdd = new HashSet<string>();
		}

		public void PreApply(IVersionControl versionControl)
		{
			if (filesToEdit.Count > 0)
			{
				versionControl.PendEdit(filesToEdit.ToArray());
			}

			if (filesToDelete.Count > 0)
			{
				// Not 100% sure if this is correct.
				versionControl.PendEdit(filesToDelete.ToArray());
			}
		}

		public void PostApply(IVersionControl versionControl)
		{
			if (filesToDelete.Count > 0)
			{
				versionControl.PendDelete(filesToDelete.ToArray());
			}

			if (filesToAdd.Count > 0)
			{
				versionControl.PendAdd(filesToAdd.ToArray());
			}
		}

		public static FileChangeSet? Extract(Solution oldSolution, ImmutableArray<CodeActionOperation> operations)
		{
			var newSolution = operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution;

			if (newSolution == null)
			{
				return null;
			}

			var result = new FileChangeSet();
			result.Process(newSolution.GetChanges(oldSolution));
			return result;
		}

		void Process(SolutionChanges changes)
		{
			foreach (var change in changes.GetProjectChanges())
			{
				Process(change);
			}

			foreach (var change in changes.GetAddedProjects())
			{
				AddRange(filesToAdd, GetAllFilenames(change));
			}

			foreach (var change in changes.GetRemovedProjects())
			{
				AddRange(filesToDelete, GetAllFilenames(change));
			}
		}

		void Process(ProjectChanges projectChange)
		{
			AddRange(filesToEdit, GetFilenames(
				projectChange.OldProject,
				projectChange.GetChangedDocuments(),
				projectChange.GetAddedAdditionalDocuments()));

			AddRange(filesToDelete, GetFilenames(
				projectChange.OldProject,
				projectChange.GetChangedDocuments(),
				projectChange.GetChangedAdditionalDocuments()));

			AddRange(filesToAdd, GetFilenames(
				projectChange.NewProject,
				projectChange.GetAddedDocuments(),
				projectChange.GetAddedAdditionalDocuments()));

			if (projectChange.GetAddedAdditionalDocuments().Any() ||
				projectChange.GetAddedAnalyzerReferences().Any() ||
				projectChange.GetAddedDocuments().Any() ||
				projectChange.GetAddedMetadataReferences().Any() ||
				projectChange.GetAddedProjectReferences().Any() ||
				projectChange.GetRemovedAdditionalDocuments().Any() ||
				projectChange.GetRemovedAnalyzerReferences().Any() ||
				projectChange.GetRemovedDocuments().Any() ||
				projectChange.GetRemovedMetadataReferences().Any() ||
				projectChange.GetRemovedProjectReferences().Any())
			{
				var filePath = projectChange.OldProject.FilePath;

				if (filePath != null)
				{
					filesToEdit.Add(filePath);
				}
			}
		}

		static IEnumerable<string> GetAllFilenames(Project project)
		{
			foreach (var document in project.Documents)
			{
				if (document.FilePath != null)
				{
					yield return document.FilePath;
				}
			}

			foreach (var document in project.AdditionalDocuments)
			{
				if (document.FilePath != null)
				{
					yield return document.FilePath;
				}
			}
		}

		static IEnumerable<string> GetFilenames(Project project, IEnumerable<DocumentId> documentIds, IEnumerable<DocumentId> additionalIds)
		{
			foreach (var change in documentIds)
			{
				var document = project.GetDocument(change);

				if (document?.FilePath != null)
				{
					yield return document.FilePath;
				}
			}

			foreach (var change in additionalIds)
			{
				var document = project.GetAdditionalDocument(change);

				if (document?.FilePath != null)
				{
					yield return document.FilePath;
				}
			}
		}

		static void AddRange(HashSet<string> set, IEnumerable<string> values)
		{
			foreach (var value in values)
			{
				set.Add(value);
			}
		}

		readonly HashSet<string> filesToEdit;
		readonly HashSet<string> filesToDelete;
		readonly HashSet<string> filesToAdd;
	}
}
