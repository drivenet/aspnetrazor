using Microsoft.VisualStudio.Text;

namespace AspNet.Razor_vHalfNext
{
	public interface IShimTextBuffer
	{
		ITextSnapshot Snapshot
		{
			get;
		}
	}
}
