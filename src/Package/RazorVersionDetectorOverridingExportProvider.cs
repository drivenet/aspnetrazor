using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;

using Microsoft.Html.Editor.ContainedLanguage.Razor.Def;

namespace AspNet.Razor_vHalfNext
{
    internal sealed class RazorVersionDetectorOverridingExportProvider : ExportProvider
    {
        private readonly ExportProvider _inner;

        private static readonly string IRazorVersionDetectorName = typeof(IRazorVersionDetector).ToString();

        private readonly IEnumerable<Export> _razorVersionDetectorExports;

        private object DecorateRazorVersionDetector()
        {
            var detector = _inner.GetExportedValueOrDefault<IRazorVersionDetector>();
            detector = new RazorVersionDetector(detector);
            return detector;
        }

        public RazorVersionDetectorOverridingExportProvider(ExportProvider exportProvider)
        {
            _inner = exportProvider;
            _razorVersionDetectorExports = new[]
            {
                new Export(IRazorVersionDetectorName, DecorateRazorVersionDetector)
            };
        }

        protected override IEnumerable<Export> GetExportsCore(ImportDefinition definition, AtomicComposition atomicComposition)
        {
            if (definition.ContractName == IRazorVersionDetectorName)
                return _razorVersionDetectorExports;
            return _inner.GetExports(definition, atomicComposition);
        }
    }
}
