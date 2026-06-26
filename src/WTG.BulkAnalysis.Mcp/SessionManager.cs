using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using WTG.BulkAnalysis.Core;

namespace WTG.BulkAnalysis.Mcp
{
	/// <summary>Owns the lifetime of the open <see cref="AnalysisSession"/> instances for the server.</summary>
	public sealed class SessionManager : IDisposable
	{
		public string Add(AnalysisSession session)
		{
			var id = "session-" + Interlocked.Increment(ref counter).ToString(CultureInfo.InvariantCulture);
			sessions[id] = session;
			return id;
		}

		public bool TryGet(string id, [NotNullWhen(true)] out AnalysisSession? session)
			=> sessions.TryGetValue(id, out session);

		public bool Remove(string id, [NotNullWhen(true)] out AnalysisSession? session)
			=> sessions.TryRemove(id, out session);

		public void Dispose()
		{
			foreach (var session in sessions.Values)
			{
				session.Dispose();
			}

			sessions.Clear();
		}

		readonly ConcurrentDictionary<string, AnalysisSession> sessions = new ConcurrentDictionary<string, AnalysisSession>(StringComparer.Ordinal);
		int counter;
	}
}
