using ROBdk97.XmlDocToMd.Cli;
using ROBdk97.XmlDocToMd.Logging;
using System.Text.RegularExpressions;
using System.Xml;

namespace ROBdk97.XmlDocToMd.Conversion;

/// <summary>
/// Primary orchestration class for converting XML documentation to Markdown.
/// Provides convenience overloads for common conversion scenarios and all helper
/// methods used by tag rendering strategies.
/// </summary>
internal static partial class XmlToMarkdown
{
    #region Regex

    [GeneratedRegex("[^a-z0-9\\s-]")]
    private static partial Regex AnchorInvalidCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@">\s+")]
    private static partial Regex BlockQuotePrefixRegex();

    [GeneratedRegex(@"\n\n\n+")]
    private static partial Regex ExcessiveLineBreaksRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex HyphenRunsRegex();

    [GeneratedRegex(@"(^[A-Z]\:)", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixReplacerRegex();

    private static readonly Regex _PrefixReplacerRegex = PrefixReplacerRegex();

    #endregion

    #region Static Data

    /// <summary>
    /// Maps XML doc member-name prefixes to their human-readable kind labels.
    /// </summary>
    private static readonly Dictionary<string, string> _MemberNamePrefixDict = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["F:"] = "Field",
        ["P:"] = "Property",
        ["T:"] = "Type",
        ["E:"] = "Event",
        ["M:"] = "Method",
        ["N:"] = "Namespace",
    };

    /// <summary>
    /// Maps CLR type names to their C# keyword aliases for display purposes.
    /// </summary>
    private static readonly Dictionary<string, string> _types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"]  = "string",
        ["object"]  = "object",
        ["int32"]   = "int",
        ["int64"]   = "long",
        ["boolean"] = "bool",
        ["decimal"] = "decimal",
        ["void"]    = "void",
        ["double"]  = "double",
        ["byte"]    = "byte",
    };

    #endregion

    #region Entry Points

    /// <summary>
    /// Converts an XML documentation string to Markdown with default error handling.
    /// </summary>
    internal static string ToMarkDown(this string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        var context = new ConversionContext
        {
            UnexpectedTagAction = UnexpectedTagActionEnum.Error,
            WarningLogger = new TextWriterWarningLogger(Console.Error)
        };
        return s.ToMarkDown(context);
    }

    /// <summary>
    /// Converts an XML documentation string to Markdown using the provided context.
    /// </summary>
    internal static string ToMarkDown(this string s, ConversionContext context)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(context);

        var xdoc = XDocument.Parse(s);
        return xdoc.ToMarkDown(context).RemoveRedundantLineBreaks();
    }

    /// <summary>
    /// Converts an XML documentation stream to Markdown with default error handling.
    /// </summary>
    internal static string ToMarkDown(this Stream s)
    {
        ArgumentNullException.ThrowIfNull(s);

        var xdoc = XDocument.Load(s);
        var context = new ConversionContext
        {
            UnexpectedTagAction = UnexpectedTagActionEnum.Error,
            WarningLogger = new TextWriterWarningLogger(Console.Error)
        };
        return xdoc.ToMarkDown(context).RemoveRedundantLineBreaks();
    }

    /// <summary>
    /// Recursively converts <paramref name="node"/> and all of its descendants to Markdown.
    /// </summary>
    /// <param name="node">The XML node to convert.</param>
    /// <param name="context">Conversion state threaded through the recursive walk.</param>
    /// <returns>The converted Markdown text.</returns>
    internal static string ToMarkDown(this XNode node, ConversionContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        if (node is XDocument document)
            node = document.Root ?? throw new InvalidOperationException("Document root is null");

        if (node.NodeType == XmlNodeType.Text)
            return WhitespaceRegex().Replace(((XText)node).Value.Replace('\n', ' '), " ");

        if (node.NodeType == XmlNodeType.Element)
        {
            var el = (XElement)node;
            var name = ResolveTagName(el, node, context);

            // Non-public members and other skipped elements return an empty tag name.
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            try
            {
                if (context.Registry is not null)
                    return context.Registry.Render(name, el, context);
                throw new InvalidOperationException("Tag renderer registry is not set in the conversion context.");
            }
            catch (KeyNotFoundException ex)
            {
                var lineInfo = (IXmlLineInfo)node;
                switch (context.UnexpectedTagAction)
                {
                    case UnexpectedTagActionEnum.Error:
                        throw new XmlException(
                            $@"Unknown element type ""{name}""",
                            ex,
                            lineInfo.LineNumber,
                            lineInfo.LinePosition);
                    case UnexpectedTagActionEnum.Warn:
                        context.WarningLogger?
                            .LogWarning(
                                $@"Unknown element type ""{name}"" on line {lineInfo.LineNumber}, pos {lineInfo.LinePosition}");
                        break;
                    case UnexpectedTagActionEnum.Accept:
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected {nameof(UnexpectedTagActionEnum)}");
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Converts a sequence of XML nodes to Markdown by concatenating the result of
    /// each individual <see cref="ToMarkDown(XNode, ConversionContext)"/> call.
    /// </summary>
    internal static string ToMarkDown(this IEnumerable<XNode> es, ConversionContext context)
        => es.Aggregate(string.Empty, (current, x) => current + x.ToMarkDown(context));

    #endregion

    #region Tag Dispatch

    /// <summary>
    /// Resolves the logical tag name for <paramref name="el"/> that will be used to
    /// look up a rendering strategy. Handles special cases such as member-kind
    /// disambiguation, first-of-kind section headers, <c>see</c> variants, and
    /// constructor detection.
    /// </summary>
    /// <param name="el">The element being rendered.</param>
    /// <param name="node">The same element as an <see cref="XNode"/> (for sibling queries).</param>
    /// <param name="context">Current conversion context (used for error handling).</param>
    /// <returns>
    /// The resolved tag name, or <see cref="string.Empty"/> to skip the element
    /// (e.g. for non-public members).
    /// </returns>
    private static string ResolveTagName(XElement el, XNode node, ConversionContext context)
    {
        var name = el.Name.LocalName;

        if (name == "member")
        {
            if (!ReflectionHelper.IsPublic(el))
                return string.Empty;

            if (!_MemberNamePrefixDict.TryGetValue(
                el.Attribute("name")?.Value?[..2] ?? string.Empty,
                out var expandedName))
            {
                expandedName = "none";
            }
            name = expandedName.ToLowerInvariant();
        }
        else if (name == "see")
        {
            if (el.Attribute("langword") != null)
                name = "seeLangword";
            else if (el.Attribute("href") != null)
                name = "seeAnchor";
            else
            {
                var crefAttr = el.Attribute("cref");
                name = crefAttr != null && crefAttr.Value.StartsWith("!:#")
                    ? "seeAnchor"
                    : "seePage";
            }
        }
        else if (name.EndsWith("param") &&
            !name.Equals("typeparam") &&
            node.ElementsBeforeSelf().LastOrDefault()?.Name?.LocalName != "param")
        {
            name = "firstparam";
        }
        else if (name == "typeparam" &&
            node.ElementsBeforeSelf().LastOrDefault()?.Name?.LocalName != "typeparam")
        {
            name = "firsttypeparam";
        }
        else if (name == "seealso" && node.ElementsBeforeSelf().LastOrDefault()?.Name?.LocalName != "seealso")
        {
            name = "firstseealso";
        }
        else if (name == "summary")
        {
            var test = el.Parent?.Attribute("name")?.Value.Contains("F:");
            if (test ?? false)
                name = "fieldsummary";
        }
        else if (name == "remarks" && el.Value == string.Empty)
        {
            name = "nameremarks";
        }

        if (name == "method" && el.Attribute("name")?.Value.Contains("#ctor") == true)
            name = "constructor";

        try
        {
            if (node.NodesBeforeSelf()
                .LastOrDefault(x => x is XElement el && ReflectionHelper.IsPublic(el)) is XElement lastNode)
            {
                var nameAttribute = el.Attribute("name")?.Value ?? string.Empty;
                var lastNameAttribute = lastNode.Attribute("name")?.Value ?? string.Empty;

                if (name == "method" &&
                    nameAttribute.StartsWith("M:") &&
                    (!lastNameAttribute.StartsWith("M:") || lastNameAttribute.Contains("#ctor")))
                {
                    name = "firstmethod";
                }
                if (name == "constructor" &&
                    !lastNameAttribute.Contains("#ctor") &&
                    nameAttribute.Contains("#ctor"))
                {
                    name = "firstconstructor";
                }
                if (name == "property" &&
                    lastNode.Name?.LocalName == "member" &&
                    !lastNameAttribute.StartsWith("P:"))
                {
                    name = "firstproperty";
                }
                if (name == "field" &&
                    lastNode.Name?.LocalName == "member" &&
                    !lastNameAttribute.StartsWith("F:"))
                {
                    name = "firstfield";
                }
                if (name == "event" &&
                    lastNode.Name?.LocalName == "member" &&
                    !lastNameAttribute.StartsWith("E:"))
                {
                    name = "firstevent";
                }
            }
        }
        catch (Exception ex)
        {
            switch (context.UnexpectedTagAction)
            {
                case UnexpectedTagActionEnum.Error:
                    throw new InvalidOperationException(
                        $"Failed to resolve member ordering for tag \"{name}\".", ex);
                case UnexpectedTagActionEnum.Warn:
                    context.WarningLogger?.LogWarning(
                        $"Failed to resolve member ordering for tag \"{name}\": {ex.Message}");
                    break;
                case UnexpectedTagActionEnum.Accept:
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected {nameof(UnexpectedTagActionEnum)}");
            }
        }

        return name;
    }

    #endregion

    #region Link Resolution

    /// <summary>
    /// Resolves a cref or href attribute value to a display name and URL pair.
    /// In GitHub mode, documented same-file types resolve to in-page anchors;
    /// other types resolve to repository source-file links when possible.
    /// In non-GitHub mode, cross-assembly links are generated.
    /// </summary>
    /// <param name="att">Attribute name to read from <paramref name="node"/> (typically <c>"cref"</c>).</param>
    /// <param name="node">The element that carries the attribute.</param>
    /// <param name="context">Current conversion context.</param>
    /// <returns>
    /// A two-element sequence: <c>[displayName, url]</c>.
    /// The URL may be empty when no link target can be determined.
    /// </returns>
    internal static IEnumerable<string> ExtractNameAndUrl(string att, XElement node, ConversionContext context)
    {
        var name = node.Attribute(att)?.Value ?? string.Empty;
        var display = ResolveDisplayName(name, node, context);
        if (string.IsNullOrWhiteSpace(name))
            return [display, string.Empty];

        if (context.IsGitHub)
        {
            // Explicit in-page anchor: !:#SomeHeading
            if (name.StartsWith("!:#", StringComparison.Ordinal))
                return [display, "#" + ToAnchorSlug(name[3..])];

            // Unresolved cref (compiler could not find the type) — try repo source file by bare name.
            if (name.StartsWith("!:", StringComparison.Ordinal))
            {
                var bareName = name[2..];
                var repoLink = ResolveRepositoryFileLink(bareName, context);
                return string.IsNullOrWhiteSpace(repoLink)
                    ? [display, string.Empty]
                    : [display, repoLink];
            }

            if (name.StartsWith("T:", StringComparison.Ordinal))
            {
                var typeName = name[2..];
                var documentedLinkTarget = ResolveDocumentedTypeLinkTarget(typeName, node);

                // Type is documented in the current XML → it will be a heading in this markdown file.
                if (!string.IsNullOrWhiteSpace(documentedLinkTarget))
                    return [display, "#" + ToAnchorSlug(documentedLinkTarget)];

                // Not in this markdown (private/internal) → link to its source file.
                var repoLink = ResolveRepositoryFileLink(typeName, context);
                return string.IsNullOrWhiteSpace(repoLink)
                    ? [display, string.Empty]
                    : [display, repoLink];
            }

            return [display, string.Empty];
        }

        var (resolvedUrl, assemblyName) = ResolveUrl(name, name, context);
        return [display, "../" + assemblyName + "/#" + resolvedUrl.ToLowerInvariant()];
    }

    /// <summary>
    /// Renders a cref or href attribute as a Markdown hyperlink, or plain text when no
    /// URL can be resolved.
    /// </summary>
    /// <param name="att">Attribute name to read from <paramref name="node"/>.</param>
    /// <param name="node">The element that carries the attribute.</param>
    /// <param name="context">Current conversion context.</param>
    internal static string FormatReference(string att, XElement node, ConversionContext context)
    {
        var parts = ExtractNameAndUrl(att, node, context).ToArray();
        var display = parts.Length > 0 ? parts[0] : string.Empty;
        var url = parts.Length > 1 ? parts[1] : string.Empty;
        return string.IsNullOrWhiteSpace(url) ? display : $"[{display}]({url})";
    }

    /// <summary>
    /// Renders an href attribute as a Markdown hyperlink.
    /// Returns only the display text when the attribute is absent.
    /// </summary>
    /// <param name="v">Attribute name to read (typically <c>"href"</c>).</param>
    /// <param name="x">The element that carries the attribute.</param>
    /// <param name="_">Unused conversion context (required by strategy signature).</param>
    internal static IEnumerable<string> ExtractUrl(string v, XElement x, ConversionContext _)
    {
        var href = x.Attribute(v)?.Value;
        var display = x.Value;
        if (href is null)
            return [display];
        return [display, href];
    }

    /// <summary>
    /// Attempts to resolve a repository-relative source-file link for
    /// <paramref name="typeName"/> using <see cref="ReflectionHelper.FindSourceFile"/>.
    /// Returns <see langword="null"/> when the repository root is not configured or the
    /// source file cannot be found.
    /// </summary>
    private static string? ResolveRepositoryFileLink(string typeName, ConversionContext context)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(context);

        var repositoryRootPath = context.RepositoryRootPath;
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return null;

        var sourceFile = ReflectionHelper.FindSourceFile(typeName, repositoryRootPath);
        if (string.IsNullOrWhiteSpace(sourceFile))
            return null;

        var outputDirectory = !string.IsNullOrWhiteSpace(context.OutputMarkdownFile)
            ? Path.GetDirectoryName(context.OutputMarkdownFile)
            : repositoryRootPath;
        if (string.IsNullOrWhiteSpace(outputDirectory))
            outputDirectory = repositoryRootPath;

        var relativePath = Path.GetRelativePath(outputDirectory, sourceFile);
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Looks up <paramref name="typeName"/> in the current XML document and returns the
    /// fully-qualified type name when it is publicly documented (i.e. will produce a
    /// heading in the output Markdown file).
    /// For interface types (<c>IFoo</c>) a same-namespace class named <c>Foo</c> is tried
    /// as a fallback.
    /// </summary>
    /// <param name="typeName">Fully-qualified type name without the <c>T:</c> prefix.</param>
    /// <param name="node">
    /// Any element within the XML document to use as the document root anchor.
    /// </param>
    /// <returns>
    /// The matched type name (for anchor generation), or <see langword="null"/> when the
    /// type is not publicly documented in this document.
    /// </returns>
    private static string? ResolveDocumentedTypeLinkTarget(string typeName, XElement node)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(node);

        if (HasDocumentedType(typeName, node))
            return typeName;

        var shortName = typeName.Split('.').Last();
        if (shortName.Length <= 1 || shortName[0] != 'I' || !char.IsUpper(shortName[1]))
            return null;

        var lastDotIndex = typeName.LastIndexOf('.');
        var namespacePrefix = lastDotIndex >= 0 ? typeName[..(lastDotIndex + 1)] : string.Empty;
        var classTypeName = namespacePrefix + shortName[1..];

        return HasDocumentedType(classTypeName, node) ? classTypeName : null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the XML document contains a publicly-visible
    /// <c>&lt;member name="T:<paramref name="typeName"/>"&gt;</c> entry.
    /// </summary>
    private static bool HasDocumentedType(string typeName, XElement node)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(node);

        return node.Document?
            .Root?
            .Element("members")?
            .Elements("member")
            .Any(member =>
                string.Equals(member.Attribute("name")?.Value, "T:" + typeName, StringComparison.Ordinal)
                && ReflectionHelper.IsPublic(member)) == true;
    }

    /// <summary>
    /// Generates a GitHub-compatible anchor slug from <paramref name="text"/> by
    /// lower-casing, removing punctuation, and collapsing whitespace to hyphens.
    /// </summary>
    private static string ToAnchorSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var slug = text
            .ToLowerInvariant()
            .Replace(".", string.Empty)
            .Replace("`", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace(",", "-")
            .Replace("_", "-")
            .Replace("‹", string.Empty)
            .Replace("›", string.Empty);

        slug = AnchorInvalidCharsRegex().Replace(slug, string.Empty);
        slug = WhitespaceRegex().Replace(slug, "-");
        slug = HyphenRunsRegex().Replace(slug, "-").Trim('-');
        return slug;
    }

    /// <summary>
    /// Builds the legacy non-GitHub URL for a cref reference by stripping the assembly
    /// namespace and sanitising the remaining name for use in a hash-fragment URL.
    /// </summary>
    private static (string Url, string AssemblyName) ResolveUrl(string name, string url, ConversionContext context)
    {
        var assemblyName = context.AssemblyName;

        if (url.Contains('`'))
            url = name[..url.IndexOf('`')];

        if (url.Contains("T:"))
        {
            url = url.Replace("T:", string.Empty);
            if (assemblyName != null && url.Contains(assemblyName))
            {
                if (!context.IsGitHub)
                    url = url.RemoveNamespace(assemblyName);
            }
            else
            {
                var count = url.Count(x => x == '.');
                string[] parts = url.Split('.');
                assemblyName = parts.Take(count).Aggregate((x, y) => x += $".{y}");
            }
        }

        if (assemblyName != null && !context.IsGitHub)
            url = url.RemoveNamespace(assemblyName);

        url = url.Replace("N:", string.Empty);
        if (name.Contains("M:"))
        {
            url = url.Replace("M:", string.Empty)
                     .Replace("(", string.Empty)
                     .Replace(")", string.Empty)
                     .Replace(",", "-")
                     .Replace(" ", "-");
        }

        url = url.Replace(".", string.Empty);
        return (url, assemblyName ?? string.Empty);
    }

    /// <summary>
    /// Generates the legacy non-GitHub URL for <paramref name="type"/>.
    /// </summary>
    private static string GenerateUrl(Type type)
    {
        var url = type.Name;
        if (url.Contains('`'))
            url = type.Name[..url.IndexOf('`')];

        url = type.Namespace + "." + url;
        url = url.RemoveNamespace(type.Assembly.GetName().Name ?? string.Empty);
        url = url.Replace(".", string.Empty)
                 .Replace(",", "-")
                 .Replace(" ", "-");

        return "../" + type.Assembly.GetName().Name + "/#" + url.ToLowerInvariant();
    }

    #endregion

    #region Member Extraction

    /// <summary>
    /// Extracts the display name and Markdown body from a <c>&lt;member&gt;</c> element,
    /// then appends the resolved return-type string as a third element.
    /// Used by table-rendering strategies to populate Name, Description, and Type columns.
    /// </summary>
    /// <param name="node">The <c>&lt;member&gt;</c> element to extract from.</param>
    /// <param name="context">Current conversion context.</param>
    /// <returns>
    /// A three-element array: <c>[name, description, typeName]</c>.
    /// </returns>
    internal static string[] ExtractNameAndBodyFromMemberProps(XElement node, ConversionContext context)
    {
        var values = ExtractNameAndBodyFromMember(node, context);
        var nameAttr = node.Attribute("name");
        if (values.Length == 2 && nameAttr != null && !nameAttr.Value.Contains("F:"))
            values[1] = values[1].TrimEnd().RemoveRedundantLineBreaks();

        values[1] = values[1].SanitizeForTableCell();

        var memberName = nameAttr?.Value ?? string.Empty;
        var type = memberName.Split(':')[0];
        var className = _PrefixReplacerRegex.Replace(memberName, string.Empty);
        var attributeName = className.Split('.').Last();
        if (className.Contains('.'))
            className = className[..className.LastIndexOf('.')];

        var retType = ReflectionHelper.GetReturnType(className, type, attributeName);
        if (retType is null)
        {
            var returnTypeName = ReflectionHelper.GetReturnTypeName(className, type, attributeName);
            if (!string.IsNullOrWhiteSpace(returnTypeName))
                return [values[0], values[1], FormatTypeName(returnTypeName, node, context)];

            return [values[0], values[1], CleanUpTypeForUrl(type)];
        }

        if (retType.IsGenericType)
        {
            var collectionName = string.Empty;
            if (retType.GetGenericTypeDefinition() == typeof(List<>))
                collectionName = "List" + '‹' + retType.GetGenericArguments()[0].Name + '›';
            else if (retType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                collectionName = "Dictionary‹" + retType.GetGenericArguments()[0].Name + ", " + retType.GetGenericArguments()[1].Name + "›";

            return [values[0], values[1], collectionName];
        }

        var typeDisplay = retType.Name.Replace("[]", string.Empty);
        var targetTypeName = retType.IsArray
            ? retType.GetElementType()?.FullName ?? retType.FullName ?? typeDisplay
            : retType.FullName ?? typeDisplay;

        if (!string.Equals(retType.Assembly.GetName().Name, context.AssemblyName, StringComparison.OrdinalIgnoreCase))
            return [values[0], values[1], typeDisplay + (retType.IsArray ? "[]" : string.Empty)];

        return [values[0], values[1], FormatLinkedTypeName(typeDisplay, targetTypeName, retType.IsArray, node, context)];
    }

    /// <summary>
    /// Extracts the display name and rendered Markdown body from a <c>&lt;member&gt;</c>
    /// element, resolving generics and parameter lists.
    /// </summary>
    /// <param name="node">The <c>&lt;member&gt;</c> element to extract from.</param>
    /// <param name="context">Current conversion context.</param>
    /// <returns>A two-element array: <c>[name, body]</c>.</returns>
    internal static string[] ExtractNameAndBodyFromMember(XElement node, ConversionContext context)
    {
        var isMethod = node.Name.LocalName == "member" && (node.Attribute("name")?.Value?.StartsWith("M:") ?? false);
        var isPropertyOrField = node.Name.LocalName == "member" &&
            ((node.Attribute("name")?.Value?.StartsWith("P:") ?? false) ||
             (node.Attribute("name")?.Value?.StartsWith("F:") ?? false));

        var name = node.Attribute("name")?.Value;
        if (name is null)
            return [string.Empty, node.Nodes().ToMarkDown(context)];

        var parameters = name
            .Split('(').Last()
            .Split(')').First()
            .Split(',');

        if (name.Contains('('))
            name = name[..name.IndexOf('(')];

        foreach (var prefix in _MemberNamePrefixDict.Keys)
        {
            if (name.Contains(prefix))
                name = name.Replace(prefix, string.Empty);
        }

        if (name.Contains(".#ctor"))
            name = name.Replace(".#ctor", string.Empty);

        if (isMethod)
            name = name[(name.LastIndexOf('.') + 1)..];

        name = GetGenerics(node, name);
        name = GetParams(node, context, name, parameters);

        if (isMethod && !name.Contains('('))
            name += "()";

        if (isPropertyOrField)
            name = name[(name.LastIndexOf('.') + 1)..];

        return [name, node.Nodes().ToMarkDown(context)];
    }

    /// <summary>
    /// Extracts the name attribute from <paramref name="x"/>'s parent member element,
    /// returning only the simple (unqualified) name.
    /// </summary>
    internal static string[] ExtractName(XElement x, ConversionContext _)
    {
        var value = x.Parent?.Attribute("name")?.Value ?? string.Empty;
        if (value.Length > 0)
            value = value[(value.LastIndexOf('.') + 1)..];
        return [value];
    }

    /// <summary>
    /// Extracts the name and body for a list-description item, bolding the name.
    /// </summary>
    internal static string[] ExtractNameAndBodyForListDescription(XElement node, ConversionContext context)
    {
        string[] values = ExtractNameAndBodyFromMember(node, context);
        if (values.Length > 0 && !string.IsNullOrWhiteSpace(values[0]))
            values[0] = $"**{values[0]}:";
        return values;
    }

    /// <summary>
    /// Extracts the name attribute and body text from <paramref name="node"/>, stripping
    /// assembly namespace prefixes from the name and defaulting the body to a capitalised
    /// version of the name when empty.
    /// </summary>
    internal static string[] ExtractNameAndBody(string att, XElement node, ConversionContext context)
    {
        var name = node.Attribute(att)?.Value;
        if (!string.IsNullOrWhiteSpace(name))
            name = name.RemoveNamespace(context.AssemblyName);
        name ??= string.Empty;
        var body = node.Nodes().ToMarkDown(context);
        if (string.IsNullOrWhiteSpace(body))
            body = char.ToUpper(name[0]) + name[1..];
        return [name, body];
    }

    /// <summary>
    /// Resolves the return type display name for the method, property, or field
    /// described by <paramref name="node"/>'s parent member element.
    /// For constructors, the declaring type name is returned.
    /// </summary>
    /// <returns>A single-element array containing the resolved type name.</returns>
    internal static string[] GetReturnType(XElement node, ConversionContext context)
    {
        string retTypeName;
        var className = node.Parent?.Attribute("name")?.Value ?? string.Empty;
        var type = className.Split(':')[0];
        className = className.Split('(')[0].Split(':')[1];
        if (type == "M" || type == "P" || type == "F")
            className = className[..className.LastIndexOf('.')];

        var methodName = (node.Parent?.Attribute("name")?.Value ?? string.Empty).Split(':')[1];
        methodName = methodName[(className.Length + 1)..];

        if (methodName.Contains("#ctor"))
        {
            retTypeName = className.RemoveNamespace(context.AssemblyName);
            retTypeName = retTypeName.Replace(".#ctor", string.Empty);
            retTypeName = retTypeName.Split('.').Last();
        }
        else
        {
            var retType = ReflectionHelper.GetReturnType(className, type, methodName);
            retTypeName = retType is null ? node.Value : retType.Name;
        }

        _types.TryGetValue(retTypeName, out var newRetTypeName);
        retTypeName = newRetTypeName ?? retTypeName;
        return [retTypeName];
    }

    #endregion

    #region List / Table Generators

    /// <summary>
    /// Generates a Markdown unordered-list item from a <c>&lt;list&gt;</c> element
    /// by bolding the header name and appending the body.
    /// </summary>
    internal static IEnumerable<string> GenerateList(XElement element, ConversionContext context)
    {
        string[] strings = ExtractNameAndBodyFromMember(element, context);
        if (!string.IsNullOrWhiteSpace(strings[0]))
            strings[0] = $"\n\n**{strings[0]}:**\n\n";
        return strings;
    }

    /// <summary>
    /// Generates a numbered (ordered) Markdown list from a <c>&lt;list type="number"&gt;</c>
    /// element. Each <c>&lt;item&gt;</c> child is rendered as a numbered list entry using
    /// its <c>&lt;description&gt;</c> content.
    /// </summary>
    internal static IEnumerable<string> GenerateOrderedList(XElement element, ConversionContext context)
    {
        var sb = new System.Text.StringBuilder();
        int count = 1;
        foreach (var item in element.Elements("item"))
        {
            var desc = item.Element("description");
            var text = desc != null
                ? desc.Nodes().ToMarkDown(context).Trim()
                : item.Nodes().ToMarkDown(context).Trim();
            sb.Append($"{count}. {text}\n");
            count++;
        }
        return [string.Empty, sb.ToString()];
    }

    /// <summary>
    /// Generates a Markdown table from a <c>&lt;list type="table"&gt;</c> element.
    /// An optional <c>&lt;listheader&gt;</c> provides column headers; each <c>&lt;item&gt;</c>
    /// becomes a table row with <c>&lt;term&gt;</c> and <c>&lt;description&gt;</c> mapped
    /// to the two columns.
    /// </summary>
    internal static IEnumerable<string> GenerateTableList(XElement element, ConversionContext context)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("\n\n");

        var header = element.Element("listheader");
        if (header != null)
        {
            string termText = (header.Element("term")?.Nodes().ToMarkDown(context).Trim() ?? "Term").SanitizeForTableCell();
            string descText = (header.Element("description")?.Nodes().ToMarkDown(context).Trim() ?? "Description").SanitizeForTableCell();
            sb.Append($"|{termText}|{descText}|\n|---|---|\n");
        }
        else
        {
            sb.Append("|Term|Description|\n|---|---|\n");
        }

        foreach (var item in element.Elements("item"))
        {
            var termText = (item.Element("term")?.Nodes().ToMarkDown(context).Trim() ?? string.Empty).SanitizeForTableCell();
            var descText = (item.Element("description")?.Nodes().ToMarkDown(context).Trim() ?? string.Empty).SanitizeForTableCell();
            sb.Append($"|{termText}|{descText}|\n");
        }

        sb.Append("\n\n");
        return [string.Empty, sb.ToString()];
    }

    #endregion

    #region Formatting Helpers

    /// <summary>
    /// Formats <paramref name="typeName"/> as a display name, optionally with a
    /// Markdown hyperlink. Handles by-ref (<c>@</c>), array (<c>[]</c>), and
    /// <c>Nullable{T}</c> suffixes.
    /// </summary>
    private static string FormatTypeName(string typeName, XElement node, ConversionContext context)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        var isByRef = typeName.EndsWith("@", StringComparison.Ordinal);
        if (isByRef)
            typeName = typeName[..^1];

        var isArray = typeName.EndsWith("[]", StringComparison.Ordinal);
        if (isArray)
            typeName = typeName[..^2];

        if (typeName.StartsWith("System.Nullable{", StringComparison.Ordinal) && typeName.EndsWith("}", StringComparison.Ordinal))
        {
            var innerTypeName = typeName[16..^1];
            return FormatTypeName(innerTypeName, node, context) + "?";
        }

        var displayName = GetTypeDisplayName(typeName);
        return FormatLinkedTypeName(displayName, typeName, isArray, node, context, isByRef);
    }

    /// <summary>
    /// Renders <paramref name="displayName"/> as a plain string or a Markdown hyperlink
    /// to the type identified by <paramref name="typeName"/>.
    /// <c>System.*</c> types are always returned as plain text.
    /// In GitHub mode, documented same-file types link to their in-page anchor and
    /// other types link to their repository source file.
    /// </summary>
    private static string FormatLinkedTypeName(
        string displayName,
        string typeName,
        bool isArray,
        XElement node,
        ConversionContext context,
        bool isByRef = false)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        if (isArray)  displayName += "[]";
        if (isByRef)  displayName += "@";

        if (typeName.StartsWith("System.", StringComparison.Ordinal))
            return displayName;

        var documentedLinkTarget = ResolveDocumentedTypeLinkTarget(typeName, node);

        if (context.IsGitHub)
        {
            if (!string.IsNullOrWhiteSpace(documentedLinkTarget))
                return $"[{displayName}](#{ToAnchorSlug(documentedLinkTarget)})";

            var repositoryFileLink = ResolveRepositoryFileLink(typeName, context);
            if (!string.IsNullOrWhiteSpace(repositoryFileLink))
                return $"[{displayName}]({repositoryFileLink})";

            return displayName;
        }

        var linkTarget = documentedLinkTarget ?? typeName;
        var url = "../" + context.AssemblyName + "/#" + linkTarget.RemoveNamespace(context.AssemblyName)
            .Replace(".", string.Empty)
            .Replace(",", "-")
            .Replace(" ", "-")
            .ToLowerInvariant();

        return $"[{displayName}]({url})";
    }

    /// <summary>
    /// Returns the C# keyword alias for a CLR type name, or the simple unqualified name
    /// when no alias is registered.
    /// </summary>
    private static string GetTypeDisplayName(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        var shortName = typeName.Split('.').Last();
        return _types.TryGetValue(shortName, out var alias) ? alias : shortName;
    }

    /// <summary>
    /// Cleans up a raw XML doc type string by resolving any <c>#</c>-fragment alias to
    /// its C# keyword equivalent, preserving array (<c>[]</c>) and by-ref (<c>@</c>)
    /// suffixes.
    /// </summary>
    private static string CleanUpTypeForUrl(string s)
    {
        var isRef   = s.Contains('@');
        var isArray = s.Contains("[]");

        if (s.Contains('#'))
        {
            string temp = s.Split('#')[1];
            if (isRef)   temp = temp.Replace("@", string.Empty);
            if (isArray) s    = s.Replace("[]", string.Empty);
            if (temp.Contains(')')) temp = temp.Split(')')[0];

            if (_types.ContainsKey(temp))
            {
                temp = temp.Replace("System.", string.Empty);
                s = _types[temp.ToLower()];
            }

            if (isRef)   s += "@";
            if (isArray) s += "[]";
        }

        return s;
    }

    /// <summary>
    /// Resolves the display name for a cref value by stripping prefixes and qualifications,
    /// formatting method parameter lists when present.
    /// </summary>
    private static string ResolveDisplayName(string name, XElement node, ConversionContext context)
    {
        var display = name;

        if (display.Contains("M:") && display.Contains('('))
        {
            var members = node?.Document?.Root?.Element("members")?.Elements("member");
            var member = members?.FirstOrDefault(m => m.Attribute("name")?.Value == name);
            if (member != null)
            {
                var parameters = display.Split('(').Last().Split(')').First().Split(',');
                display = display[..display.IndexOf('(')];
                display = display[(display.LastIndexOf('.') + 1)..];
                display = GetParams(member, context, display, parameters);
            }
        }
        else if (display.Contains("M:"))
        {
            display = display[(display.LastIndexOf('.') + 1)..];
            if (!display.Contains('('))
                display += "()";
        }
        else
        {
            display = display[(display.LastIndexOf('.') + 1)..];
        }

        if (display.Contains('`'))
            display = display[..display.IndexOf('`')];

        return display;
    }

    /// <summary>
    /// Appends generic type parameters to <paramref name="name"/> using angle-bracket
    /// notation (<c>‹T›</c>) derived from the <c>&lt;typeparam&gt;</c> children of
    /// <paramref name="node"/>.
    /// </summary>
    private static string GetGenerics(XElement node, string name)
    {
        if (!name.Contains('`'))
            return name;

        name = name.Replace("``", "`");
        var genericCount = int.Parse(name.Substring(name.IndexOf('`') + 1, 1));
        var generics = "‹";

        foreach (XElement el in node.Nodes().Cast<XElement>())
        {
            if (el.Name.LocalName != "typeparam")
                continue;
            if (el.Attribute("name")?.Value is string paramName)
                generics += paramName + ", ";
        }

        if (generics.Length > 2)
            generics = generics[..^2];
        generics += "›";

        if (generics == "‹›")
            generics = string.Empty;

        return name.Replace($"`{genericCount}", generics);
    }

    /// <summary>
    /// Builds the parameter list portion of a method display name by matching XML doc
    /// <c>&lt;param&gt;</c> children with the raw parameter-type strings from the
    /// member's name attribute. Wraps the result in parentheses.
    /// </summary>
    private static string GetParams(XElement node, ConversionContext context, string name, string[] parameters)
    {
        var paramNodes = node.Nodes()
            .Where(n => n.NodeType == XmlNodeType.Element && ((XElement)n).Name.LocalName == "param")
            .ToList();

        var total = paramNodes.Count;
        var count = 0;
        var param = string.Empty;

        foreach (var paramNode in paramNodes)
        {
            if (count == 0)
                name += "(";

            var paramNodeName = ((XElement)paramNode).Attribute("name")?.Value ?? string.Empty;

            if (parameters.Length > count)
            {
                var type = parameters[count];

                if (type.Contains("System.Collections.Generic.List{"))
                {
                    var classTemp = type.Split('{').Last();
                    type = type.Split('{').First();
                    type = type.Replace("System.Collections.Generic.List", "List‹");
                    classTemp = classTemp.RemoveNamespace(context.AssemblyName);
                    type += classTemp.Replace("}", "›");
                }
                else if (type.Contains("System."))
                {
                    var tempType = type.Split('.').Last();
                    var isParameterArray = tempType.Contains("[]");
                    if (isParameterArray) tempType = tempType.Replace("[]", string.Empty);
                    var isReferenceType = tempType.Contains('@');
                    if (isReferenceType) tempType = tempType.Replace("@", string.Empty);
                    if (_types.TryGetValue(tempType, out tempType))
                        type = tempType ?? type;
                    if (isParameterArray) type += "[]";
                    if (isReferenceType)  type += "@";
                }
                else
                {
                    type = type.RemoveNamespace(context.AssemblyName);
                }

                if (type.Contains('.'))
                    type = type[(type.LastIndexOf('.') + 1)..];

                param += $"{type} {paramNodeName}";
            }

            if (count < total - 1)
                param += ", ";

            if (count == total - 1)
                param += ")";

            count++;
        }

        return name + param;
    }

    #endregion

    #region String Extensions

    /// <summary>
    /// Removes the assembly namespace prefix and <c>System.</c> prefix from
    /// <paramref name="s"/>, and converts <c>Nullable{T}</c> notation to the <c>T?</c>
    /// shorthand.
    /// </summary>
    internal static string RemoveNamespace(this string s, string assemblyName)
    {
        s = s.Replace($@"{assemblyName}.", string.Empty);
        if (s.Contains("Nullable"))
        {
            s = s.Replace("Nullable", string.Empty)
                 .Replace("{", string.Empty)
                 .Replace("}", string.Empty);
            s += "?";
        }
        s = s.Replace("System.", string.Empty);
        return s;
    }

    /// <summary>
    /// Converts the indented source text <paramref name="s"/> to a de-indented code
    /// block by trimming the same number of leading spaces from every line as were
    /// present on the first line beyond a four-space minimum.
    /// </summary>
    internal static string ToCodeBlock(this string s)
    {
        var lines = s.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return string.Empty;
        var blank = lines[0].TakeWhile(x => x == ' ').Count() - 4;
        return string.Join("\n", lines.Select(x => new string([.. x.SkipWhile((y, i) => i < blank)])))
            .TrimEnd();
    }

    /// <summary>
    /// Collapses three or more consecutive blank lines to a maximum of two.
    /// </summary>
    internal static string RemoveRedundantLineBreaks(this string s)
        => ExcessiveLineBreaksRegex().Replace(s, "\n\n");

    /// <summary>
    /// Normalises line endings, trims trailing whitespace from each line, fixes
    /// block-quote prefix whitespace, removes redundant blank lines, and ensures the
    /// output ends with exactly one newline.
    /// </summary>
    internal static string NormalizeMarkdown(this string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var cleaned = lines
            .Select(x => x.TrimEnd())
            .ToArray();

        for (var i = 0; i < cleaned.Length; i++)
        {
            if (cleaned[i].StartsWith('>'))
                cleaned[i] = BlockQuotePrefixRegex().Replace(cleaned[i], "> ");
        }

        var result = string.Join("\n", cleaned)
            .RemoveRedundantLineBreaks()
            .Trim();

        return result + "\n";
    }

    /// <summary>
    /// Sanitizes content for use in Markdown table cells by replacing newlines with
    /// spaces, collapsing excessive whitespace, escaping pipe characters, and
    /// converting fenced code blocks to inline code. This prevents multi-line content
    /// from breaking table syntax.
    /// </summary>
    internal static string SanitizeForTableCell(this string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        content = content.Replace("\r\n", " ").Replace("\n", " ");
        content = content.Replace("```", "`");
        content = content.Replace("|", "\\|");
        content = WhitespaceRegex().Replace(content, " ");
        return content.Trim();
    }

    #endregion
}
