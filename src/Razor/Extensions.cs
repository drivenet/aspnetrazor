using System.Web.Razor.Parser.SyntaxTree;

using Microsoft.WebTools.Languages.Html.Artifacts;

namespace AspNet.Razor_vHalfNext
{
    public static class Extensions
	{
		public static AdditionalContentInclusion GetTrailingInclusion(this Span span)
		{
			AdditionalContentInclusion result = AdditionalContentInclusion.None;
			if (span.EditHandler.AcceptedCharacters != AcceptedCharacters.None)
			{
				result = AdditionalContentInclusion.All;
				if (span.EditHandler.AcceptedCharacters == AcceptedCharacters.NonWhiteSpace)
				{
					result = AdditionalContentInclusion.AllButWS;
				}
				else if (span.EditHandler.AcceptedCharacters == AcceptedCharacters.AnyExceptNewline)
				{
					result = AdditionalContentInclusion.AllButNL;
				}
			}
			return result;
		}
	}
}
