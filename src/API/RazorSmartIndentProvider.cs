using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.WebTools.Languages.Html.Editor.Razor;
using Microsoft.WebTools.Languages.Shared.Composition;

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
