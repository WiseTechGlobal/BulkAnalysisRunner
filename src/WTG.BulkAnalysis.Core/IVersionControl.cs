namespace WTG.BulkAnalysis.Core
{
	public interface IVersionControl
	{
		void PendEdit(string[] paths);
	}
}
