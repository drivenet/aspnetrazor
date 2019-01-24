using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.WebTools.Languages.Html.Editor.ContainedLanguage.Razor.Def;
using Microsoft.WebTools.Languages.Html.Editor.Razor;
using Microsoft.WebTools.Languages.Shared.Composition;

namespace AspNet.Razor_vHalfNext
{
    [Export(typeof(IRazorCodeGeneratorProvider))]
    [Version(SupportedRuntime.Version)]
	internal class RazorCodeGeneratorProvider : IRazorCodeGeneratorProvider
	{
		IRazorCodeGenerator IRazorCodeGeneratorProvider.CreateRazorCodeGenerator(ITextBuffer buffer, Version razorVersion, string physicalPath, string virtualPath)
		{
			return RazorCodeGenerator.Create(buffer, razorVersion, physicalPath, virtualPath);
		}
	}
}
