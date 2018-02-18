using System;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace WTG.BulkAnalysis.Core
{
	public sealed class TfsVersionControl : IVersionControl, IDisposable
	{
		public static TfsVersionControl Create(Uri server, string directory)
		{
			var tfs = new TfsTeamProjectCollection(server);
			tfs.EnsureAuthenticated();
			var vcs = tfs.GetService<VersionControlServer>();
			var workspace = vcs.TryGetWorkspace(directory);

			if (workspace == null)
			{
				throw new InvalidConfigurationException(FormattableString.Invariant($"No workspace mapping found for '{directory}'."));
			}

			return new TfsVersionControl(tfs, workspace);
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