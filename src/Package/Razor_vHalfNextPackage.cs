using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.WebTools.Languages.Shared.Editor.Host;

namespace AspNet.Razor_vHalfNext
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.1")]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class Razor_vHalfNextPackage : AsyncPackage
    {
        public const string PackageGuidString = "bbe31384-95c9-4558-b44e-90d55b7a36b4";

        protected override System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
            => JoinableTaskFactory.StartOnIdle(() => InitializeHost()).Task;

        private static void InitializeHost()
        {
            var defaultHost = WebEditor.Host;
            var decoratedHost = new RazorVersionDetectorOverridingWebHost(defaultHost);
            WebEditor.RemoveHost(defaultHost);
            WebEditor.SetHost(decoratedHost);
        }
    }
}
