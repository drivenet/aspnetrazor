using System;
using System.Collections.Generic;
using System.Web.Razor.Parser.SyntaxTree;

namespace AspNet.Razor_vHalfNext
{
	internal class SpansChangedEventArgs : EventArgs
	{
		internal IEnumerable<Span> Spans
		{
			get;
			private set;
		}

		internal SpansChangedEventArgs(IEnumerable<Span> spans)
		{
			Spans = spans;
		}
	}
}
