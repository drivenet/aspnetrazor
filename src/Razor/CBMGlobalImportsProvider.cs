using System;
using System.Linq;
using System.Web.WebPages.Razor;

namespace AspNet.Razor_vHalfNext
{
	internal static class CBMGlobalImportsProvider
	{
		public static string[] ExecuteInCBM()
		{
			return WebPageRazorHost.GetGlobalImports().ToArray();
		}
	}
}
