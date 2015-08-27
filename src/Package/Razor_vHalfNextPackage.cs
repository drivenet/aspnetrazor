using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Web.Editor.Host;

namespace AspNet.Razor_vHalfNext
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.1")]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(UIContextGuids.SolutionExists)]
    public sealed class Razor_vHalfNextPackage : Package
    {
        public const string PackageGuidString = "bbe31384-95c9-4558-b44e-90d55b7a36b4";

        #region Package Members
        protected override void Initialize()
        {
            base.Initialize();
            var defaultHost = WebEditor.Host;
            var decoratedHost = new RazorVersionDetectorOverridingWebHost(defaultHost);
            WebEditor.RemoveHost(defaultHost);
            WebEditor.SetHost(decoratedHost);
        }
        #endregion
    }
}
