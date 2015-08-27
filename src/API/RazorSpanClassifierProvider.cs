using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Html.Editor.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.Web.Core.Composition;

namespace AspNet.Razor_vHalfNext
{
    [Export(typeof(IRazorSpanClassifierProvider))]
    [Version(SupportedRuntime.Version)]
	internal class RazorSpanClassifierProvider : IRazorSpanClassifierProvider, IClassifierProvider
	{
		[SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "This catch clause is intended to catch all exceptions")]
		IClassifier IClassifierProvider.GetClassifier(ITextBuffer diskBuffer)
		{
			IClassifier result = null;
			try
			{
				result = RazorSpanClassifier.FromBuffer(diskBuffer);
			}
			catch
			{
			}
			return result;
		}
	}
}
