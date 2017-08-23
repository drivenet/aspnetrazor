using System;
using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.Html.Editor.ContainedLanguage.Razor.Def;
using Microsoft.VisualStudio.Html.Package.Workspace;
using Microsoft.VisualStudio.Text;
using Microsoft.Web.Editor.EditorHelpers;

namespace AspNet.Razor_vHalfNext
{
    internal sealed class RazorVersionDetector : IRazorVersionDetector
    {
        private readonly IRazorVersionDetector _inner;

        private static readonly Lazy<MethodInfo> _getExplicitWebPagesVersionMethod = new Lazy<MethodInfo>(CreateGetExplicitWebPagesVersionMethod);

        private static readonly ConcurrentDictionary<Type, MethodInfo> _setCachedVersionMethodCache = new ConcurrentDictionary<Type, MethodInfo>();

        public RazorVersionDetector(IRazorVersionDetector razorVersionDetector)
        {
            _inner = razorVersionDetector;
        }

        private const string RazorSupportedRuntimeVersion2 = "RazorSupportedRuntimeVersion2";

        public Version GetVersion(ITextBuffer textBuffer)
        {
            Version version = null;
            if (textBuffer != null)
            {
                bool fromCache = false;
                foreach (var buffer in textBuffer.GetContributingBuffers())
                {
                    if (buffer.Properties.TryGetProperty(RazorSupportedRuntimeVersion2, out version))
                    {
                        fromCache = true;
                        break;
                    }
                }
                if (!fromCache)
                {
                    var getExplicitWebPagesVersionMethod = _getExplicitWebPagesVersionMethod.Value;
                    if (getExplicitWebPagesVersionMethod != null)
                    {
                        var fileName = textBuffer.GetFileName();
                        var projectData = Microsoft.VisualStudio.Html.Package.Project.ProjectData.FromFileNameAndPath(fileName);
                        if (projectData != null)
                        {
                            var absolutePath = VsWebUrl.CreateAppUrl(projectData.Hierarchy, projectData.ItemID).AbsolutePath;
                            try
                            {
                                version = (Version)getExplicitWebPagesVersionMethod.Invoke(null, new object[] { absolutePath });
                            }
                            catch
                            {
                            }
                        }
                    }
                    textBuffer.Properties[RazorSupportedRuntimeVersion2] = version;
                }
                if (version != null)
                {
                    if (_inner != null)
                    {
                        var innerType = _inner.GetType();
                        var setCachedVersionMethod = _setCachedVersionMethodCache.GetOrAdd(innerType,
                            type => type.GetMethod("SetCachedVersion", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(ITextBuffer), typeof(Version) }, null));
                        if (setCachedVersionMethod != null)
                        {
                            try
                            {
                                setCachedVersionMethod.Invoke(_inner, new object[] { textBuffer, version });
                            }
                            catch (TargetInvocationException)
                            {
                            }
                        }
                    }
                    return version;
                }
            }
            if (_inner != null)
                version = _inner.GetVersion(textBuffer);
            return version;
        }

        private static MethodInfo CreateGetExplicitWebPagesVersionMethod()
        {
            var type = Type.GetType("System.Web.WebPages.Deployment.WebPagesDeployment, System.Web.WebPages.Deployment", false);
            var method = type?.GetMethod("GetExplicitWebPagesVersion", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null);
            return method;
        }
    }
}
