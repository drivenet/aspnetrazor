using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.Web.Editor;
using Microsoft.WebTools.Languages.Shared.Editor.Host;
using Microsoft.WebTools.Languages.Shared.Editor.Undo;

namespace AspNet.Razor_vHalfNext
{
    internal sealed class RazorVersionDetectorOverridingWebHost : IWebEditorHost
    {
        private readonly IWebEditorHost _inner;

        private readonly ExportProvider _exportProvider;

        public RazorVersionDetectorOverridingWebHost(IWebEditorHost host)
        {
            _inner = host;
            _exportProvider = new RazorVersionDetectorOverridingExportProvider(_inner.ExportProvider);
        }

        public ICompositionService CompositionService => _inner.CompositionService;

        public ExportProvider ExportProvider => _exportProvider;

        public int LocaleId => _inner.LocaleId;

        public Thread MainThread => _inner.MainThread;

        public string SchemasFolder => _inner.SchemasFolder;

        public System.IServiceProvider ServiceProvider => _inner.ServiceProvider;

        public string UserFolder => _inner.UserFolder;

        public JoinableTaskFactory JoinableTaskFactory => _inner.JoinableTaskFactory;

        public event EventHandler<EventArgs> Idle
        {
            add
            {
                _inner.Idle += value;
            }

            remove
            {
                _inner.Idle -= value;
            }
        }

        public event EventHandler<EventArgs> Terminating
        {
            add
            {
                _inner.Terminating += value;
            }

            remove
            {
                _inner.Terminating -= value;
            }
        }

        public bool CheckAccess() => _inner.CheckAccess();

        public ICompoundUndoAction CreateCompoundAction(Microsoft.VisualStudio.Text.Editor.ITextView textView, ITextBuffer textBuffer) => _inner.CreateCompoundAction(textView, textBuffer);

        public void DispatchOnUIThread(Action action, System.Windows.Threading.DispatcherPriority priority) => _inner.DispatchOnUIThread(action, priority);

        public Task<ICompositionService> GetCompositionServiceAsync() => _inner.GetCompositionServiceAsync();

        public Uri GetFileName(Microsoft.VisualStudio.Text.Editor.ITextView textView) => _inner.GetFileName(textView);

        public Uri GetFileName(ITextBuffer textBuffer) => _inner.GetFileName(textBuffer);

        public bool NavigateToFile(Uri uri, int selectStart, int selectLength, bool allowProvisionalTab) => _inner.NavigateToFile(uri, selectStart, selectLength, allowProvisionalTab);

        public bool NavigateToTextBuffer(ITextBuffer textBuffer, int selectStart, int selectLength) => _inner.NavigateToTextBuffer(textBuffer, selectStart, selectLength);

        public void ShowErrorMessage(string message) => _inner.ShowErrorMessage(message);

        public bool ShowHelp(string topicName) => _inner.ShowHelp(topicName);

        public void ThrowIfNotOnUIThread(string callerMemberName = "") => _inner.ThrowIfNotOnUIThread(callerMemberName);

        public void TraceEvent(int eventId, object parameter) => _inner.TraceEvent(eventId, parameter);

        public Task TraceEventAsync(int eventId, object parameter) => _inner.TraceEventAsync(eventId, parameter);

        public ICommandTarget TranslateCommandTarget(Microsoft.VisualStudio.Text.Editor.ITextView textView, object commandTarget) => _inner.TranslateCommandTarget(textView, commandTarget);

        public object TranslateToHostCommandTarget(Microsoft.VisualStudio.Text.Editor.ITextView textView, object commandTarget) => _inner.TranslateToHostCommandTarget(textView, commandTarget);
    }
}
