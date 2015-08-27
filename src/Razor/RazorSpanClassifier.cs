using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Web.Razor.Parser.SyntaxTree;

using Microsoft.Html.Core.Classify;
using Microsoft.Html.Editor.Document;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.Web.Editor.Host;
using Microsoft.Web.Editor.Services;

namespace AspNet.Razor_vHalfNext
{
    internal class RazorSpanClassifier : IClassifier
	{
		private ITextBuffer _diskBuffer;

		private IClassificationType _razorDelimiterClassificationType;

		private IClassificationType _razorCommentClassificationType;

		private List<ClassificationData> _spansToClassify;

		private bool _advisedToSpansChanged;

		private HtmlEditorDocument _document;

		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

		private RazorSpanClassifier(ITextBuffer diskBuffer)
		{
			try
			{
				_diskBuffer = diskBuffer;
				IClassificationTypeRegistryService value = WebEditor.ExportProvider.GetExport<IClassificationTypeRegistryService>().Value;
				string type = HtmlClassificationTypes.MapToEditorClassification("HtmlServerCodeBlockSeparator");
				string type2 = HtmlClassificationTypes.MapToEditorClassification("HtmlComment");
				_razorDelimiterClassificationType = value.GetClassificationType(type);
				_razorCommentClassificationType = value.GetClassificationType(type2);
				_spansToClassify = new List<ClassificationData>();
				_document = ServiceManager.GetService<HtmlEditorDocument>(diskBuffer);
				_document.OnDocumentClosing += OnDocumentClosing;
				ServiceManager.AddService<RazorSpanClassifier>(this, _diskBuffer);
			}
			catch
			{
				OnDocumentClosing(null, EventArgs.Empty);
				throw;
			}
		}

		internal static RazorSpanClassifier FromBuffer(ITextBuffer textBuffer)
		{
			RazorSpanClassifier razorSpanClassifier = ServiceManager.GetService<RazorSpanClassifier>(textBuffer);
			if (razorSpanClassifier == null)
			{
				razorSpanClassifier = new RazorSpanClassifier(textBuffer);
			}
			return razorSpanClassifier;
		}

		private void OnDocumentClosing(object sender, EventArgs e)
		{
			RazorCodeGenerator service = ServiceManager.GetService<RazorCodeGenerator>(_diskBuffer);
			if (service != null)
			{
				service.SpansChanged -= OnSpansChanged;
			}
			_document.OnDocumentClosing -= OnDocumentClosing;
			ServiceManager.RemoveService<RazorSpanClassifier>(_diskBuffer);
		}

		private void EnsureInitialized()
		{
			if (!_advisedToSpansChanged)
			{
				RazorCodeGenerator service = ServiceManager.GetService<RazorCodeGenerator>(_diskBuffer);
				if (service != null)
				{
					service.SpansChanged += OnSpansChanged;
					_advisedToSpansChanged = true;
				}
			}
		}

		private int GetFirstClassificationDataAfterOrAtPosition(int position, ITextSnapshot snapshot)
		{
			int i = 0;
			int num = _spansToClassify.Count - 1;
			while (i <= num)
			{
				int num2 = (i + num) / 2;
				int num3 = _spansToClassify[num2].TrackingSpan.GetStartPoint(snapshot);
				if (position <= num3)
				{
					num = num2 - 1;
				}
				else
				{
					i = num2 + 1;
				}
			}
			return num + 1;
		}

		IList<ClassificationSpan> IClassifier.GetClassificationSpans(SnapshotSpan toClassifySpan)
		{
			List<ClassificationSpan> list = new List<ClassificationSpan>();
			if (_document.IsMassiveChangeInProgress)
			{
				return list;
			}
			EnsureInitialized();
			ITextSnapshot snapshot = toClassifySpan.Snapshot;
			for (int i = GetFirstClassificationDataAfterOrAtPosition(toClassifySpan.Start, snapshot); i < _spansToClassify.Count; i++)
			{
				ClassificationData classificationData = _spansToClassify[i];
				SnapshotSpan span = classificationData.TrackingSpan.GetSpan(snapshot);
				if (span.Start >= toClassifySpan.End)
				{
					break;
				}
				Microsoft.VisualStudio.Text.Span span2 = Microsoft.VisualStudio.Text.Span.FromBounds(Math.Max(span.Start, toClassifySpan.Start), Math.Min(span.End, toClassifySpan.End));
				SnapshotSpan arg_C4_0 = new SnapshotSpan(snapshot, span2);
				IClassificationType classificationType = classificationData.ClassificationType;
				ClassificationSpan item = new ClassificationSpan(arg_C4_0, classificationType);
				list.Add(item);
			}
			return list;
		}

		private void OnSpansChanged(object sender, SpansChangedEventArgs eventArgs)
		{
			IEnumerable<System.Web.Razor.Parser.SyntaxTree.Span> arg_8C_0 = eventArgs.Spans;
			Microsoft.VisualStudio.Text.Span? span = null;
			ITextSnapshot currentSnapshot = _diskBuffer.CurrentSnapshot;
			if (_spansToClassify.Count > 0)
			{
				SnapshotPoint startPoint = _spansToClassify[0].TrackingSpan.GetStartPoint(currentSnapshot);
				SnapshotPoint endPoint = _spansToClassify[_spansToClassify.Count - 1].TrackingSpan.GetEndPoint(currentSnapshot);
				span = new Microsoft.VisualStudio.Text.Span?(Microsoft.VisualStudio.Text.Span.FromBounds(startPoint.Position, endPoint.Position));
			}
			_spansToClassify = new List<ClassificationData>();
			IClassificationType classificationType = null;
			foreach (System.Web.Razor.Parser.SyntaxTree.Span current in arg_8C_0)
			{
				switch (current.Kind)
				{
				case SpanKind.Transition:
				case SpanKind.MetaCode:
					classificationType = _razorDelimiterClassificationType;
					break;
				case SpanKind.Comment:
					classificationType = _razorCommentClassificationType;
					break;
				}
				if (classificationType != null)
				{
					Microsoft.VisualStudio.Text.Span span2 = new Microsoft.VisualStudio.Text.Span(current.Start.AbsoluteIndex, current.Length);
					ClassificationData item = new ClassificationData(currentSnapshot.CreateTrackingSpan(span2, SpanTrackingMode.EdgeExclusive), classificationType);
					_spansToClassify.Add(item);
					classificationType = null;
				}
			}
			if (_spansToClassify.Count > 0)
			{
				SnapshotPoint startPoint2 = _spansToClassify[0].TrackingSpan.GetStartPoint(currentSnapshot);
				SnapshotPoint endPoint2 = _spansToClassify[_spansToClassify.Count - 1].TrackingSpan.GetEndPoint(currentSnapshot);
				if (!span.HasValue)
				{
					span = new Microsoft.VisualStudio.Text.Span?(Microsoft.VisualStudio.Text.Span.FromBounds(startPoint2.Position, endPoint2.Position));
				}
				else
				{
					span = new Microsoft.VisualStudio.Text.Span?(Microsoft.VisualStudio.Text.Span.FromBounds(Math.Min(span.Value.Start, startPoint2.Position), Math.Max(span.Value.End, endPoint2.Position)));
				}
			}
			if (span.HasValue)
			{
				EventHandler<ClassificationChangedEventArgs> classificationChanged = ClassificationChanged;
				if (classificationChanged != null)
				{
					SnapshotSpan changeSpan = new SnapshotSpan(currentSnapshot, span.Value);
					classificationChanged(this, new ClassificationChangedEventArgs(changeSpan));
				}
			}
		}
	}
}
