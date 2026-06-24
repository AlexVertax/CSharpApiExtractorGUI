using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace CSharpApiExtractor
{
    internal static class RoslynAssemblyResolver
    {
        private static readonly string[] ManagedAssemblyNames =
        {
            "Microsoft.CodeAnalysis",
            "Microsoft.CodeAnalysis.CSharp"
        };

        private static bool _registered;

        internal static void Register()
        {
            if (_registered)
            {
                return;
            }

            AssemblyLoadContext.Default.Resolving += ResolveManagedAssembly;
            _registered = true;
        }

        private static Assembly? ResolveManagedAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            if (!ManagedAssemblyNames.Contains(assemblyName.Name, StringComparer.Ordinal))
            {
                return null;
            }

            string candidatePath = Path.Combine(AppContext.BaseDirectory, string.Format("{0}.dll", assemblyName.Name));
            if (File.Exists(candidatePath))
            {
                return context.LoadFromAssemblyPath(candidatePath);
            }

            return null;
        }
    }
}
