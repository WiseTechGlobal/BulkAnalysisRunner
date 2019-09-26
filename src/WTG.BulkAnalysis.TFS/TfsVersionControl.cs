using System;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.TFS
{
	public sealed class TfsVersionControl : IVersionControl, IDisposable
	{
		public static TfsVersionControl Create(string directory, Uri tfsServer)
		{
			var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(directory);

			if (workspaceInfo == null)
			{
				if (tfsServer == null)
				{
					return null;
				}

				EnsureUpdatedWorkspaceInfoCache(tfsServer);
				workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(directory);

				if (workspaceInfo == null)
				{
					return null;
				}
			}

			var tfs = new TfsTeamProjectCollection(workspaceInfo.ServerUri);

			try
			{
				tfs.EnsureAuthenticated();

				var vcs = tfs.GetService<VersionControlServer>();
				var workspace = vcs.GetWorkspace(workspaceInfo);

				if (workspace != null)
				{
					var result = new TfsVersionControl(tfs, workspace);
					tfs = null;
					return result;
				}
			}
			finally
			{
				tfs?.Dispose();
			}

			return null;
		}

		static void EnsureUpdatedWorkspaceInfoCache(Uri tfsServer)
		{
			using (var tfs = new TfsTeamProjectCollection(tfsServer))
			{
				tfs.EnsureAuthenticated();
				var vcs = tfs.GetService<VersionControlServer>();
				Workstation.Current.EnsureUpdateWorkspaceInfoCache(vcs, vcs.AuthorizedUser);
			}
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
