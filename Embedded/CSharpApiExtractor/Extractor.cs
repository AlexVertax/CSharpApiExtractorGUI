using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpApiExtractor
{
    public sealed class Extractor
    {
        private static readonly MetadataReference[] MetadataReferences =
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location)
        };

        private readonly ExtractorOptions _options;
        private readonly ConcurrentDictionary<string, ApiNamespace> _namespaces =
            new ConcurrentDictionary<string, ApiNamespace>(StringComparer.Ordinal);
        private readonly List<string> _missedItems = new List<string>();
        private string[] _normalizedExcludedPaths = Array.Empty<string>();

        static Extractor()
        {
            RoslynAssemblyResolver.Register();
        }

        public Extractor(ExtractorOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Extractor(params string[] sourcePaths)
            : this(new ExtractorOptions())
        {
            foreach (string sourcePath in sourcePaths)
            {
                _options.SourcePaths.Add(sourcePath);
            }
        }

        public async Task<ApiDocument> ExtractAsync(CancellationToken cancellationToken = default)
        {
            if (_options.SourcePaths.Count == 0)
            {
                throw new InvalidOperationException("At least one source path must be provided.");
            }

            _namespaces.Clear();
            _missedItems.Clear();
            _normalizedExcludedPaths = _options.GetNormalizedExcludedPaths();

            foreach (string sourcePath in _options.SourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExtractFromPathAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            }

            ApiDocument document = new ApiDocument();
            foreach (ApiNamespace apiNamespace in _namespaces.Values.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                apiNamespace.Types.Sort(CompareTypes);
                foreach (ApiType apiType in apiNamespace.Types)
                {
                    apiType.Members.Sort(CompareMembers);
                }

                document.Namespaces.Add(apiNamespace);
            }

            if (_options.IncludeMissedItems)
            {
                document.MissedItems.AddRange(
                    _missedItems.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal));
            }

            return document;
        }

        private static int CompareMembers(ApiMember left, ApiMember right)
        {
            if (string.Equals(left.Kind, "enum", StringComparison.Ordinal) &&
                string.Equals(right.Kind, "enum", StringComparison.Ordinal))
            {
                return left.SortOrder.CompareTo(right.SortOrder);
            }

            int result = StringComparer.Ordinal.Compare(left.Kind, right.Kind);
            if (result != 0)
            {
                return result;
            }

            result = StringComparer.Ordinal.Compare(left.Name, right.Name);
            return result != 0 ? result : StringComparer.Ordinal.Compare(left.Declaration, right.Declaration);
        }

        private static int CompareTypes(ApiType left, ApiType right)
        {
            int result = StringComparer.Ordinal.Compare(left.Name, right.Name);
            return result != 0 ? result : StringComparer.Ordinal.Compare(left.Declaration, right.Declaration);
        }

        private bool CheckAccessibility(Microsoft.CodeAnalysis.Accessibility accessibility)
        {
            return accessibility == Microsoft.CodeAnalysis.Accessibility.Public ||
                   (_options.IncludeInternalMembers && accessibility == Microsoft.CodeAnalysis.Accessibility.Internal);
        }

        private async Task ExtractFromPathAsync(string folderPath, CancellationToken cancellationToken)
        {
            string fullPath = Path.GetFullPath(folderPath);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException(string.Format("Directory not found: {0}", fullPath));
            }

            string[] csFiles = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);
            foreach (string filePath in csFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsExcluded(filePath))
                {
                    continue;
                }

                await ProcessFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
        }

        private ApiType? GetContainingType(SemanticModel model, SyntaxNode node)
        {
            MemberDeclarationSyntax? parent = node.Parent as MemberDeclarationSyntax;
            if (parent == null)
            {
                return null;
            }

            INamedTypeSymbol? symbol = model.GetDeclaredSymbol(parent) as INamedTypeSymbol;
            if (symbol == null)
            {
                return null;
            }

            ApiNamespace apiNamespace = GetNamespace(symbol);
            return GetOrCreateType(apiNamespace, symbol, true);
        }

        private static string GetFieldSummary(BaseFieldDeclarationSyntax node)
        {
            SyntaxTrivia xmlTrivia = node.GetLeadingTrivia().FirstOrDefault(trivia =>
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            if (xmlTrivia == default)
            {
                return string.Empty;
            }

            try
            {
                XDocument document = XDocument.Parse("<root>" + xmlTrivia.ToFullString() + "</root>");
                return DocumentationCommentParser.GetSummary(document.Descendants("summary").FirstOrDefault());
            }
            catch
            {
                return string.Empty;
            }
        }

        private ApiNamespace GetNamespace(INamedTypeSymbol symbol)
        {
            string name = symbol.ContainingNamespace?.ToString() ?? string.Empty;
            return _namespaces.GetOrAdd(name, namespaceName => new ApiNamespace { Name = namespaceName });
        }

        private ApiType? GetOrCreateType(ApiNamespace apiNamespace, INamedTypeSymbol symbol, bool allowMissingSummary = false)
        {
            string fullName = symbol.ToDisplayString();
            string typeName = string.IsNullOrEmpty(apiNamespace.Name)
                ? fullName
                : fullName.Substring(apiNamespace.Name.Length + 1);

            ApiType? existing = apiNamespace.Types.FirstOrDefault(item => string.Equals(item.Name, typeName, StringComparison.Ordinal));
            if (existing != null)
            {
                return existing;
            }

            string summary = DocumentationCommentParser.GetSummary(symbol);
            if (!allowMissingSummary && string.IsNullOrWhiteSpace(summary))
            {
                TrackMissedItem(fullName);
                return null;
            }

            ApiType created = new ApiType
            {
                Name = typeName,
                Kind = symbol.TypeKind.ToString().ToLowerInvariant(),
                Declaration = fullName,
                Summary = summary,
                IsStatic = symbol.IsStatic,
                IsAbstract = symbol.IsAbstract,
                Access = symbol.DeclaredAccessibility.ToString().ToLowerInvariant(),
                BaseType = GetBaseType(symbol)
            };

            foreach (string @interface in symbol.Interfaces
                         .Select(item => item.ToDisplayString())
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(item => item, StringComparer.Ordinal))
            {
                created.Interfaces.Add(@interface);
            }

            apiNamespace.Types.Add(created);
            return created;
        }

        private static string? GetBaseType(INamedTypeSymbol symbol)
        {
            INamedTypeSymbol? baseType = symbol.BaseType;
            if (baseType == null)
            {
                return null;
            }

            string name = baseType.ToDisplayString();
            if (string.Equals(name, "object", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "System.Object", StringComparison.Ordinal))
            {
                return null;
            }

            return name;
        }

        private bool IsExcluded(string filePath)
        {
            string normalized = Path.GetFullPath(filePath).Replace('\\', '/').ToLowerInvariant();
            return _normalizedExcludedPaths.Any(normalized.StartsWith);
        }

        private async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken)
        {
            string fileContent = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

            SyntaxTree tree = CSharpSyntaxTree.ParseText(fileContent, cancellationToken: cancellationToken, path: filePath);
            SyntaxNode root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName: "ApiReferenceExtraction",
                syntaxTrees: new[] { tree },
                references: MetadataReferences);

            SemanticModel model = compilation.GetSemanticModel(tree);
            IEnumerable<MemberDeclarationSyntax> nodes = root.DescendantNodes().OfType<MemberDeclarationSyntax>();

            foreach (MemberDeclarationSyntax node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProcessNode(model, node);
            }
        }

        private void ProcessDelegate(SemanticModel model, MemberDeclarationSyntax node, ITypeSymbol symbol)
        {
            string declaration = symbol.ToDisplayString();
            if (string.IsNullOrWhiteSpace(declaration))
            {
                return;
            }

            string summary = DocumentationCommentParser.GetSummary(symbol);
            if (string.IsNullOrWhiteSpace(summary))
            {
                TrackMissedItem(symbol.ToDisplayString());
                return;
            }

            DelegateDeclarationSyntax? delegateNode = node as DelegateDeclarationSyntax;
            ApiMember member = new ApiMember
            {
                Name = symbol.Name,
                Kind = "delegate",
                Declaration = declaration,
                Summary = summary,
                IsStatic = symbol.IsStatic,
                Access = symbol.DeclaredAccessibility.ToString().ToLowerInvariant(),
                ValueType = delegateNode?.ReturnType.ToString() ?? "void",
                SortOrder = node.SpanStart,
                IsVirtual = symbol.IsVirtual,
                IsAbstract = symbol.IsAbstract
            };

            if (delegateNode != null)
            {
                foreach (ParameterSyntax parameter in delegateNode.ParameterList.Parameters)
                {
                    member.Parameters.Add(new ApiParameter
                    {
                        Name = parameter.Identifier.Text,
                        Type = parameter.Type?.ToString() ?? string.Empty
                    });
                }
            }

            ApiType? type = GetContainingType(model, node);
            if (type != null)
            {
                type.Members.Add(member);
            }
        }

        private void ProcessEnumField(SemanticModel model, EnumMemberDeclarationSyntax node, ISymbol symbol)
        {
            string declaration = symbol.ToDisplayString();
            if (string.IsNullOrWhiteSpace(declaration))
            {
                return;
            }

            string summary = DocumentationCommentParser.GetSummary(symbol);
            if (string.IsNullOrWhiteSpace(summary))
            {
                TrackMissedItem(symbol.ToDisplayString());
                return;
            }

            ApiMember member = new ApiMember
            {
                Name = symbol.Name,
                Kind = "enum",
                Declaration = declaration,
                Summary = summary,
                IsStatic = symbol.IsStatic,
                Access = symbol.DeclaredAccessibility.ToString().ToLowerInvariant(),
                SortOrder = node.SpanStart
            };

            ApiType? type = GetContainingType(model, node);
            if (type != null)
            {
                type.Members.Add(member);
            }
        }

        private void ProcessField(SemanticModel model, BaseFieldDeclarationSyntax node)
        {
            if (!node.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)) &&
                !(_options.IncludeInternalMembers && node.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.InternalKeyword))))
            {
                return;
            }

            string summary = GetFieldSummary(node);
            if (string.IsNullOrWhiteSpace(summary))
            {
                foreach (VariableDeclaratorSyntax variable in node.Declaration.Variables)
                {
                    IFieldSymbol? fieldSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                    TrackMissedItem(fieldSymbol?.ToDisplayString() ?? variable.Identifier.Text);
                }

                return;
            }

            ApiType? type = GetContainingType(model, node);
            if (type == null)
            {
                return;
            }

            foreach (VariableDeclaratorSyntax variable in node.Declaration.Variables)
            {
                type.Members.Add(new ApiMember
                {
                    Name = variable.Identifier.Text,
                    Kind = "field",
                    Declaration = node.ToString(),
                    Summary = summary,
                    IsStatic = node.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)),
                    ValueType = node.Declaration.Type.ToString(),
                    Access = string.Join(" ", node.Modifiers.Select(modifier => modifier.ValueText)),
                    SortOrder = variable.SpanStart
                });
            }
        }

        private void ProcessMethod(SemanticModel model, MemberDeclarationSyntax node, IMethodSymbol symbol)
        {
            if (symbol.IsOverride ||
                symbol.MethodKind == MethodKind.PropertyGet ||
                symbol.MethodKind == MethodKind.PropertySet ||
                symbol.MethodKind == MethodKind.EventAdd ||
                symbol.MethodKind == MethodKind.EventRemove)
            {
                return;
            }

            string declaration = symbol.ToDisplayString();
            if (string.IsNullOrWhiteSpace(declaration))
            {
                return;
            }

            ApiMember member = new ApiMember
            {
                Name = symbol.Name,
                Kind = "method",
                Declaration = declaration,
                IsStatic = symbol.IsStatic,
                IsGeneric = symbol.IsGenericMethod,
                ValueType = symbol.ReturnType.ToDisplayString(),
                Access = symbol.DeclaredAccessibility.ToString().ToLowerInvariant(),
                SortOrder = node.SpanStart,
                IsVirtual = symbol.IsVirtual,
                IsAbstract = symbol.IsAbstract
            };

            foreach (IParameterSymbol parameter in symbol.Parameters)
            {
                string? defaultValue = null;
                if (parameter.HasExplicitDefaultValue)
                {
                    defaultValue = parameter.ExplicitDefaultValue?.ToString();
                    if (parameter.ExplicitDefaultValue is bool boolValue)
                    {
                        defaultValue = boolValue.ToString().ToLowerInvariant();
                    }
                }

                member.Parameters.Add(new ApiParameter
                {
                    Name = parameter.Name,
                    Type = parameter.Type.ToDisplayString(),
                    DefaultValue = defaultValue,
                    IsOptional = parameter.IsOptional,
                    RefKind = parameter.RefKind.ToString().ToLowerInvariant()
                });
            }

            DocumentationCommentParser.PopulateMemberSummary(member, symbol.GetDocumentationCommentXml());
            if (string.IsNullOrWhiteSpace(member.Summary))
            {
                TrackMissedItem(symbol.ToDisplayString());
                return;
            }

            ApiType? type = GetContainingType(model, node);
            if (type != null)
            {
                type.Members.Add(member);
            }
        }

        private void ProcessNode(SemanticModel model, MemberDeclarationSyntax node)
        {
            if (node is NamespaceDeclarationSyntax || node is FileScopedNamespaceDeclarationSyntax)
            {
                return;
            }

            ISymbol? symbol = model.GetDeclaredSymbol(node);
            if (symbol != null)
            {
                ProcessSymbol(model, node, symbol);
                return;
            }

            BaseFieldDeclarationSyntax? fieldNode = node as BaseFieldDeclarationSyntax;
            if (fieldNode != null)
            {
                ProcessField(model, fieldNode);
            }
        }

        private void ProcessProperty(SemanticModel model, MemberDeclarationSyntax node, IPropertySymbol symbol)
        {
            if (symbol.IsOverride)
            {
                return;
            }

            string declaration = symbol.ToDisplayString();
            if (string.IsNullOrWhiteSpace(declaration))
            {
                return;
            }

            ApiMember member = new ApiMember
            {
                Name = symbol.IsIndexer ? "this[]" : symbol.Name,
                Kind = symbol.IsIndexer ? "indexer" : "property",
                Declaration = declaration,
                Summary = DocumentationCommentParser.GetSummary(symbol),
                IsStatic = symbol.IsStatic,
                IsAbstract = symbol.IsAbstract,
                IsVirtual = symbol.IsVirtual,
                ValueType = symbol.Type.ToDisplayString(),
                HasGet = symbol.GetMethod != null && CheckAccessibility(symbol.GetMethod.DeclaredAccessibility),
                HasSet = symbol.SetMethod != null && CheckAccessibility(symbol.SetMethod.DeclaredAccessibility),
                Access = symbol.DeclaredAccessibility.ToString().ToLowerInvariant(),
                SortOrder = node.SpanStart
            };

            foreach (IParameterSymbol parameter in symbol.Parameters)
            {
                member.Parameters.Add(new ApiParameter
                {
                    Name = parameter.Name,
                    Type = parameter.Type.ToDisplayString(),
                    DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue?.ToString() : null,
                    IsOptional = parameter.IsOptional,
                    RefKind = parameter.RefKind.ToString().ToLowerInvariant()
                });
            }

            DocumentationCommentParser.PopulateMemberSummary(member, symbol.GetDocumentationCommentXml());
            if (string.IsNullOrWhiteSpace(member.Summary))
            {
                TrackMissedItem(symbol.ToDisplayString());
                return;
            }

            ApiType? type = GetContainingType(model, node);
            if (type != null)
            {
                type.Members.Add(member);
            }
        }

        private void ProcessSymbol(SemanticModel model, MemberDeclarationSyntax node, ISymbol symbol)
        {
            if (!CheckAccessibility(symbol.DeclaredAccessibility))
            {
                return;
            }

            INamedTypeSymbol? namedTypeSymbol = symbol as INamedTypeSymbol;
            if (namedTypeSymbol != null && symbol is ITypeSymbol typeSymbol && typeSymbol.TypeKind == TypeKind.Delegate)
            {
                ProcessDelegate(model, node, typeSymbol);
                return;
            }

            if (namedTypeSymbol != null)
            {
                GetOrCreateType(GetNamespace(namedTypeSymbol), namedTypeSymbol);
                return;
            }

            IMethodSymbol? methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                ProcessMethod(model, node, methodSymbol);
                return;
            }

            IPropertySymbol? propertySymbol = symbol as IPropertySymbol;
            if (propertySymbol != null)
            {
                ProcessProperty(model, node, propertySymbol);
                return;
            }

            if (symbol is IFieldSymbol && node is EnumMemberDeclarationSyntax enumNode)
            {
                ProcessEnumField(model, enumNode, symbol);
            }
        }

        private void TrackMissedItem(string item)
        {
            if (!_options.IncludeMissedItems || string.IsNullOrWhiteSpace(item))
            {
                return;
            }

            _missedItems.Add(item);
        }
    }
}
