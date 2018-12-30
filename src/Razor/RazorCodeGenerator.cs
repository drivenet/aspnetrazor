using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web.Razor;
using System.Web.Razor.Generator;
using System.Web.Razor.Parser.SyntaxTree;
using System.Web.Razor.Text;
using System.Windows.Threading;

using Microsoft.Html.Core.Artifacts;
using Microsoft.Html.Core.Tree.Extensions;
using Microsoft.Html.Editor.Completion;
using Microsoft.Html.Editor.ContainedLanguage.Common;
using Microsoft.Html.Editor.ContainedLanguage.Razor;
using Microsoft.Html.Editor.ContainedLanguage.Razor.Def;
using Microsoft.Html.Editor.ContentType.Def;
using Microsoft.Html.Editor.Document;
using Microsoft.Html.Editor.Projection;
using Microsoft.Html.Editor.Razor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.Web.Core.Utility;
using Microsoft.Web.Editor.Controller;
using Microsoft.Web.Editor.Host;
using Microsoft.Web.Editor.Services;

namespace AspNet.Razor_vHalfNext
{
    internal sealed class RazorCodeGenerator : IRazorCodeGenerator, IContainedCodeGenerator
	{
		private class ParseData
		{
			private DocumentParseCompleteEventArgs _parseCompleteEventArg;

			private int _lastVersion;

			public bool TreeStructureChanged
			{
				get;
				set;
			}

			public bool NotificationPending
			{
				get;
				set;
			}

			public GeneratorResults GeneratorResults
			{
				get
				{
					return _parseCompleteEventArg.GeneratorResults;
				}
			}

			public TextChange SourceChange
			{
				get
				{
					return _parseCompleteEventArg.SourceChange;
				}
			}

			public ParseData()
			{
				TreeStructureChanged = true;
				_parseCompleteEventArg = null;
				NotificationPending = false;
				_lastVersion = -1;
			}

			public bool Update(DocumentParseCompleteEventArgs e)
			{
				int versionNumber = (e.SourceChange.NewBuffer as IShimTextBuffer).Snapshot.Version.VersionNumber;
				if (versionNumber >= _lastVersion)
				{
					_lastVersion = versionNumber;
					_parseCompleteEventArg = e;
				}
				if (e.TreeStructureChanged)
				{
					TreeStructureChanged = true;
				}
				else if (!TreeStructureChanged)
				{
					TextChange sourceChange = e.SourceChange;
					if (sourceChange.NewLength > 1)
					{
						if ((sourceChange.NewBuffer as IShimTextBuffer).Snapshot.GetText(sourceChange.NewPosition, sourceChange.NewLength).Any(c => char.IsWhiteSpace(c)))
						{
							TreeStructureChanged = true;
						}
					}
				}
				bool expr_BE = !NotificationPending;
				if (expr_BE)
				{
					NotificationPending = true;
				}
				return expr_BE;
			}
		}

#pragma warning disable 0649
        [Import]
		private ICompletionBroker _completionBroker;

		[Import]
		private IRazorImportsProvider _razorImportsProvider;
#pragma warning restore 0649

        private HtmlEditorDocument _htmlDocument;

		private Microsoft.VisualStudio.Text.ITextBuffer _viewBuffer;

		private GeneratorResults _result;

		private ShimRazorEditorParserImpl _razorEditorParser;

		private bool _spanContextChanged = true;

		private TextChange? _pendingShimTextChange;

		private ParseData _parseData;

		private DateTime? _lastEditTime;

		private DateTime? _lastProcessedTime;

		private int _pendingRazorNamespacesWorkItem;

		private AutoBlockCompletor _autoBlockCompletor;

		private bool _bufferChangedWithinMassiveEdit;

		private LanguageProjectionBuffer _languageProjectionBuffer;

		private IEmbeddedLanguageLinePragmaReader _linePragmaReader;

		private string _code;

		private RazorRuntimeError _runtimeError;

		private string _fullPath;

		private EventHandler<SpansChangedEventArgs> PrivateSpansChanged;

		public event EventHandler<CodeRangesChangedEventArgs> OnCodeRangesChanged;

		public event EventHandler<ContainedCodeGeneratorEventArgs> OnCodeGenerationComplete;

		public event EventHandler<SpansChangedEventArgs> SpansChanged
		{
			add
			{
				if (_result != null)
				{
					IEnumerable<System.Web.Razor.Parser.SyntaxTree.Span> spans = _result.Document.Flatten();
					value(this, new SpansChangedEventArgs(spans));
				}
				PrivateSpansChanged = (EventHandler<SpansChangedEventArgs>)Delegate.Combine(PrivateSpansChanged, value);
			}
			remove
			{
				PrivateSpansChanged = (EventHandler<SpansChangedEventArgs>)Delegate.Remove(PrivateSpansChanged, value);
			}
		}

		private ITextView TextView
		{
			get
			{
				ITextView result = null;
				if (_viewBuffer != null)
				{
					TextViewData textViewDataForBuffer = TextViewConnectionListener.GetTextViewDataForBuffer(_viewBuffer);
					if (textViewDataForBuffer != null)
					{
                        result = textViewDataForBuffer.LastActiveView;
					}
				}
				return result;
			}
		}

		internal Block Document
		{
			get
			{
				Block result = null;
				if (_result != null)
				{
					result = _result.Document;
				}
				return result;
			}
		}

		public bool IsReady
		{
			get
			{
				return !_pendingShimTextChange.HasValue;
			}
		}

		public void ReparseFile()
		{
			System.Web.Razor.Text.ITextBuffer textBuffer = new ShimTextBufferAdapter(_viewBuffer.CurrentSnapshot);
			TextChange textChange = new TextChange(0, textBuffer.Length, textBuffer, 0, textBuffer.Length, textBuffer);
			CheckForStructureChanges(textChange);
		}

		internal static IRazorCodeGenerator Create(Microsoft.VisualStudio.Text.ITextBuffer buffer, Version razorVersion, string physicalPath, string virtualPath)
		{
			RazorCodeGenerator razorCodeGenerator = ServiceManager.GetService<RazorCodeGenerator>(buffer);
			if (razorCodeGenerator == null)
			{
				razorCodeGenerator = new RazorCodeGenerator(buffer, razorVersion, physicalPath, virtualPath);
			}
			return razorCodeGenerator;
		}

		private RazorCodeGenerator(Microsoft.VisualStudio.Text.ITextBuffer buffer, Version razorVersion, string physicalPath, string virtualPath)
		{
			WebEditor.CompositionService.SatisfyImportsOnce(this);
			_parseData = new ParseData();
			_viewBuffer = buffer;
			_viewBuffer.Changed += TextBuffer_OnChanged;
			_viewBuffer.PostChanged += TextBuffer_OnPostChanged;
			_htmlDocument = ServiceManager.GetService<HtmlEditorDocument>(_viewBuffer);
			_htmlDocument.OnDocumentClosing += OnClose;
            _htmlDocument.MassiveChangeEnded += OnMassiveChangeEnded;
			_fullPath = ((!string.IsNullOrEmpty(physicalPath)) ? physicalPath : "Default.cshtml");
			if (virtualPath == null)
			{
				virtualPath = "Default.cshtml";
			}
			_runtimeError = new RazorRuntimeError(_viewBuffer);
			_razorEditorParser = new ShimRazorEditorParserImpl(virtualPath, _fullPath);
			_razorEditorParser.DocumentParseComplete += DocumentParseComplete;
			ReparseFile();
			WebEditor.OnIdle += OnFirstIdle;
			ServiceManager.AddService(this, _viewBuffer);
			ServiceManager.AddService<IRazorCodeGenerator>(this, _viewBuffer);
			ServiceManager.AddService<IContainedCodeGenerator>(this, _viewBuffer);
		}

		private void OnMassiveChangeEnded(object sender, EventArgs e)
		{
			if (_bufferChangedWithinMassiveEdit)
			{
				_bufferChangedWithinMassiveEdit = false;
				ReparseFile();
			}
		}

		private void OnFirstIdle(object sender, EventArgs e)
		{
			WebEditor.OnIdle -= OnFirstIdle;
			if (_parseData != null)
			{
				DispatchGetRazorNamespacesWorkItem();
			}
		}

		private void DispatchGetRazorNamespacesWorkItem()
		{
			_pendingRazorNamespacesWorkItem++;
			_razorImportsProvider.GetImportsAsync(_viewBuffer, CBMGlobalImportsProvider.ExecuteInCBM, OnRazorNamespacesFound);
		}

		private void OnRazorNamespacesFound(string[] razorNamespaces)
		{
			List<string> namespaceList = new List<string>();
			if (razorNamespaces != null)
			{
				namespaceList.AddRange(razorNamespaces);
			}
			WebEditor.DispatchOnUIThread(delegate
			{
				OnRazorNamespacesFoundMainThread(namespaceList);
			});
		}

		private void OnRazorNamespacesFoundMainThread(IEnumerable<string> imports)
		{
			_pendingRazorNamespacesWorkItem--;
			if (_pendingRazorNamespacesWorkItem == 0 && _razorEditorParser != null)
			{
				_razorEditorParser.RazorEditorParser.Host.NamespaceImports.UnionWith(imports);
				ReparseFile();
			}
		}

		public void WaitForReady()
		{
			if (!IsReady)
			{
				Stopwatch stopwatch = new Stopwatch();
				stopwatch.Start();
				while (!IsReady && stopwatch.ElapsedMilliseconds < 5000L)
				{
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
				}
			}
		}

		private void DocumentParseComplete(object sender, DocumentParseCompleteEventArgs e)
		{
			if (_parseData == null)
			{
				return;
			}
            ParseData parseData = _parseData;
			bool flag2;
			lock (parseData)
			{
				flag2 = _parseData.Update(e);
			}
			if (flag2)
			{
				WebEditor.DispatchOnUIThread(delegate
				{
					DocumentParseCompleteMainThread();
				});
			}
		}

		private void DocumentParseCompleteMainThread()
		{
			if (_parseData == null)
			{
				return;
			}
            ParseData parseData = _parseData;
			lock (parseData)
			{
				_parseData.NotificationPending = false;
				if (_pendingShimTextChange.HasValue && (_parseData.SourceChange.NewBuffer as IShimTextBuffer).Snapshot == (_pendingShimTextChange.Value.NewBuffer as IShimTextBuffer).Snapshot)
				{
					_pendingShimTextChange = null;
					if (_viewBuffer != null)
					{
						ITextSnapshot snapshot = (_parseData.SourceChange.NewBuffer as IShimTextBuffer).Snapshot;
						if (snapshot != snapshot.TextBuffer.CurrentSnapshot)
						{
							ReparseFile();
						}
						else
						{
							_result = _parseData.GeneratorResults;
							if (!_parseData.TreeStructureChanged)
							{
								NotifyOnCodeGenerationComplete(null);
							}
							else
							{
								_parseData.TreeStructureChanged = false;
								IEnumerable<System.Web.Razor.Parser.SyntaxTree.Span> spans = _result.Document.Flatten();
								List<RazorRange> newCodeRanges = GetNewCodeRanges(spans);
								NotifyOnCodeGenerationComplete(newCodeRanges);
								NotifySpansChanged(spans, newCodeRanges);
								if (_spanContextChanged)
								{
									RepairCompletionSession();
									_spanContextChanged = false;
								}
								if (_parseData != null)
								{
									NotifyPossibleTrigger(_parseData.SourceChange);
								}
							}
						}
					}
				}
			}
		}

		private void RepairCompletionSession()
		{
			if (TextView != null && _completionBroker != null && _completionBroker.IsCompletionActive(TextView))
			{
				_completionBroker.DismissAllSessions(TextView);
				ServiceManager.GetService<HtmlCompletionController>(TextView).OnShowMemberList(false);
			}
		}

		private bool NotifyOnCodeGenerationComplete(List<RazorRange> newCodeRanges)
		{
			IList<RazorError> parserErrors = _result.ParserErrors;
			int num = parserErrors.Count + _runtimeError.Count;
			bool flag = newCodeRanges == null;
			List<CodeGenerationError> list = new List<CodeGenerationError>();
			if (_runtimeError.Count == 1)
			{
				list.Add(new CodeGenerationError(_runtimeError.Message, _runtimeError.Line, _runtimeError.Column, _runtimeError.Length));
			}
			for (int i = _runtimeError.Count; i < num; i++)
			{
				RazorError razorError = parserErrors[i - _runtimeError.Count];
				list.Add(new CodeGenerationError(razorError.Message, razorError.Location.LineIndex + 1, razorError.Location.CharacterIndex, razorError.Length));
			}
			_code = (flag ? string.Empty : GetCodeFromCCU(_result.GeneratedCode));
			if (OnCodeGenerationComplete != null)
			{
				OnCodeGenerationComplete(this, new ContainedCodeGeneratorEventArgs(_code, list));
			}
			return !flag && SetTextAndMappings(newCodeRanges);
		}

		private List<RazorRange> GetNewCodeRanges(IEnumerable<System.Web.Razor.Parser.SyntaxTree.Span> spans)
		{
			List<RazorRange> list = new List<RazorRange>();
			foreach (System.Web.Razor.Parser.SyntaxTree.Span current in spans)
			{
				ArtifactTreatAs artifactTreatAs;
				switch (current.Kind)
				{
				case SpanKind.Transition:
				case SpanKind.MetaCode:
					artifactTreatAs = ArtifactTreatAs.WhiteSpace;
					break;
				case SpanKind.Comment:
					artifactTreatAs = ArtifactTreatAs.Comment;
					break;
				case SpanKind.Code:
					goto IL_4A;
				case SpanKind.Markup:
					artifactTreatAs = ArtifactTreatAs.Tag;
					break;
				default:
					goto IL_4A;
				}
				IL_4C:
				if (artifactTreatAs != ArtifactTreatAs.Tag)
				{
					ITrackingSpan trackingSpan = _viewBuffer.CurrentSnapshot.CreateTrackingSpan(current.Parent.Start.AbsoluteIndex, current.Parent.Length, SpanTrackingMode.EdgeExclusive);
					list.Add(new RazorRange(artifactTreatAs, current.Start.AbsoluteIndex, current.Length, current.GetTrailingInclusion(), (RazorSpanType)current.Kind, trackingSpan, (RazorBlockType)current.Parent.Type));
					continue;
				}
				continue;
				IL_4A:
				artifactTreatAs = 0;
				goto IL_4C;
			}
			return list;
		}

		private void NotifySpansChanged(IEnumerable<System.Web.Razor.Parser.SyntaxTree.Span> spans, List<RazorRange> newCodeRanges)
		{
			if (PrivateSpansChanged != null)
			{
				PrivateSpansChanged(this, new SpansChangedEventArgs(spans));
			}
			if (OnCodeRangesChanged != null)
			{
				OnCodeRangesChanged(this, new CodeRangesChangedEventArgs(newCodeRanges));
			}
		}

		private void NotifyPossibleTrigger(TextChange shimTextChange)
		{
			if (shimTextChange.NewLength == 1)
			{
				HtmlCompletionController service = ServiceManager.GetService<HtmlCompletionController>(TextView);
				if (service != null)
				{
					char c = TextView.TextBuffer.CurrentSnapshot.GetText(shimTextChange.NewPosition, 1)[0];
					service.OnPostTypeChar(c);
				}
			}
		}

		private string GetCodeFromCCU(CodeCompileUnit codeCompileUnit)
		{
			CodeDomProvider codeDomProvider = (CodeDomProvider)Activator.CreateInstance(_razorEditorParser.CodeDomProviderType);
			string result;
			using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
			{
				codeDomProvider.GenerateCodeFromCompileUnit(codeCompileUnit, stringWriter, new CodeGeneratorOptions
				{
					BlankLinesBetweenMembers = false,
					IndentString = string.Empty
				});
				result = stringWriter.ToString();
			}
			return result;
		}

		private void OnClose(object sender, EventArgs args)
		{
			Close();
		}

		private void Close()
		{
			if (_parseData == null)
			{
				return;
			}
            ParseData parseData = _parseData;
			lock (parseData)
			{
				_fullPath = null;
				_htmlDocument.OnDocumentClosing -= OnClose;
				_htmlDocument.MassiveChangeEnded -= OnMassiveChangeEnded;
				_htmlDocument = null;
				ServiceManager.RemoveService<RazorCodeGenerator>(_viewBuffer);
				ServiceManager.RemoveService<IRazorCodeGenerator>(_viewBuffer);
				ServiceManager.RemoveService<IContainedCodeGenerator>(_viewBuffer);
				_parseData = null;
				_runtimeError.Close();
				_viewBuffer.Changed -= TextBuffer_OnChanged;
				_viewBuffer.PostChanged -= TextBuffer_OnPostChanged;
				SetProvisionallyAcceptedState(false);
				_viewBuffer = null;
				if (_razorEditorParser != null)
				{
					_razorEditorParser.DocumentParseComplete -= DocumentParseComplete;
					_razorEditorParser.Close();
					_razorEditorParser = null;
				}
			}
		}

		private List<ProjectionMapping> GetMappings(List<RazorRange> razorRanges)
		{
			List<ProjectionMapping> list = new List<ProjectionMapping>();
			ServiceManager.GetService<ProjectionBufferManager>(_viewBuffer);
			ITextSnapshot currentSnapshot = _viewBuffer.CurrentSnapshot;
			string fileName = Path.GetFileName(_fullPath);
			foreach (KeyValuePair<int, int> current in _linePragmaReader.GetCodePositions(_code, fileName))
			{
				GeneratedCodeMapping generatedCodeMapping;
				if (_result.DesignTimeLineMappings.TryGetValue(current.Key, out generatedCodeMapping))
				{
					int num = currentSnapshot.GetLineFromLineNumber(generatedCodeMapping.StartLine - 1).Start.Position + generatedCodeMapping.StartColumn - 1;
					int num2 = current.Value + generatedCodeMapping.StartGeneratedColumn - 1;
					int codeLength = generatedCodeMapping.CodeLength;
					foreach (RazorRange current2 in razorRanges)
					{
						if (current2.Start == num && current2.Length == codeLength)
						{
							ProjectionMapping item = new ProjectionMapping(num, num2, codeLength, current2.TrailingInclusion);
							list.Add(item);
							break;
						}
					}
				}
			}
			return list;
		}

		private void TextBuffer_OnChanged(object sender, TextContentChangedEventArgs e)
		{
			if (_htmlDocument.IsMassiveChangeInProgress)
			{
				_bufferChangedWithinMassiveEdit = true;
				return;
			}
			if (e.Changes.Count > 0)
			{
				System.Web.Razor.Text.ITextBuffer newBuffer = new ShimTextBufferAdapter(e.After);
				System.Web.Razor.Text.ITextBuffer oldBuffer = new ShimTextBufferAdapter(e.Before);
				ITextChange textChange = e.Changes[0];
				ITextChange expr_60 = e.Changes[e.Changes.Count - 1];
				int oldLength = expr_60.OldEnd - textChange.OldPosition;
				int newLength = expr_60.NewEnd - textChange.NewPosition;
				TextChange textChange2 = new TextChange(textChange.OldPosition, oldLength, oldBuffer, textChange.NewPosition, newLength, newBuffer);
				CheckForStructureChanges(textChange2);
			}
		}

		private void TextBuffer_OnPostChanged(object sender, EventArgs e)
		{
			if (_autoBlockCompletor != null)
			{
				AutoBlockCompletor arg_15_0 = _autoBlockCompletor;
				_autoBlockCompletor = null;
				arg_15_0.CompleteBlock();
			}
		}

		private void CheckForStructureChanges(TextChange textChange)
		{
			SetProvisionallyAcceptedState(false);
			PartialParseResult partialParseResult = _razorEditorParser.CheckForStructureChanges(textChange);
			CreateAutoBlockCompleter(partialParseResult, textChange);
			if (IsFlagSet(partialParseResult, PartialParseResult.Rejected))
			{
				_pendingShimTextChange = new TextChange?(textChange);
			}
			if (IsFlagSet(partialParseResult, PartialParseResult.Provisional))
			{
				SetProvisionallyAcceptedState(true);
			}
			if (IsFlagSet(partialParseResult, PartialParseResult.SpanContextChanged))
			{
				_spanContextChanged = true;
			}
		}

		private bool IsFlagSet(PartialParseResult toTest, PartialParseResult flag)
		{
			return (toTest & flag) > (PartialParseResult)0;
		}

		private void CreateAutoBlockCompleter(PartialParseResult parseResult, TextChange textChange)
		{
			if (IsFlagSet(parseResult, PartialParseResult.AutoCompleteBlock))
			{
				_autoBlockCompletor = new AutoBlockCompletor(TextView, textChange.NewPosition, _razorEditorParser.GetAutoCompleteString());
				return;
			}
			if (textChange.OldLength == 0 && textChange.NewLength > 0 && textChange.NewLength <= 2 && Whitespace.IsNewLine((textChange.NewBuffer as IShimTextBuffer).Snapshot.GetText(textChange.NewPosition, textChange.NewLength)) && _viewBuffer.ContentType.IsOfType("RazorCSharp"))
			{
				ITextSnapshot currentSnapshot = _viewBuffer.CurrentSnapshot;
				ITextSnapshotLine lineFromPosition = currentSnapshot.GetLineFromPosition(textChange.NewPosition);
				string text = lineFromPosition.GetText();
				int num = Math.Min(lineFromPosition.Length, textChange.NewPosition - lineFromPosition.Start) - 1;
				while (num >= 0 && char.IsWhiteSpace(text[num]))
				{
					num--;
				}
				if (num >= 1 && text[num] == '{' && text[num - 1] == '@')
				{
					int num2 = textChange.NewPosition + textChange.NewLength;
					ITextSnapshotLine lineFromPosition2 = currentSnapshot.GetLineFromPosition(num2);
					string text2 = lineFromPosition2.GetText();
					int num3 = num2 - lineFromPosition2.Start;
					while (num3 < lineFromPosition2.Length && char.IsWhiteSpace(text2[num3]))
					{
						num3++;
					}
					if (num3 < lineFromPosition2.Length && text2[num3] == '}')
					{
						_autoBlockCompletor = new AutoBlockCompletor(TextView, textChange.NewPosition, string.Empty);
					}
				}
			}
		}

		internal bool SetTextAndMappings(List<RazorRange> newCodeRanges)
		{
			bool flag = false;
			EnsureLanguageProjectionBuffer();
			if (_languageProjectionBuffer == null)
			{
				return false;
			}
			IEnumerable<RazorRange> currentRanges = (_htmlDocument.ContentTypeHandler as IRazorContentTypeHandler).CurrentRanges;
			if (currentRanges.Count<RazorRange>() != newCodeRanges.Count)
			{
				flag = true;
			}
			else
			{
				int num = 0;
				foreach (RazorRange current in currentRanges)
				{
					RazorRange razorRange = newCodeRanges[num];
					if (razorRange.Start != current.Start || razorRange.Length != current.Length)
					{
						flag = true;
						break;
					}
					num++;
				}
			}
			if (!flag)
			{
				IProjectionBuffer iProjectionBuffer = _languageProjectionBuffer.IProjectionBuffer;
				string text = iProjectionBuffer.CurrentSnapshot.GetText();
				int matchingPrefixLen = StringUtility.GetMatchingPrefixLen(text, _code);
				if (matchingPrefixLen != _code.Length || _code.Length != text.Length)
				{
					int num2 = Whitespace.GetMatchingSuffixLen(text, _code);
					int num3 = Math.Min(text.Length, _code.Length);
					if (matchingPrefixLen + num2 > num3)
					{
						num2 = num3 - matchingPrefixLen;
					}
					int num4 = matchingPrefixLen + num2;
					int num5 = text.Length - num4;
					ReadOnlyCollection<SnapshotSpan> arg_14A_0 = iProjectionBuffer.CurrentSnapshot.GetSourceSpans();
					int num6 = 0;
					bool flag2 = false;
					foreach (SnapshotSpan current2 in arg_14A_0)
					{
						int num7 = num6 + current2.Length;
						if (num7 > matchingPrefixLen)
						{
							if (num6 < matchingPrefixLen && num7 > matchingPrefixLen + num5 && !current2.Snapshot.ContentType.IsOfType("htmlx"))
							{
								flag2 = true;
								break;
							}
							break;
						}
						else
						{
							num6 = num7;
						}
					}
					if (flag2)
					{
						Microsoft.VisualStudio.Text.Span replaceSpan = new Microsoft.VisualStudio.Text.Span(matchingPrefixLen, num5);
						string replaceWith = _code.Substring(matchingPrefixLen, _code.Length - num4);
						iProjectionBuffer.Replace(replaceSpan, replaceWith);
					}
					else
					{
						flag = true;
					}
				}
			}
			if (flag)
			{
				List<ProjectionMapping> mappings = GetMappings(newCodeRanges);
				mappings.Sort();
				_languageProjectionBuffer.SetTextAndMappings(_code, mappings.ToArray());
			}
			return flag;
		}

		private void EnsureLanguageProjectionBuffer()
		{
			if (_languageProjectionBuffer == null)
			{
				ProjectionBufferManager service = ServiceManager.GetService<ProjectionBufferManager>(_viewBuffer);
				if (service != null)
				{
					if (Path.GetExtension(_fullPath) == ".vbhtml")
					{
						_languageProjectionBuffer = (service.GetProjectionBuffer(".vb") as LanguageProjectionBuffer);
					}
					else
					{
						_languageProjectionBuffer = (service.GetProjectionBuffer(".cs") as LanguageProjectionBuffer);
					}
				}
			}
			if (_linePragmaReader == null)
			{
				if (Path.GetExtension(_fullPath) == ".vbhtml")
				{
					_linePragmaReader = EmbeddedLanguageLinePragmaReaderFactory.Create("vb");
					return;
				}
				_linePragmaReader = EmbeddedLanguageLinePragmaReaderFactory.Create("c#");
			}
		}

		private void SetProvisionallyAcceptedState(bool newState)
		{
			if (newState)
			{
				if (!_lastEditTime.HasValue)
				{
					WebEditor.OnIdle += OnIdle;
					_lastEditTime = new DateTime?(DateTime.UtcNow);
					_lastProcessedTime = _lastEditTime;
					return;
				}
			}
			else if (_lastEditTime.HasValue)
			{
				WebEditor.OnIdle -= OnIdle;
				_lastEditTime = null;
				_lastProcessedTime = null;
			}
		}

		private void OnIdle(object sender, EventArgs e)
		{
			DateTime utcNow = DateTime.UtcNow;
			if (utcNow > _lastProcessedTime.Value.AddMilliseconds(250.0))
			{
				_lastProcessedTime = new DateTime?(utcNow);
				if (_completionBroker.IsCompletionActive(TextView))
				{
					_lastEditTime = _lastProcessedTime;
					return;
				}
				if (utcNow > _lastEditTime.Value.AddMilliseconds(3000.0))
				{
					ReparseFile();
				}
			}
		}
	}
}
