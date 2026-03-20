namespace ROBdk97.XmlDocToMd.Rendering;

/// <summary>
/// Factory that creates the complete set of built-in XML-tag-to-Markdown rendering
/// strategies used by the converter.
/// </summary>
/// <remarks>
/// Call <see cref="CreateStrategies"/> once at startup and pass the result to an
/// <see cref="ITagRendererRegistry"/> (or the DI container via
/// <see cref="Infrastructure.ServiceCollectionExtensions.AddXmlDocToMd"/>).
/// <note>
/// The order of strategies within the returned collection does not affect dispatch
/// correctness — the registry builds a dictionary and looks up strategies by tag name.
/// </note>
/// <tip>
/// To extend the built-in set, call <see cref="CreateStrategies"/>, append your own
/// strategies, and pass the combined enumerable to the registry constructor.
/// </tip>
/// </remarks>
internal static class TagRenderers
{
    /// <summary>
    /// Returns one <see cref="ITagRenderStrategy"/> per supported XML documentation tag.
    /// </summary>
    /// <returns>
    /// An enumerable of strategies covering all standard C# XML doc tags, HTML-like
    /// pass-through tags, GFM alert blocks, and structural member tags used internally
    /// by the converter. See <see cref="AssemblyDoc"/> for the full supported-tag reference.
    /// </returns>
    internal static IEnumerable<ITagRenderStrategy> CreateStrategies()
    {
        return
        [
            new TagRenderStrategy(
                "doc",
                "# {0}\n{1}\n\n{2}\n\n",
                (x, context) =>
                {
                    XElement? assemblyElement = x.Element("assembly");
                    if (assemblyElement is null)
                        return [string.Empty, string.Empty, string.Empty];

                    XElement? nameElement = assemblyElement.Element("name");
                    string assemblyName = nameElement?.Value ?? string.Empty;

                    string assemblyMarkdown = assemblyElement.ToMarkDown(context.MutateAssemblyName(assemblyName));

                    XElement? membersElement = x.Element("members");
                    string membersMarkdown = membersElement?.Elements("member").ToMarkDown(context.MutateAssemblyName(assemblyName)) ?? string.Empty;

                    return [assemblyName, assemblyMarkdown, membersMarkdown];
                }),
            new TagRenderStrategy("type", "\n---\n## {0}\n\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy(
                "firstfield",
                "\n**Fields**\n\n|Name|Type|Description|\n|---|---|---|\n|{0}|{2}|{1}|\n",
                XmlToMarkdown.ExtractNameAndBodyFromMemberProps),
            new TagRenderStrategy("field", "|{0}|{2}|{1}|\n", XmlToMarkdown.ExtractNameAndBodyFromMemberProps),
            new TagRenderStrategy(
                "firstproperty",
                "\n**Properties**\n\n|Name|Type|Description|\n|---|---|---|\n|{0}|{2}|{1}|\n",
                XmlToMarkdown.ExtractNameAndBodyFromMemberProps),
            new TagRenderStrategy("property", "|{0}|{2}|{1}|\n", XmlToMarkdown.ExtractNameAndBodyFromMemberProps),
            new TagRenderStrategy(
                "firstmethod",
                "\n**Methods**\n\n### {0}\n\n{1}\n",
                XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy("method", "\n### {0}\n\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy(
                "firstconstructor",
                "\n**Constructors**\n\n### {0}\n\n{1}\n",
                XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy("constructor", "\n### {0}\n\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy(
                "firstevent",
                "\n**Events**\n\n### {0}\n\n{1}\n",
                XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy("event", "\n### {0}\n\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy("summary", "{0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("fieldsummary", "{0}", (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("value", "**Value**: {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("nameremarks", "{0}", XmlToMarkdown.ExtractName),
            new TagRenderStrategy("remarks", "\n{0}\n", (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("note",      "\n> [!NOTE]\n> {0}\n\n",      (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("warning",   "\n> [!WARNING]\n> {0}\n\n",   (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("tip",       "\n> [!TIP]\n> {0}\n\n",       (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("important", "\n> [!IMPORTANT]\n> {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("caution",   "\n> [!CAUTION]\n> {0}\n\n",   (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("example", "\n**Example**\n\n{0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("para", "\n\n{0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy(
                "code",
                "\n```{0}\n{1}\n```\n\n",
                (x, context) => [x.Attribute("lang")?.Value ?? "cs", x.Value.ToCodeBlock()]),
            new TagRenderStrategy(
                "seePage",
                "{0}",
                (x, context) => [XmlToMarkdown.FormatReference("cref", x, context)]),
            new TagRenderStrategy(
                "seeLangword",
                "`{0}`",
                (x, context) => [x.Attribute("langword")?.Value ?? x.Attribute("cref")?.Value ?? string.Empty]),
            new TagRenderStrategy(
                "firstseealso",
                "\n\nSee also:\n\n" + "- {0}\n",
                (x, context) => [XmlToMarkdown.FormatReference("cref", x, context)]),
            new TagRenderStrategy(
                "seealso",
                "- {0}\n",
                (x, context) => [XmlToMarkdown.FormatReference("cref", x, context)]),
            new TagRenderStrategy(
                "seeAnchor",
                "{0}",
                (x, context) =>
                {
                    var rendered = XmlToMarkdown.FormatReference("cref", x, context);
                    return [rendered];
                }),
            new TagRenderStrategy(
                "firsttypeparam",
                "\n**Generics**\n\n|Name|Description|\n|---|---|\n|{0}|{1}|\n",
                XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy("typeparam", "|{0}|{1}|\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy(
                "firstparam",
                "\n**Parameters**\n\n|Name|Description|\n|---|---|\n|{0}|{1}|\n",
                (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)),
            new TagRenderStrategy(
                "param",
                "|{0}|{1}|\n",
                (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)),
            new TagRenderStrategy(
                "paramref",
                "`{0}`",
                (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)),
            new TagRenderStrategy("typeparamref", "`{0}`", XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy(
                "exception",
                "**Throws:** {0}\n\n",
                (x, context) => [XmlToMarkdown.FormatReference("cref", x, context)]),
            new TagRenderStrategy("returns", "\n**Returns:** {0}\n\n", XmlToMarkdown.GetReturnType),
            new TagRenderStrategy(
                "inheritdoc",
                "*Inherited from `{0}`*\n",
                (x, context) =>
                {
                    var cref = x.Attribute("cref")?.Value;
                    return [cref != null ? cref.Split('.').Last() : "base type"];
                }),
            new TagRenderStrategy("c", "`{0}`", (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("none", string.Empty, (x, context) => []),
            new TagRenderStrategy("name", string.Empty, (x, context) => []),
            new TagRenderStrategy("assembly", "{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy("list", "{0}{1}",
                (x, context) =>
                {
                    var type = x.Attribute("type")?.Value ?? "bullet";
                    return type switch
                    {
                        "number" => XmlToMarkdown.GenerateOrderedList(x, context),
                        "table"  => XmlToMarkdown.GenerateTableList(x, context),
                        _        => XmlToMarkdown.GenerateList(x, context),
                    };
                }),
            new TagRenderStrategy("item", "\n- {0}{1}", XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy("description", "{0} {1}", XmlToMarkdown.ExtractNameAndBodyForListDescription),
            new TagRenderStrategy(
                "listheader",
                "|{0}|{1}|\n|---|---|\n",
                XmlToMarkdown.ExtractNameAndBodyFromMember),
            new TagRenderStrategy("term", "**{0}**", (x, context) => [x.Nodes().ToMarkDown(context)]),
            new TagRenderStrategy("b", "**{0}**", (x, context) => [x.Nodes().ToMarkDown(context)]),
            new TagRenderStrategy("strong", "**{0}**", (x, context) => [x.Nodes().ToMarkDown(context)]),
            new TagRenderStrategy("i",  "*{0}*",       (x, context) => [x.Nodes().ToMarkDown(context)]),
            new TagRenderStrategy("u",  "**{0}**",  (x, context) => [x.Nodes().ToMarkDown(context)]),
            new TagRenderStrategy("tt", "`{0}`",       (x, context) => [x.Nodes().ToMarkDown(context)]),
            new TagRenderStrategy("br", "{0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context).Trim()]),
            new TagRenderStrategy("a", "[{0}]({1})", (x, context) => XmlToMarkdown.ExtractUrl("href", x, context)),
            new TagRenderStrategy("h1", "# {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            new TagRenderStrategy("h2", "## {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            new TagRenderStrategy("h3", "### {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            new TagRenderStrategy("h4", "#### {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
        ];
    }
}
