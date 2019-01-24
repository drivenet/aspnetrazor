using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Web.Razor.Parser.SyntaxTree;
using System.Web.Razor.Text;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.WebTools.Languages.Html.Editor.ContentType.Def;
using Microsoft.WebTools.Languages.Html.Editor.Settings;
using Microsoft.WebTools.Languages.Shared.Editor.Host;
using Microsoft.WebTools.Languages.Shared.Editor.Services;
using Microsoft.WebTools.Languages.Shared.Formatting;

namespace AspNet.Razor_vHalfNext
{
    internal class RazorSmartIndenter : ISmartIndent, IDisposable
	{
		private ITextView _textView;

		private ISmartIndent _baseIndenter;

		private Dictionary<string, Lazy<ISmartIndentProvider, IContentTypeMetadata>> _smartIndentProviders;

		private RazorCodeGenerator _razorCodeGenerator;

		private ISmartIndent BaseIndenter
		{
			get
			{
				if (_baseIndenter == null)
				{
					Queue<IContentType> queue = new Queue<IContentType>(WebEditor.ExportProvider.GetExport<IContentTypeRegistryService>().Value.GetContentType("Razor").BaseTypes);
					while (queue.Count > 0)
					{
						IContentType contentType = queue.Dequeue();
						Lazy<ISmartIndentProvider, IContentTypeMetadata> lazy;
						if (SmartIndentProviders.TryGetValue(contentType.TypeName, out lazy))
						{
							_baseIndenter = lazy.Value.CreateSmartIndent(_textView);
							break;
						}
						foreach (IContentType current in contentType.BaseTypes)
						{
							queue.Enqueue(current);
						}
					}
				}
				return _baseIndenter;
			}
		}

		private Dictionary<string, Lazy<ISmartIndentProvider, IContentTypeMetadata>> SmartIndentProviders
		{
			get
			{
				if (_smartIndentProviders == null)
				{
					IContentTypeRegistryService value = WebEditor.ExportProvider.GetExport<IContentTypeRegistryService>().Value;
					IEnumerable<Lazy<ISmartIndentProvider, IContentTypeMetadata>> arg_35_0 = WebEditor.ExportProvider.GetExports<ISmartIndentProvider, IContentTypeMetadata>();
					_smartIndentProviders = new Dictionary<string, Lazy<ISmartIndentProvider, IContentTypeMetadata>>(StringComparer.OrdinalIgnoreCase);
					foreach (Lazy<ISmartIndentProvider, IContentTypeMetadata> current in arg_35_0)
					{
						foreach (string current2 in current.Metadata.ContentTypes)
						{
							IContentType contentType = value.GetContentType(current2);
							Lazy<ISmartIndentProvider, IContentTypeMetadata> lazy = null;
							if (!_smartIndentProviders.TryGetValue(contentType.TypeName, out lazy))
							{
								_smartIndentProviders.Add(contentType.TypeName, current);
							}
						}
					}
				}
				return _smartIndentProviders;
			}
		}

		private RazorCodeGenerator RazorCodeGenerator
		{
			get
			{
				if (_razorCodeGenerator == null)
				{
					IRazorContentTypeHandler razorContentTypeHandler = ServiceManager.GetService<IContentTypeHandler>(_textView.TextDataModel.DocumentBuffer) as IRazorContentTypeHandler;
					if (razorContentTypeHandler != null)
					{
						_razorCodeGenerator = (razorContentTypeHandler.RazorCodeGenerator as RazorCodeGenerator);
					}
				}
				return _razorCodeGenerator;
			}
		}

		private RazorSmartIndenter(ITextView textView)
		{
			_textView = textView;
		}

		internal static ISmartIndent Create(ITextView textView)
		{
			return new RazorSmartIndenter(textView);
		}

		private bool IsSmartIndentEnabled()
		{
			return HtmlSettings.IndentStyle == IndentStyle.Smart;
		}

		public System.Web.Razor.Parser.SyntaxTree.Span LocateOwner(Block root, TextChange change, Stack<Block> parentChain)
		{
			System.Web.Razor.Parser.SyntaxTree.Span span = null;
			parentChain.Push(root);
			foreach (SyntaxTreeNode current in root.Children)
			{
				if (current.Start.AbsoluteIndex > change.OldPosition)
				{
					break;
				}
				if (current.Start.AbsoluteIndex + current.Length >= change.OldPosition)
				{
					if (current.IsBlock)
					{
						Block block = current as Block;
						if (current.Start.AbsoluteIndex + current.Length == change.OldPosition)
						{
							System.Web.Razor.Parser.SyntaxTree.Span span2 = block.FindLastDescendentSpan();
							if (span2.EditHandler.OwnsChange(span2, change))
							{
								span = span2;
								break;
							}
						}
						else
						{
							span = LocateOwner(block, change, parentChain);
							if (span != null)
							{
								break;
							}
						}
					}
					else
					{
						System.Web.Razor.Parser.SyntaxTree.Span span3 = current as System.Web.Razor.Parser.SyntaxTree.Span;
						if (span3.EditHandler.OwnsChange(span3, change))
						{
							span = span3;
							break;
						}
					}
				}
			}
			return span;
		}

		[SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "This catch clause is intended to catch all exceptions")]
		int? ISmartIndent.GetDesiredIndentation(ITextSnapshotLine line)
		{
			int? result = null;
			try
			{
				result = GetDesiredIndentationHelper(line);
			}
			catch
			{
			}
			return result;
		}

		private int? GetDesiredIndentationHelper(ITextSnapshotLine line)
		{
			int? num = null;
			if (IsSmartIndentEnabled() && line.LineNumber > 0 && RazorCodeGenerator != null)
			{
				ITextSnapshotLine lineFromLineNumber = line.Snapshot.GetLineFromLineNumber(line.LineNumber - 1);
				Block document = RazorCodeGenerator.Document;
				System.Web.Razor.Text.ITextBuffer textBuffer = new ShimTextBufferAdapter(lineFromLineNumber.Snapshot);
				TextChange change = new TextChange(lineFromLineNumber.End, 0, textBuffer, lineFromLineNumber.End, 0, textBuffer);
				Stack<Block> stack = new Stack<Block>();
				System.Web.Razor.Parser.SyntaxTree.Span span = LocateOwner(document, change, stack);
				if (span.Kind != SpanKind.Code)
				{
					SyntaxTreeNode syntaxTreeNode = span;
					while (stack.Count > 0 && !num.HasValue)
					{
						Block block = stack.Pop();
						List<SyntaxTreeNode> list = new List<SyntaxTreeNode>(block.Children);
						for (int i = 0; i < list.Count; i++)
						{
							SyntaxTreeNode syntaxTreeNode2 = list[i];
							if (!syntaxTreeNode2.IsBlock)
							{
								System.Web.Razor.Parser.SyntaxTree.Span span2 = syntaxTreeNode2 as System.Web.Razor.Parser.SyntaxTree.Span;
								if (span2.Kind == SpanKind.MetaCode)
								{
									ITextSnapshotLine lineFromLineNumber2 = line.Snapshot.GetLineFromLineNumber(span2.Start.LineIndex);
									int num2 = 0;
									if (i < list.Count - 1)
									{
										SyntaxTreeNode syntaxTreeNode3 = list[i + 1];
										if (syntaxTreeNode3.IsBlock && (syntaxTreeNode3 as Block).Type == BlockType.Markup)
										{
											num2 = _textView.Options.GetOptionValue<int>(DefaultOptions.IndentSizeOptionId);
										}
									}
									num = new int?(GetIndentLevelOfLine(lineFromLineNumber2) + num2);
								}
							}
							if (syntaxTreeNode2 == syntaxTreeNode)
							{
								break;
							}
						}
						syntaxTreeNode = block;
					}
				}
			}
			if (BaseIndenter != null)
			{
				int? desiredIndentation = BaseIndenter.GetDesiredIndentation(line);
				if (desiredIndentation.HasValue && (!num.HasValue || desiredIndentation > num))
				{
					num = desiredIndentation;
				}
			}
			return num;
		}

		private int GetIndentLevelOfLine(ITextSnapshotLine line)
		{
			int optionValue = _textView.Options.GetOptionValue<int>(DefaultOptions.TabSizeOptionId);
			int num = 0;
			string text = line.GetText();
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (!char.IsWhiteSpace(c))
				{
					break;
				}
				if (c == '\t')
				{
					num += optionValue;
				}
				else
				{
					num++;
				}
			}
			return num;
		}

		void IDisposable.Dispose()
		{
		}
	}
}
