using System;
using System.ComponentModel.Composition;

using Microsoft.Html.Editor.Razor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.Web.Core.Composition;

namespace AspNet.Razor_vHalfNext
{
    [Export(typeof(IRazorSmartIndentProvider))]
    [Version(SupportedRuntime.Version)]
	internal class RazorSmartIndentProvider : IRazorSmartIndentProvider, ISmartIndentProvider
	{
		public ISmartIndent CreateSmartIndent(ITextView textView)
		{
			return RazorSmartIndenter.Create(textView);
		}
	}
}
