namespace WTG.BulkAnalysis.Core
{
	public sealed class NullVersionControl : IVersionControl
	{
		public static NullVersionControl Instance { get; } = new NullVersionControl();

		NullVersionControl()
		{
		}

		void IVersionControl.PendEdit(string[] paths)
		{
		}

		void IVersionControl.PendAdd(string[] paths)
		{
		}

		void IVersionControl.PendDelete(string[] paths)
		{
		}
	}
}
