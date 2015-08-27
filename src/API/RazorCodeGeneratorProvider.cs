using System;
using System.ComponentModel.Composition;

using Microsoft.Html.Editor.ContainedLanguage.Razor.Def;
using Microsoft.Html.Editor.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.Web.Core.Composition;

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
