using System;
using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.VisualStudio.Text;
using Microsoft.WebTools.Languages.Html.Editor.ContainedLanguage.Razor.Def;

namespace AspNet.Razor_vHalfNext
{
    internal sealed class RazorVersionDetector : IRazorVersionDetector
    {
        private static readonly Version OldVersion = new Version(3, 0, 0, 0);

        private static readonly Version NewVersion = new Version(SupportedRuntime.Version);

        private readonly IRazorVersionDetector _inner;

        private static readonly ConcurrentDictionary<Type, MethodInfo> _setCachedVersionMethodCache = new ConcurrentDictionary<Type, MethodInfo>();

        public RazorVersionDetector(IRazorVersionDetector razorVersionDetector)
        {
            _inner = razorVersionDetector ?? throw new ArgumentNullException(nameof(razorVersionDetector));
        }

        public Version GetVersion(ITextBuffer textBuffer)
        {
            var version = _inner.GetVersion(textBuffer);
            if (version == OldVersion)
            {
                version = NewVersion;
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
}
