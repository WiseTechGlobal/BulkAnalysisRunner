using System;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace WTG.BulkAnalysis.Core
{
	public sealed class TfsVersionControl : IVersionControl, IDisposable
	{
		public static TfsVersionControl Create(string directory)
		{
			var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(directory);

			if (workspaceInfo != null)
			{
				var tfs = new TfsTeamProjectCollection(workspaceInfo.ServerUri);
				tfs.EnsureAuthenticated();

				var vcs = tfs.GetService<VersionControlServer>();
				var workspace = vcs.GetWorkspace(workspaceInfo);

				if (workspace != null)
				{
					return new TfsVersionControl(tfs, workspace);
				}
			}

			return null;
		}

		TfsVersionControl(TfsTeamProjectCollection tfs, Workspace workspace)
		{
			this.tfs = tfs;
			this.workspace = workspace;
		}

		public void PendEdit(string[] paths) => workspace.PendEdit(paths);
		public void PendAdd(string[] paths) => workspace.PendAdd(paths);
		public void PendDelete(string[] paths) => workspace.PendDelete(paths);
		public void Dispose() => tfs.Dispose();

		readonly TfsTeamProjectCollection tfs;
		readonly Workspace workspace;
	}
}
