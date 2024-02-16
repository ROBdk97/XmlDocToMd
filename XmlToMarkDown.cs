using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ROBdk97.XmlDocToMd
{
    internal static partial class XmlToMarkdown
    {
        internal static string CurrentXmlFile { get; set; } = string.Empty;

        internal static bool IsGitHub { get; set; } = false;

        internal static string ToMarkDown(this string s)
        {
            return s.ToMarkDown(
                new ConversionContext
                {
                    UnexpectedTagAction = UnexpectedTagActionEnum.Error,
                    WarningLogger = new TextWriterWarningLogger(Console.Error)
                });
        }

        internal static string ToMarkDown(this string s, ConversionContext context)
        {
            var xdoc = XDocument.Parse(s);
            return xdoc
                .ToMarkDown(context)
                .RemoveRedundantLineBreaks();
        }

        internal static string ToMarkDown(this Stream s)
        {
            var xdoc = XDocument.Load(s);
            return xdoc
                .ToMarkDown(
                    new ConversionContext
                    {
                        UnexpectedTagAction = UnexpectedTagActionEnum.Error,
                        WarningLogger = new TextWriterWarningLogger(Console.Error)
                    })
                .RemoveRedundantLineBreaks();
        }

        private static Dictionary<string, string> _MemberNamePrefixDict = new Dictionary<string, string>(
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
        /// Write out the given XML Node as Markdown. Recursive function used internally.
        /// </summary>
        /// <param name="node">The xml node to write out.</param>
        /// <param name="context">The Conversion Context that will be passed around and manipulated over the course of the translation.</param>
        /// <returns>The converted markdown text.</returns>
        internal static string ToMarkDown(this XNode node, ConversionContext context)
        {
            if (node is XDocument document)
            {
                node = document.Root;
            }

            string name;
            if (node.NodeType == XmlNodeType.Element)
            {
                var el = (XElement)node;
                name = el.Name.LocalName;
                if (name == "member")
                {
                    if (!ReflectionHelper.IsPublic(el))
                    {
                        return string.Empty;
                    }
                    if (!_MemberNamePrefixDict.TryGetValue(
                        el.Attribute("name").Value.Substring(0, 2),
                        out string expandedName))
                    {
                        expandedName = "none";
                    }
                    name = expandedName.ToLowerInvariant();
                }
                else if (name == "see")
                {
                    var anchor = el.Attribute("cref") != null && el.Attribute("cref").Value.StartsWith("!:#");
                    name = anchor ? "seeAnchor" : "seePage";
                }
                else if (name.EndsWith("param") &&
                    !name.Equals("typeparam") &&
                    node.ElementsBeforeSelf().LastOrDefault()?.Name?.LocalName != "param")
                {
                    //treat first Param element separately to add table headers.
                    name = "firstparam";
                }
                else if (name == "typeparam" &&
                    node.ElementsBeforeSelf().LastOrDefault()?.Name?.LocalName != "typeparam")
                {
                    //treat first TypeParam element separately to add table headers.
                    name = "firsttypeparam";
                }
                else if (name == "seealso" && node.ElementsBeforeSelf().LastOrDefault()?.Name?.LocalName != "seealso")
                {
                    // treat first seealso element separately to add title
                    name = "firstseealso";
                }
                else if (name == "summary")
                {
                    bool? test = el.Parent.Attribute("name")?.Value.Contains("F:");
                    if (test ?? false)
                    {
                        name = "fieldsummary";
                    }
                }
                else if (name == "remarks")
                {
                    // check if remarks content is empty and if so than set name to nameremarks
                    if (el.Value == string.Empty)
                    {
                        name = "nameremarks";
                    }
                }
                // Check if contains #ctor and if so, set name to constructor
                if (name == "method" && el.Attribute("name")?.Value.Contains("#ctor") == true)
                {
                    name = "constructor";
                }
                try
                {
                    // Get the Last Node before this one, to see if it's a type node. and check if public
                    XElement lastNode = (node.NodesBeforeSelf()
                        .LastOrDefault(x => ReflectionHelper.IsPublic(x as XElement)) as XElement);
                    if (lastNode != null)
                    {
                        // Get the name attribute of the current node and the last node.
                        string nameAttribute = el.Attribute("name")?.Value;
                        string lastNameAttribute = lastNode.Attribute("name")?.Value;

                        // treat the first method separately to add header. Check if last was a member, and if so, check if it has a M: in the Attribute name.
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
                        // treat the first property separately to add header. Check if last was a member, and if so, check if it has a P: in the Attribute name.
                        if (name == "property" &&
                            lastNode?.Name?.LocalName == "member" &&
                            !lastNameAttribute.StartsWith("P:"))
                        {
                            name = "firstproperty";
                        }
                        // treat the first field separately to add header. Check if last was a member, and if so, check if it has a F: in the Attribute name.
                        if (name == "field" &&
                            lastNode?.Name?.LocalName == "member" &&
                            !lastNameAttribute.StartsWith("F:"))
                        {
                            name = "firstfield";
                        }
                        // treat the first event separately to add header. Check if last was a member, and if so, check if it has a E: in the Attribute name.
                        if (name == "event" &&
                            lastNode?.Name?.LocalName == "member" &&
                            !lastNameAttribute.StartsWith("E:"))
                        {
                            name = "firstevent";
                        }
                    }
                }
                catch (Exception)
                {
                }

                try
                {
                    var vals = TagRenderer.Dict[name].ValueExtractor(el, context).ToArray();
                    return string.Format(TagRenderer.Dict[name].FormatString, args: vals);
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
                            context.WarningLogger
                                .LogWarning(
                                    $@"Unknown element type ""{name}"" on line {lineInfo.LineNumber}, pos {lineInfo.LinePosition}");
                            break;
                        case UnexpectedTagActionEnum.Accept:
                            //do nothing;
                            break;
                        default:
                            throw new InvalidOperationException($"Unexpected {nameof(UnexpectedTagActionEnum)}");
                    }
                }
            }


            if (node.NodeType == XmlNodeType.Text)
                return Regex.Replace(((XText)node).Value.Replace('\n', ' '), @"\s+", " ");

            return string.Empty;
        }

        private static readonly Regex _PrefixReplacerRegex = PrefixReplacerRegex();

        internal static string[] ExtractNameAndBodyFromMemberProps(XElement node, ConversionContext context)
        {
            string[] values = ExtractNameAndBodyFromMember(node, context);
            if (values.Length == 2 && !node.Attribute("name").Value.Contains("F:"))
            {
                values[1] = values[1].TrimEnd().RemoveRedundantLineBreaks();
            }

            string className = node.Attribute("name")?.Value;
            string type = className.Split(':')[0];
            //remove the type prefix
            className = _PrefixReplacerRegex.Replace(className, string.Empty);
            // get property/field name
            string attributeName = node.Attribute("name").Value;
            // get property/field by getting the last part of the attribute name
            attributeName = attributeName.Split('.').Last();
            // remove the attribute name from the class name
            Type retType = ReflectionHelper.GetReturnType(className, type, attributeName);
            if (retType == null)
            {
                return [values[0], values[1], CleanUpTypeForUrl(type)];
            }
            // try to get lists and the type of the list
            if (retType.IsGenericType)
            {
                string collectionName = "";
                if (retType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    collectionName = "List" + '‹' + retType.GetGenericArguments()[0].Name + '›';
                }
                else if (retType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    collectionName = "Dictionary" + '‹' + retType.GetGenericArguments()[0].Name + ", " + retType.GetGenericArguments()[1].Name + '›';
                }
                collectionName = $"[{collectionName}](#{retType})";
                return [values[0], values[1], collectionName];
            }
            string displayAndUrl = string.Format(
                "[{0}]({1})",
                retType.Name.Replace("[]", string.Empty),
                GenerateUrl(retType));
            displayAndUrl = CleanUpTypeForUrl(displayAndUrl);
            if (IsGitHub)
                displayAndUrl = $"[{(retType.Name.Replace("[]", string.Empty))}](#{(retType.Name.Replace("[]", string.Empty).ToLowerInvariant())})";
            string[] strings = [values[0], values[1], displayAndUrl];
            return strings;
        }

        private static string CleanUpTypeForUrl(string s)
        {
            bool isRef = s.Contains('@');
            bool isArray = s.Contains("[]");
            if (s.Contains('#'))
            {
                string temp = s.Split('#')[1];
                if (isRef)
                    temp.Replace("@", string.Empty);
                if (isArray)
                    s = s.Replace("[]", string.Empty);
                if (temp.Contains(')'))
                    temp = temp.Split(')')[0];
                if (_types.ContainsKey(temp))
                {
                    temp.Replace("System.", string.Empty);
                    s = _types[temp.ToLower()];
                }
                if (isRef)
                    s += "@";
                if (isArray)
                    s += "[]";
            }
            return s;
        }

        private static readonly Dictionary<string, string> _types = new Dictionary<string, string>
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
            string retTypeName = string.Empty;
            string className = node.Parent.Attribute("name")?.Value;
            string type = className.Split(':')[0];
            className = className.Split('(')[0].Split(':')[1];
            // Remove Method name from class name
            if (type == "M" || type == "P" || type == "F")
                className = className.Remove(className.LastIndexOf('.'));
            // Get method name
            string methodName = node.Parent.Attribute("name").Value.Split(':')[1];
            methodName = methodName.Remove(0, className.Length + 1);
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
                Type retType = ReflectionHelper.GetReturnType(className, type, methodName);
                if (retType == null)
                    retTypeName = node.Value;
                else
                    retTypeName = retType.Name;
            }
            _types.TryGetValue(retTypeName.ToLower(), out string newRetTypeName);
            if (newRetTypeName != null)
                retTypeName = newRetTypeName;
            else if (retTypeName is null)
                retTypeName = node.Value;
            string[] values = [retTypeName];
            return values;
        }


        internal static string[] ExtractNameAndBodyFromMember(XElement node, ConversionContext context)
        {
            bool isMethod = node.Name.LocalName == "member" && node.Attribute("name").Value.StartsWith("M:");
            bool isPropertyOrField = node.Name.LocalName == "member" &&
                (node.Attribute("name").Value.StartsWith("P:") || node.Attribute("name").Value.StartsWith("F:"));
            var name = node.Attribute("name")?.Value;
            if (name == null)
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
                name = name.Remove(name.IndexOf('('));
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
            //name = _PrefixReplacerRegex.Replace(name, match => _MemberNamePrefixDict[match.Value] + " "); //expand prefixes into more verbose words for member.

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
            //name = GetGenerics(node, name);
            // remove everything after ` in the name
            if (name.Contains('`'))
            {
                name = name.Remove(name.IndexOf('`'));
            }
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
                name = name.Substring(name.LastIndexOf('.') + 1);
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
                int genericCount = int.Parse(name.Substring(name.IndexOf('`') + 1, 1));
                string generics = "‹";
                foreach (XElement el in node.Nodes())
                {
                    if (el.Name.LocalName != "typeparam")
                        continue;
                    generics += el?.Attribute("name").Value + ", ";
                }
                if (generics.Length > 2)
                    generics = generics.Remove(generics.Length - 2);
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
                .Where(n => n.NodeType == XmlNodeType.Element && ((XElement)n).Name.LocalName == "param");
            int count = 0;
            string param = string.Empty;
            foreach (var paramNode in paramNodes)
            {
                if (count == 0)
                {
                    name += "(";
                }
                // get the Name attribute
                var paramNodeName = ((XElement)paramNode).Attribute("name").Value;
                // get the type of the parameter
                if (parameters.Length > count)
                {
                    string type = parameters[count];
                    // if temp contains Collections.Generic.List{ then replace it with List<
                    if (type.Contains("System.Collections.Generic.List{"))
                    {
                        string classTemp = type.Split('{').Last();
                        type = type.Split('{').First();
                        type = type.Replace("System.Collections.Generic.List", "List‹");
                        classTemp = classTemp.RemoveNamespace(context.AssemblyName);
                        type += classTemp.Replace("}", "›");
                    }
                    else
                    {
                        if (type.Contains("System."))
                        {
                            string tempType = type.Split('.').Last();
                            bool isArray = tempType.Contains("[]");
                            if (isArray)
                                tempType = tempType.Replace("[]", string.Empty);
                            bool isReferenceType = tempType.Contains('@');
                            if (isReferenceType)
                                tempType = tempType.Replace("@", string.Empty);
                            if (_types.TryGetValue(tempType.ToLower(), out tempType))
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
                        type = type.Substring(type.LastIndexOf('.') + 1);
                    param += $"{type} {paramNodeName}";
                }
                // if the param is not the last add a comma
                if (count < paramNodes.Count() - 1)
                {
                    param += ", ";
                }
                // if last param then add the closing )
                if (count == paramNodes.Count() - 1)
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
            // if contains DevExpress then remove the first 3 parts of the namespace
            if (s.Contains("DevExpress"))
            {
                var parts = s.Split('.');
                if (parts.Length > 3)
                {
                    s = string.Join(".", parts.Skip(3));
                }
            }
            return s;
        }

        internal static string[] ExtractNameAndBody(string att, XElement node, ConversionContext context)
        {
            string name = node.Attribute(att)?.Value;
            if (!string.IsNullOrWhiteSpace(name))
                name = name.RemoveNamespace(context.AssemblyName);
            string body = node.Nodes().ToMarkDown(context);
            if (string.IsNullOrWhiteSpace(body))
            {
                body = char.ToUpper(name[0]) + name.Substring(1);
            }
            return [name, body];
        }

        internal static string[] ExtractNameAndUrl(string att, XElement node, ConversionContext context)
        {
            string name = node.Attribute(att)?.Value;
            string url = name;
            string assemblyName = context.AssemblyName;
            string display = name;
            // only text after the last dot should remain in the display name
            // Find the node "member" with the attribute name of the name
            if (display.Contains("M:") && display.Contains('('))
            {
                // Grab the root and x.Element("members").Elements("member")
                var members = node.Document.Root.Element("members").Elements("member");
                // Find the member with the name attribute of the display
                var member = members.FirstOrDefault(m => m.Attribute("name").Value == name);
                // if found then get the param nodes and add them to the display
                if (member != null)
                {
                    var parameters = display.Split('(').Last().Split(')').First().Split(',');
                    // remove everything to the right including (
                    display = display.Substring(0, display.IndexOf('('));
                    // remove everything to the left including the last .
                    display = display.Substring(display.LastIndexOf('.') + 1);
                    // add the () back with the params in the middle
                    display = GetParams(member, context, display, parameters);
                }
            }
            else if (display.Contains("M:"))
            {
                // remove all the stuff before the last .
                display = display.Substring(display.LastIndexOf('.') + 1);
                if (!display.Contains('('))
                {
                    display += @"()";
                }
            }
            else
            {
                // remove everything to the left including the last .
                display = display.Substring(display.LastIndexOf('.') + 1);
            }


            // Remove everything after ` in the name and display
            if (url.Contains('`'))
            {
                url = name.Remove(url.IndexOf('`'));
                display = display.Remove(display.IndexOf('`'));
            }
            if (url.Contains("T:"))
            {
                url = url.Replace("T:", string.Empty);
                if (url.Contains(context.AssemblyName))
                {
                    if (!IsGitHub)
                        url = url.RemoveNamespace(context.AssemblyName);
                }
                else
                {
                    // get the assembly name from the url
                    int count = url.Count(x => x == '.');
                    string[] parts = url.Split('.');
                    assemblyName = parts.Take(count).Aggregate((x, y) => x += $".{y}");
                }
            }
            if (!IsGitHub)
                url = url.RemoveNamespace(context.AssemblyName);
            // Replace "T: with ""
            url = url.Replace("N:", string.Empty);
            if (name.Contains("M:"))
            {
                url = display;
                // Grab the displayname and clean up the params
                url = url.Replace("M:", string.Empty);
                url = url.Replace("(", string.Empty);
                url = url.Replace(")", string.Empty);
                url = url.Replace(",", "-");
                url = url.Replace(" ", "-");
            }
            // replace dots with ""
            url = url.Replace(".", string.Empty);
            if (IsGitHub)
            {
                return [display, "#" + url.ToLowerInvariant()];
            }
            return [display, "../" + assemblyName + "/#" + url.ToLowerInvariant()];
        }

        internal static string ToMarkDown(this IEnumerable<XNode> es, ConversionContext context)
        { return es.Aggregate(string.Empty, (current, x) => current + x.ToMarkDown(context)); }

        internal static string ToCodeBlock(this string s)
        {
            var lines = s.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return string.Empty;
            var blank = lines[0].TakeWhile(x => x == ' ').Count() - 4;
            return string.Join("\n", lines.Select(x => new string(x.SkipWhile((y, i) => i < blank).ToArray())))
                .TrimEnd();
        }

        internal static string RemoveRedundantLineBreaks(this string s) { return Regex.Replace(s, @"\n\n\n+", "\n\n"); }

        internal static string[] ExtractName(XElement x, ConversionContext context)
        {
            string[] strings = [string.Empty];
            strings[0] = x.Parent.Attribute("name")?.Value;
            // remove all the stuff before the last .
            strings[0] = strings[0].Substring(strings[0].LastIndexOf('.') + 1);
            return strings;
        }


        /// <summary>
        /// Generate the url for the type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static string GenerateUrl(Type type)
        {
            string url = type.Name;
            // Remove everything after ` in the url
            if (url.Contains('`'))
            {
                url = type.Name.Remove(url.IndexOf('`'));
            }
            // add the namespace to the url
            url = type.Namespace + "." + url;
            //remove assembly name
            url = url.RemoveNamespace(type.Assembly.GetName().Name);
            // replace dots with ""
            url = url.Replace(".", string.Empty);
            // replace not allowed characters with -
            url = url.Replace(",", "-");
            url = url.Replace(" ", "-");
            // return the the url
            return "../" + type.Assembly.GetName().Name + "/#" + url.ToLowerInvariant();
        }

        [GeneratedRegex(@"(^[A-Z]\:)")]
        private static partial Regex PrefixReplacerRegex();

        internal static IEnumerable<string> ExtractUrl(string v, XElement x, ConversionContext context)
        {
            // get the href attribute
            string href = x.Attribute(v)?.Value;
            // get the display name
            string display = x.Value;
            // if the href is null then return the display
            if (href is null)
                return [display];
            // if the href is not null then return the display and the href
            return [display, href];
        }

        internal static IEnumerable<string> GenerateList(XElement element, ConversionContext context)
        {
            // ExtractNameAndBodyFromMember
            // "\n\n**{0}:**  {1}\n\n"
            string[] strings = ExtractNameAndBodyFromMember(element, context);
            // "\n\n**{0}:**" for the first element if it's not empty
            if (!string.IsNullOrWhiteSpace(strings[0]))
                strings[0] = "\n\n**{0}:**";
            return strings;
        }
    }
}
