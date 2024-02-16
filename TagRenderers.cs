using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace ROBdk97.XmlDocToMd
{
    internal class TagRenderer
    {
        internal TagRenderer(string formatString, Func<XElement, ConversionContext, IEnumerable<string>> valueExtractor)
        {
            FormatString = formatString;
            ValueExtractor = valueExtractor;
        }

        internal string FormatString { get; } = string.Empty;

        internal Func<XElement, //xml Element to extract from 
            ConversionContext, //context
            IEnumerable<string> //resultant list of values that will get used with formatString
        > ValueExtractor;

        internal static Dictionary<string, TagRenderer> Dict
        {
            get;
        } = new Dictionary<string, TagRenderer>()
        {
            ["doc"] =
                new TagRenderer(
                    "# {0}\n{1}\n\n{2}\n\n",
                    (x, context) => [
                        x.Element("assembly").Element("name").Value,
                        x.Element("assembly")
                        .ToMarkDown(context.MutateAssemblyName(x.Element("assembly").Element("name").Value)),
                        x.Element("members")
                        .Elements("member")
                        .ToMarkDown(context.MutateAssemblyName(x.Element("assembly").Element("name").Value))
                ]),
            ["type"] = new TagRenderer("\n---\n## {0}\n\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["firstfield"] =
                new TagRenderer(
                    "\n#### Fields:" + "\n|Name | Type | Description |\n|-----|------|------|\n" + "|{0}|{2}|{1}|\n",
                    XmlToMarkdown.ExtractNameAndBodyFromMemberProps),
            ["field"] = new TagRenderer("|{0}|{2}|{1}|\n", XmlToMarkdown.ExtractNameAndBodyFromMemberProps),
            ["firstproperty"] =
                new TagRenderer(
                    "\n#### Properties:" + "\n|Name | Type | Description |\n|-----|------|------|\n" + "|{0}|{2}|{1}|\n",
                    XmlToMarkdown.ExtractNameAndBodyFromMemberProps),
            ["property"] = new TagRenderer("|{0}|{2}|{1}|\n", XmlToMarkdown.ExtractNameAndBodyFromMemberProps),
            ["firstmethod"] =
                new TagRenderer("\n#### Methods:\n\n" + "##### {0}\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["method"] = new TagRenderer("\n##### {0}\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["firstconstructor"] =
                new TagRenderer(
                    "\n#### Constructors:\n\n" + "##### {0}\n{1}\n",
                    XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["constructor"] = new TagRenderer("\n##### {0}\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["firstevent"] =
                new TagRenderer("\n#### Events:\n" + "##### {0}\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["event"] = new TagRenderer("##### {0}\n{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["summary"] = new TagRenderer("{0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["fieldsummary"] = new TagRenderer("{0}", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["value"] = new TagRenderer("**Value**: {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["nameremarks"] = new TagRenderer("{0}", XmlToMarkdown.ExtractName),
            ["remarks"] = new TagRenderer("\n{0}\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["example"] = new TagRenderer("\n##### Example\n{0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["para"] = new TagRenderer("  \n {0}  ", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["code"] =
                new TagRenderer(
                    "\n```{0}\n{1}\n```\n",
                    (x, context) => [x.Attribute("lang")?.Value ?? "cs", x.Value.ToCodeBlock()]),
            ["seePage"] =
                new TagRenderer("[{0}]({1})", (x, context) => XmlToMarkdown.ExtractNameAndUrl("cref", x, context)),
            ["firstseealso"] =
                new TagRenderer(
                    "\n\nSee also:\n\n" + "- [{0}]({1})\n\n",
                    (x, context) => XmlToMarkdown.ExtractNameAndUrl("cref", x, context)),
            ["seealso"] =
                new TagRenderer("- [{0}]({1})\n\n", (x, context) => XmlToMarkdown.ExtractNameAndUrl("cref", x, context)),
            ["seeAnchor"] =
                new TagRenderer(
                    "[{1}]({0})",
                    (x, context) =>
                    {
                        var xx = XmlToMarkdown.ExtractNameAndUrl("cref", x, context);
                        xx[0] = xx[0].ToLower();
                        return xx;
                    }),
            ["firsttypeparam"] =
                new TagRenderer(
                    "\n#### Generics:\n" + "\n|Name | Description |\n|-----|------|\n" + "|{0}: |{1}|\n",
                    XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["typeparam"] = new TagRenderer("|{0}: |{1}|\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["firstparam"] =
                new TagRenderer(
                    "\n|Name | Description |\n|-----|------|\n|{0}|{1}|\n",
                    (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)),
            ["param"] =
                new TagRenderer("|{0}|{1}|\n", (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)),
            ["paramref"] =
                new TagRenderer("`{0}`", (x, context) => XmlToMarkdown.ExtractNameAndBody("name", x, context)),
            ["typeparamref"] = new TagRenderer("`{0}`", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["exception"] =
                new TagRenderer(
                    "**Throws:** [{0}]({1})\n\n",
                    (x, context) => XmlToMarkdown.ExtractNameAndUrl("cref", x, context)),
            ["returns"] = new TagRenderer("\n**Returns:** {0}\n\n", XmlToMarkdown.GetReturnType),
            ["c"] = new TagRenderer(" `{0}` ", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["none"] = new TagRenderer(string.Empty, (x, context) => []),
            ["name"] = new TagRenderer(string.Empty, (x, context) => []),
            ["assembly"] = new TagRenderer("{1}\n", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["list"] = new TagRenderer("{0}{1}", XmlToMarkdown.GenerateList),
            ["item"] = new TagRenderer("\n- {0}{1}  ", XmlToMarkdown.ExtractNameAndBodyFromMember),
            ["description"] = new TagRenderer("{0} {1}", XmlToMarkdown.ExtractNameAndBodyForListDescription),
            ["b"] = new TagRenderer("**{0}**", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["br"] = new TagRenderer("{0}  \n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["a"] = new TagRenderer("[{0}]({1})", (x, context) => XmlToMarkdown.ExtractUrl("href", x, context)),
            ["h1"] = new TagRenderer("# {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["h2"] = new TagRenderer("## {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["h3"] = new TagRenderer("### {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
            ["h4"] = new TagRenderer("#### {0}\n\n", (x, context) => [x.Nodes().ToMarkDown(context)]),
        };
    }
}
