using System;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Web.Configuration;
using System.Web.Razor;
using System.Web.Razor.Text;
using System.Web.WebPages.Razor;
using System.Web.WebPages.Razor.Configuration;

using Microsoft.WebTools.Languages.Html.Editor.Settings;
using Microsoft.WebTools.Languages.Shared.Formatting;

namespace AspNet.Razor_vHalfNext
{
    internal class ShimRazorEditorParserImpl
	{
		private Type _codeDomProviderType;

		public event EventHandler<DocumentParseCompleteEventArgs> DocumentParseComplete;

		internal RazorEditorParser RazorEditorParser
		{
			get;
			private set;
		}

		public Type CodeDomProviderType
		{
			get
			{
				return _codeDomProviderType;
			}
		}

		public ShimRazorEditorParserImpl(string virtualPath, string physicalPath)
		{
			WebPageRazorHost razorWebPageRazorHost = ShimRazorEditorParserImpl.GetRazorWebPageRazorHost(virtualPath, physicalPath);
			HtmlSettings.Changed += OnSettingsChanged;
			razorWebPageRazorHost.DesignTimeMode = true;
			OnSettingsChanged(razorWebPageRazorHost);
			_codeDomProviderType = razorWebPageRazorHost.CodeLanguage.CodeDomProviderType;
			RazorEditorParser = new RazorEditorParser(razorWebPageRazorHost, physicalPath);
			RazorEditorParser.DocumentParseComplete += OnDocumentParseComplete;
		}

		private void OnSettingsChanged(object sender, EventArgs e)
		{
			if (RazorEditorParser != null)
			{
				OnSettingsChanged(RazorEditorParser.Host);
			}
		}

		[SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "TODO")]
		private static WebPageRazorHost GetRazorWebPageRazorHost(string virtualPath, string physicalPath)
		{
			WebPageRazorHost webPageRazorHost = null;
			try
			{
				string physicalDirectory = physicalPath.Substring(0, physicalPath.Length - virtualPath.Length);
				string text = virtualPath.Replace('\\', '/');
				if (!text.StartsWith("/", StringComparison.Ordinal))
				{
					text = "/" + text;
				}
				int num = text.LastIndexOf('/');
				text = text.Substring(0, (num == 0) ? 1 : num);
				WebConfigurationFileMap arg_62_0 = new WebConfigurationFileMap();
				VirtualDirectoryMapping mapping = new VirtualDirectoryMapping(physicalDirectory, true);
				arg_62_0.VirtualDirectories.Add("/", mapping);
				Configuration configuration = WebConfigurationManager.OpenMappedWebConfiguration(arg_62_0, text);
				if (configuration != null)
				{
					RazorWebSectionGroup razorWebSectionGroup = (RazorWebSectionGroup)configuration.GetSectionGroup(RazorWebSectionGroup.GroupName);
					if (razorWebSectionGroup != null)
					{
						webPageRazorHost = WebRazorHostFactory.CreateHostFromConfig(razorWebSectionGroup, virtualPath, physicalPath);
					}
				}
			}
			catch (Exception)
			{
			}
			if (webPageRazorHost == null)
			{
				webPageRazorHost = WebRazorHostFactory.CreateDefaultHost(virtualPath, physicalPath);
			}
			return webPageRazorHost;
		}

		public PartialParseResult CheckForStructureChanges(TextChange textChange)
		{
			return RazorEditorParser.CheckForStructureChanges(textChange);
		}

		public string GetAutoCompleteString()
		{
			return RazorEditorParser.GetAutoCompleteString();
		}

		public void Close()
		{
			HtmlSettings.Changed -= OnSettingsChanged;
			((IDisposable)RazorEditorParser).Dispose();
			RazorEditorParser = null;
		}

		public void OnDocumentParseComplete(object sender, DocumentParseCompleteEventArgs args)
		{
			EventHandler<DocumentParseCompleteEventArgs> documentParseComplete = DocumentParseComplete;
			if (documentParseComplete != null)
			{
				documentParseComplete(this, args);
			}
		}

		private void OnSettingsChanged(RazorEngineHost razorWebPageRazorHost)
		{
			razorWebPageRazorHost.IsIndentingWithTabs = (HtmlSettings.IndentType == IndentType.Tabs);
			razorWebPageRazorHost.TabSize = HtmlSettings.TabSize;
		}
	}
}
