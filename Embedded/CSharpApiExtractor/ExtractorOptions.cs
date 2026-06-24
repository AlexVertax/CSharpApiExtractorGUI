using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CSharpApiExtractor
{
    public sealed class ExtractorOptions
    {
        public Collection<string> SourcePaths { get; } = new Collection<string>();
        public Collection<string> ExcludedPaths { get; } = new Collection<string>();
        public bool IncludeInternalMembers { get; set; } = true;
        public bool IncludeMissedItems { get; set; } = true;

        internal string[] GetNormalizedExcludedPaths()
        {
            return ExcludedPaths
                .Select(path => Path.GetFullPath(path).Replace('\\', '/').ToLowerInvariant())
                .ToArray();
        }
    }
}
