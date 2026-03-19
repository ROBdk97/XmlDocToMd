using ROBdk97.XmlDocToMd.Cli;
using ROBdk97.XmlDocToMd.Logging;
using System.Text.RegularExpressions;
using System.Xml;

namespace ROBdk97.XmlDocToMd.Conversion;

/// <summary>
/// Primary orchestration class for converting XML documentation to Markdown.
/// Provides convenience overloads for common conversion scenarios.
/// </summary>
internal static partial class XmlToMarkdown
{
    [GeneratedRegex("[^a-z0-9\\s-]")]
    private static partial Regex AnchorInvalidCharsRegex();

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

    private static string ResolveTagName(XElement el, XNode node, ConversionContext context)
    {
        var name = el.Name.LocalName;
        if (name == "member")
        {
            if (!ReflectionHelper.IsPublic(el))
            {
                return string.Empty;
            }
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
                var anchor = crefAttr != null && crefAttr.Value.StartsWith("!:#");
                name = anchor ? "seeAnchor" : "seePage";
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
            {
                name = "fieldsummary";
            }
        }
        else if (name == "remarks")
        {
            if (el.Value == string.Empty)
            {
                name = "nameremarks";
            }
        }
        if (name == "method" && el.Attribute("name")?.Value.Contains("#ctor") == true)
        {
            name = "constructor";
        }
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
            context.WarningLogger?.LogWarning(
                $"Failed to resolve member ordering for tag \"{name}\": {ex.Message}");
        }
        return name;
    }

    /// <summary>
    /// Write out the given XML Node as Markdown. Recursive function used internally.
    /// </summary>
    /// <param name="node">The xml node to write out.</param>
    /// <param name="context">The Conversion Context that will be passed around and manipulated over the course of the translation.</param>
    /// <returns>The converted markdown text.</returns>
    internal static string ToMarkDown(this XNode node, ConversionContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        if (node is XDocument document)
        {
            node = document.Root ?? throw new InvalidOperationException("Document root is null");
        }

        if (node.NodeType == XmlNodeType.Text)
            return WhitespaceRegex().Replace(((XText)node).Value.Replace('\n', ' '), " ");

        if (node.NodeType == XmlNodeType.Element)
        {
            var el = (XElement)node;
            var name = ResolveTagName(el, node, context);
            // If tag name is empty (e.g., non-public member), skip rendering
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

    private static readonly Regex _PrefixReplacerRegex = PrefixReplacerRegex();

    internal static string[] ExtractNameAndBodyFromMemberProps(XElement node, ConversionContext context)
    {
        var values = ExtractNameAndBodyFromMember(node, context);
        var nameAttr = node.Attribute("name");
        if (values.Length == 2 && nameAttr != null && !nameAttr.Value.Contains("F:"))
            values[1] = values[1].TrimEnd().RemoveRedundantLineBreaks();

        // Sanitize description for table cell use
        values[1] = values[1].SanitizeForTableCell();

        var className = nameAttr?.Value ?? string.Empty;
        var type = className.Split(':')[0];
        //remove the type prefix
        className = _PrefixReplacerRegex.Replace(className, string.Empty);
        // get property/field name
        var attributeName = node.Attribute("name")?.Value ?? string.Empty;
        // get property/field by getting the last part of the attribute name
        attributeName = attributeName.Split('.').Last();
        // remove the attribute name from the class name
        var retType = ReflectionHelper.GetReturnType(className, type, attributeName);
        if (retType is null)
            return [values[0], values[1], CleanUpTypeForUrl(type)];
        // try to get lists and the type of the list
        if (retType.IsGenericType)
        {
            var collectionName = string.Empty;
            if (retType.GetGenericTypeDefinition() == typeof(List<>))
            {
                collectionName = "List" + '‹' + retType.GetGenericArguments()[0].Name + '›';
            }
            else if (retType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                collectionName = "Dictionary" + '‹' + retType.GetGenericArguments()[0].Name + ", " + retType.GetGenericArguments()[1].Name + '›';
            }
            return [values[0], values[1], collectionName];
        }
        var typeDisplay = retType.Name.Replace("[]", string.Empty);
        var displayAndUrl = string.Format("[{0}]({1})", typeDisplay, GenerateUrl(retType));
        displayAndUrl = CleanUpTypeForUrl(displayAndUrl);
        if (context.IsGitHub)
        {
            // Keep external/BCL types unlinked to avoid unresolved in-document fragments.
            if (!string.Equals(retType.Assembly.GetName().Name, context.AssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                displayAndUrl = typeDisplay;
            }
            else
            {
                var anchorSource = retType.FullName ?? typeDisplay;
                displayAndUrl = $"[{typeDisplay}](#{ToAnchorSlug(anchorSource)})";
            }
        }
        string[] strings = [values[0], values[1], displayAndUrl];
        return strings;
    }

    private static string CleanUpTypeForUrl(string s)
    {
        var isRef = s.Contains('@');
        var isArray = s.Contains("[]");
        if (s.Contains('#'))
        {
            string temp = s.Split('#')[1];
            if (isRef)
                temp = temp.Replace("@", string.Empty);
            if (isArray)
                s = s.Replace("[]", string.Empty);
            if (temp.Contains(')'))
                temp = temp.Split(')')[0];
            if (_types.ContainsKey(temp))
            {
                temp = temp.Replace("System.", string.Empty);
                s = _types[temp.ToLower()];
            }
            if (isRef)
                s += "@";
            if (isArray)
                s += "[]";
        }
        return s;
    }

    private static readonly Dictionary<string, string> _types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = "string",
        ["object"] = "object",
        ["int32"] = "int",
        ["int64"] = "long",
        ["boolean"] = "bool",
        ["decimal"] = "decimal",
        ["void"] = "void",
        ["double"] = "double",
        ["byte"] = "byte",
    };

    internal static string[] ExtractNameAndBodyForListDescription(XElement node, ConversionContext context)
    {
        string[] values = ExtractNameAndBodyFromMember(node, context);
        if (values.Length > 0 && !string.IsNullOrWhiteSpace(values[0]))
            values[0] = $"**{values[0]}:**";
        return values;
    }

    internal static string[] GetReturnType(XElement node, ConversionContext context)
    {
        string retTypeName;
        var className = node.Parent?.Attribute("name")?.Value ?? string.Empty;
        var type = className.Split(':')[0];
        className = className.Split('(')[0].Split(':')[1];
        // Remove Method name from class name
        if (type == "M" || type == "P" || type == "F")
            className = className[..className.LastIndexOf('.')];
        // Get method name
        var methodName = (node.Parent?.Attribute("name")?.Value ?? string.Empty).Split(':')[1];
        methodName = methodName[(className.Length + 1)..];
        // Check if the method is a constructor
        if (methodName.Contains("#ctor"))
        {
            retTypeName = className.RemoveNamespace(context.AssemblyName);
            retTypeName = retTypeName.Replace(".#ctor", string.Empty);
            retTypeName = retTypeName.Split('.').Last();
        }
        else
        {
            // remove class name from method name
            var retType = ReflectionHelper.GetReturnType(className, type, methodName);
            if (retType is null)
                retTypeName = node.Value;
            else
                retTypeName = retType.Name;
        }
        _types.TryGetValue(retTypeName, out var newRetTypeName);
        if (newRetTypeName != null)
            retTypeName = newRetTypeName;
        else retTypeName ??= node.Value;
        string[] values = [retTypeName];
        return values;
    }


    internal static string[] ExtractNameAndBodyFromMember(XElement node, ConversionContext context)
    {
        var isMethod = node.Name.LocalName == "member" && (node.Attribute("name")?.Value?.StartsWith("M:") ?? false);
        var isPropertyOrField = node.Name.LocalName == "member" &&
            ((node.Attribute("name")?.Value?.StartsWith("P:") ?? false) || (node.Attribute("name")?.Value?.StartsWith("F:") ?? false));
        var name = node.Attribute("name")?.Value;
        if (name is null)
        {
            return [string.Empty, node.Nodes().ToMarkDown(context)];
        }
        //Extract the parameters from the name
        var parameters = name
            .Split('(')
            .Last()
            .Split(')')
            .First()
            .Split(',');
        // if name contains ( then remove it and everything after it
        if (name.Contains('('))
        {
            name = name[..name.IndexOf('(')];
        }
        // remove the assembly name from the name
        //name = name.RemoveNamespace(context.AssemblyName);
        // if name contains on of _MemberNamePrefixDict keys, then replace it with nothing
        foreach (var prefix in _MemberNamePrefixDict.Keys)
        {
            if (name.Contains(prefix))
            {
                name = name.Replace(prefix, string.Empty);
            }
        }

        // if it's a constructor, then replace the Method with Constructor
        if (name.Contains(".#ctor"))
        {
            name = name.Replace(".#ctor", string.Empty);
        }
        if (isMethod)
        {
            // remove all the stuff before the last .
            name = name[(name.LastIndexOf('.') + 1)..];
        }
        // if it's has Generics, then replace the `1 with the Type/Name
        name = GetGenerics(node, name);
        // Get the "param" nodes and add them to the name
        name = GetParams(node, context, name, parameters);
        // if it's a method without params, then add the () to the end
        if (isMethod && !name.Contains('('))
        {
            name += "()";
        }
        if (isPropertyOrField)
        {
            //Remove all the stuff before the last .
            name = name[(name.LastIndexOf('.') + 1)..];
        }
        return [name, node.Nodes().ToMarkDown(context)];
    }

    /// <summary>
    /// Get the generic types from the node and add them to the name
    /// </summary>
    /// <param name="node"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    private static string GetGenerics(XElement node, string name)
    {
        if (name.Contains('`'))
        {
            name = name.Replace("``", "`");
            // get one decimal int number after the `
            var genericCount = int.Parse(name.Substring(name.IndexOf('`') + 1, 1));
            var generics = "‹";
            foreach (XElement el in node.Nodes().Cast<XElement>())
            {
                if (el.Name.LocalName != "typeparam")
                    continue;
                if (el?.Attribute("name")?.Value is string name2)
                    generics += name2 + ", ";
            }
            if (generics.Length > 2)
                generics = generics[..^2];
            generics += "›";
            if (generics == "‹›")
            {
                generics = string.Empty;
            }
            name = name.Replace($"`{genericCount}", generics);
        }
        return name;
    }

    /// <summary>
    /// Get the parameters from the node and add them to the name
    /// </summary>
    /// <param name="node"></param>
    /// <param name="context"></param>
    /// <param name="name"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
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
            {
                name += "(";
            }
            // get the Name attribute
            var paramNodeName = ((XElement)paramNode).Attribute("name")?.Value ?? string.Empty;
            // get the type of the parameter
            if (parameters.Length > count)
            {
                var type = parameters[count];
                // if temp contains Collections.Generic.List{ then replace it with List<
                if (type.Contains("System.Collections.Generic.List{"))
                {
                    var classTemp = type.Split('{').Last();
                    type = type.Split('{').First();
                    type = type.Replace("System.Collections.Generic.List", "List‹");
                    classTemp = classTemp.RemoveNamespace(context.AssemblyName);
                    type += classTemp.Replace("}", "›");
                }
                else
                {
                    if (type.Contains("System."))
                    {
                        var tempType = type.Split('.').Last();
                        var isArray = tempType.Contains("[]");
                        if (isArray)
                            tempType = tempType.Replace("[]", string.Empty);
                        var isReferenceType = tempType.Contains('@');
                        if (isReferenceType)
                            tempType = tempType.Replace("@", string.Empty);
                        if (_types.TryGetValue(tempType, out tempType))
                            type = tempType is null ? type : tempType;
                        if (isArray)
                            type += "[]";
                        if (isReferenceType)
                            type += "@";
                    }
                    else
                        type = type.RemoveNamespace(context.AssemblyName);
                }
                // remove all the stuff before the last .
                if (type.Contains('.'))
                    type = type[(type.LastIndexOf('.') + 1)..];
                param += $"{type} {paramNodeName}";
            }
            // if the param is not the last add a comma
            if (count < total - 1)
            {
                param += ", ";
            }
            // if last param then add the closing )
            if (count == total - 1)
            {
                param += ")";
            }
            count++;
        }
        name += param;
        return name;
    }

    internal static string RemoveNamespace(this string s, string assemblyName)
    {
        s = s.Replace($@"{assemblyName}.", string.Empty);
        // if contains System.Nullable then remove the curly braces and add a ? to the end
        if (s.Contains("Nullable"))
        {
            s = s.Replace("Nullable", string.Empty);
            s = s.Replace("{", string.Empty);
            s = s.Replace("}", string.Empty);
            s += "?";
        }
        s = s.Replace("System.", string.Empty);
        return s;
    }

    internal static string[] ExtractNameAndBody(string att, XElement node, ConversionContext context)
    {
        var name = node.Attribute(att)?.Value;
        if (!string.IsNullOrWhiteSpace(name))
            name = name.RemoveNamespace(context.AssemblyName);
        name ??= string.Empty;
        var body = node.Nodes().ToMarkDown(context);
        if (string.IsNullOrWhiteSpace(body))
        {
            body = char.ToUpper(name[0]) + name[1..];
        }
        return [name, body];
    }

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
            {
                display += @"()";
            }
        }
        else
        {
            display = display[(display.LastIndexOf('.') + 1)..];
        }

        if (display.Contains('`'))
        {
            display = display[..display.IndexOf('`')];
        }

        return display;
    }

    private static (string Url, string AssemblyName) ResolveUrl(string name, string url, ConversionContext context)
    {
        var assemblyName = context.AssemblyName;

        if (url.Contains('`'))
        {
            url = name[..url.IndexOf('`')];
        }

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
            url = url.Replace("M:", string.Empty);
            url = url.Replace("(", string.Empty);
            url = url.Replace(")", string.Empty);
            url = url.Replace(",", "-");
            url = url.Replace(" ", "-");
        }

        url = url.Replace(".", string.Empty);
        return (url, assemblyName ?? string.Empty);
    }

    internal static IEnumerable<string> ExtractNameAndUrl(string att, XElement node, ConversionContext context)
    {
        var name = node.Attribute(att)?.Value ?? string.Empty;
        var display = ResolveDisplayName(name, node, context);
        if (string.IsNullOrWhiteSpace(name))
        {
            return [display, string.Empty];
        }

        if (context.IsGitHub)
        {
            // Explicit anchor cref format !:#anchor-text
            if (name.StartsWith("!:#", StringComparison.Ordinal))
            {
                return [display, "#" + ToAnchorSlug(name[3..])];
            }

            // Only link internal types where generated headings exist and anchors are stable.
            if (name.StartsWith("T:", StringComparison.Ordinal))
            {
                var isInternalTypeRef = !string.IsNullOrWhiteSpace(context.AssemblyName)
                    && name.Contains(context.AssemblyName + ".", StringComparison.Ordinal);
                if (!isInternalTypeRef)
                {
                    return [display, string.Empty];
                }

                var (typeUrl, _) = ResolveUrl(name, name, context);
                return [display, "#" + ToAnchorSlug(typeUrl)];
            }

            // Member refs (M:/P:/F:/E:) and namespaces are often not represented as headings.
            return [display, string.Empty];
        }

        var (url, assemblyName) = ResolveUrl(name, name, context);
        return [display, "../" + assemblyName + "/#" + url.ToLowerInvariant()];
    }

    internal static string FormatReference(string att, XElement node, ConversionContext context)
    {
        var parts = ExtractNameAndUrl(att, node, context).ToArray();
        var display = parts.Length > 0 ? parts[0] : string.Empty;
        var url = parts.Length > 1 ? parts[1] : string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            return display;
        return $"[{display}]({url})";
    }

    internal static string ToMarkDown(this IEnumerable<XNode> es, ConversionContext context)
    { return es.Aggregate(string.Empty, (current, x) => current + x.ToMarkDown(context)); }

    /// <summary>
    /// Sanitizes content for use in markdown table cells by replacing newlines with
    /// spaces and collapsing excessive whitespace. This prevents code blocks and
    /// multi-line content from breaking table syntax.
    /// </summary>
    internal static string SanitizeForTableCell(this string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Replace newlines with spaces
        content = content.Replace("\r\n", " ").Replace("\n", " ");
        // Replace code fence markers with backticks to preserve inline code indication
        content = content.Replace("```", "`");
        // Escape table delimiters so nested markdown tables don't split parent table columns
        content = content.Replace("|", "\\|");
        // Collapse multiple spaces into one
        content = WhitespaceRegex().Replace(content, " ");
        return content.Trim();
    }

    internal static string ToCodeBlock(this string s)
    {
        var lines = s.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return string.Empty;
        var blank = lines[0].TakeWhile(x => x == ' ').Count() - 4;
        return string.Join("\n", lines.Select(x => new string([.. x.SkipWhile((y, i) => i < blank)])))
            .TrimEnd();
    }

    internal static string RemoveRedundantLineBreaks(this string s) { return ExcessiveLineBreaksRegex().Replace(s, "\n\n"); }

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
            {
                cleaned[i] = BlockQuotePrefixRegex().Replace(cleaned[i], "> ");
            }
        }

        var result = string.Join("\n", cleaned)
            .RemoveRedundantLineBreaks()
            .Trim();

        // markdownlint expects exactly one trailing newline.
        return result + "\n";
    }

    internal static string[] ExtractName(XElement x, ConversionContext _)
    {
        string[] strings = [string.Empty];
        strings[0] = x.Parent?.Attribute("name")?.Value ?? string.Empty;
        // remove all the stuff before the last .
        if (strings[0].Length > 0)
            strings[0] = strings[0][(strings[0].LastIndexOf('.') + 1)..];
        return strings;
    }


    /// <summary>
    /// Generate the url for the type
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static string GenerateUrl(Type type)
    {
        var url = type.Name;
        // Remove everything after ` in the url
        if (url.Contains('`'))
        {
            url = type.Name[..url.IndexOf('`')];
        }
        // add the namespace to the url
        url = type.Namespace + "." + url;
        //remove assembly name
        url = url.RemoveNamespace(type.Assembly.GetName().Name ?? string.Empty);
        // replace dots with ""
        url = url.Replace(".", string.Empty);
        // replace not allowed characters with -
        url = url.Replace(",", "-");
        url = url.Replace(" ", "-");
        // return the the url
        return "../" + type.Assembly.GetName().Name + "/#" + url.ToLowerInvariant();
    }

    [GeneratedRegex(@"(^[A-Z]\:)", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixReplacerRegex();

    internal static IEnumerable<string> ExtractUrl(string v, XElement x, ConversionContext _)
    {
        // get the href attribute
        var href = x.Attribute(v)?.Value;
        // get the display name
        var display = x.Value;
        // if the href is null then return the display
        if (href is null)
            return [display];
        // if the href is not null then return the display and the href
        return [display, href];
    }

    internal static IEnumerable<string> GenerateList(XElement element, ConversionContext context)
    {
        string[] strings = ExtractNameAndBodyFromMember(element, context);
        if (!string.IsNullOrWhiteSpace(strings[0]))
            strings[0] = $"\n\n**{strings[0]}:**\n\n";
        return strings;
    }

    /// <summary>
    /// Generates a numbered (ordered) Markdown list from a &lt;list type="number"&gt; element.
    /// Each &lt;item&gt; child is rendered as a numbered list entry using its &lt;description&gt; content.
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
    /// Generates a Markdown table from a &lt;list type="table"&gt; element.
    /// An optional &lt;listheader&gt; provides column headers; each &lt;item&gt; becomes a table row
    /// with &lt;term&gt; and &lt;description&gt; mapped to the two columns.
    /// </summary>
    internal static IEnumerable<string> GenerateTableList(XElement element, ConversionContext context)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('\n');
        var header = element.Element("listheader");
        if (header != null)
        {
            string termText = header.Element("term")?.Nodes().ToMarkDown(context).Trim() ?? "Term";
            string descText = header.Element("description")?.Nodes().ToMarkDown(context).Trim() ?? "Description";
            sb.Append($"|{termText}|{descText}|\n|---|---|\n");
        }
        else
        {
            sb.Append("|Term|Description|\n|---|---|\n");
        }
        foreach (var item in element.Elements("item"))
        {
            var termText = item.Element("term")?.Nodes().ToMarkDown(context).Trim() ?? string.Empty;
            var descText = item.Element("description")?.Nodes().ToMarkDown(context).Trim() ?? string.Empty;
            sb.Append($"|{termText}|{descText}|\n");
        }
        sb.Append('\n');
        return [string.Empty, sb.ToString()];
    }

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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@">\s+")]
    private static partial Regex BlockQuotePrefixRegex();
    [GeneratedRegex(@"\n\n\n+")]
    private static partial Regex ExcessiveLineBreaksRegex();
    [GeneratedRegex(@"-+")]
    private static partial Regex HyphenRunsRegex();
}
