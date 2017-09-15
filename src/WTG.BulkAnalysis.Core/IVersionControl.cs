namespace WTG.BulkAnalysis.Core
{
	public interface IVersionControl
	{
		void PendEdit(string[] paths);
		void PendAdd(string[] paths);
		void PendDelete(string[] paths);
	}
}
