using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace AspNet.Razor_vHalfNext
{
	internal class ClassificationData
	{
		internal ITrackingSpan TrackingSpan
		{
			get;
			private set;
		}

		internal IClassificationType ClassificationType
		{
			get;
			private set;
		}

		internal ClassificationData(ITrackingSpan trackingSpan, IClassificationType classificationType)
		{
			TrackingSpan = trackingSpan;
			ClassificationType = classificationType;
		}
	}
}
