using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WTG.BulkAnalysis.Core
{
	static class CompilationOptionsExtensions
	{
		// Attempt to change the warning level, currently only supports C#
		public static CompilationOptions WithWarningLevel(this CompilationOptions @this, int warningLevel)
		{
			if (@this is CSharpCompilationOptions csco)
			{
				return csco.WithWarningLevel(warningLevel);
			}

			return @this;
		}
	}
}
