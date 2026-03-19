namespace ROBdk97.XmlDocToMd;

/// <summary>
/// Converts XML documentation files into GitHub-Flavored Markdown, producing output
/// compatible with <a href="https://www.mkdocs.org/">MkDocs</a> and GitHub Pages.
/// <para>
/// The tool reads the standard <tt>.xml</tt> file emitted by the C# compiler alongside
/// the compiled assembly, enriches member signatures via Reflection, and writes a
/// structured <tt>.md</tt> file.
/// </para>
/// <para>
/// See the <see cref="Cli.Options"/> class for every available command-line parameter, and
/// the <i>Supported XML Documentation Tags</i> section in <see cref="AssemblyDoc"/>
/// remarks for the full list of handled tags.
/// </para>
/// <para>
/// The tool looks for an empty public class named <c>AssemblyDoc</c> (abstract is
/// recommended) and places its XML documentation at the top of the generated Markdown
/// file. This is used to create a general documentation overview with references to the
/// most important parts and a high-level description, similar to the main section of a
/// README.
/// </para>
/// <note>
/// Because the tool uses Reflection to resolve member types and visibility, the
/// compiled <tt>.dll</tt> must reside in the same directory as the <tt>.xml</tt> file.
/// Conversion still succeeds without the DLL, but type information in tables will be
/// reduced to the raw prefix letter (<tt>F</tt>, <tt>P</tt>, …).
/// </note>
/// <example>
/// <h4>Basic single-file conversion</h4>
/// <code lang="bat">
/// XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md"
/// </code>
///
/// <h4>Batch conversion — search a directory tree</h4>
/// <para>
/// Searches <tt>C:\Project\Docs</tt> for sub-folders named <tt>Release</tt>, converts
/// every <tt>.xml</tt> found inside, and writes <tt>.md</tt> files to the output dir:
/// </para>
/// <code lang="bat">
/// XmlDocToMd.exe -s "C:\Project\Docs" -d "Release" -o "C:\Project\MarkdownDocs"
/// </code>
///
/// <h4>With a secondary (backup) output directory</h4>
/// <code lang="bat">
/// XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\Markdown\output.md" -l "C:\Backup\Docs"
/// </code>
///
/// <h4>Soft tag handling — warn instead of throwing</h4>
/// <code lang="bat">
/// XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md" -u Warn
/// </code>
///
/// <h4>GitHub-flavored URLs + README output</h4>
/// <code lang="bat">
/// XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\README.md" -g -r
/// </code>
///
/// <h4>Visual Studio post-build event (XMLtoMD configuration)</h4>
/// <code lang="xml"><![CDATA[
///<Target Name="PostBuild" AfterTargets="PostBuildEvent" Condition="'$(ConfigurationName)' == 'XMLtoMD'">
///  <Exec Command="call &quot;$(TargetDir)$(TargetName).exe&quot; -s &quot;$(ProjectDir)\&quot; -d &quot;$(ConfigurationName)&quot; -o &quot;$(ProjectDir)\&quot; -g -r" />
///</Target>]]>
/// </code>
/// </example>
/// <h3>Supported XML Documentation Tags</h3>
/// </summary>
/// <remarks>
/// <h4>Standard C# XML Doc Tags</h4>
/// <list type="table">
/// <listheader><term>Tag</term><description>Markdown output</description></listheader>
/// <item><term><c>doc</c></term><description>Top-level document root — emits an <c>#</c> heading with the assembly name.</description></item>
/// <item><term><c>summary</c></term><description>Rendered as a plain paragraph below the member heading.</description></item>
/// <item><term><c>remarks</c></term><description>Rendered as a paragraph block below the summary.</description></item>
/// <item><term><c>returns</c></term><description>Emits a <b>Returns:</b> line with the resolved return type.</description></item>
/// <item><term><c>value</c></term><description>Emits a <b>Value:</b> line for property descriptions.</description></item>
/// <item><term><c>example</c></term><description>Rendered under an <c>#####&#160;Example</c> sub-heading.</description></item>
/// <item><term><c>param</c></term><description>Collected into a two-column Markdown table (Name / Description).</description></item>
/// <item><term><c>typeparam</c></term><description>Collected into a two-column Generics table.</description></item>
/// <item><term><c>exception</c></term><description>Emits a <b>Throws:</b> linked entry per exception type.</description></item>
/// <item><term><c>inheritdoc</c></term><description>Emits <i>Inherited from `BaseType`</i>. The <c>cref</c> attribute controls the display name.</description></item>
/// </list>
///
/// <h4>Cross-Reference Tags</h4>
/// <list type="table">
/// <listheader><term>Tag / attribute</term><description>Markdown output</description></listheader>
/// <item><term><c>see cref=""</c></term><description>Inline hyperlink to the referenced member.</description></item>
/// <item><term><c>see langword=""</c></term><description>Renders the keyword as inline code, e.g. <see langword="null"/>, <see langword="true"/>.</description></item>
/// <item><term><c>see href=""</c></term><description>Inline hyperlink using the raw URL (anchor variant).</description></item>
/// <item><term><c>seealso</c></term><description>Appended to a <i>See also</i> bullet list at the end of the member block.</description></item>
/// <item><term><c>paramref</c></term><description>Renders the parameter name as inline code.</description></item>
/// <item><term><c>typeparamref</c></term><description>Renders the type-parameter name as inline code.</description></item>
/// </list>
///
/// <h4>List Tags</h4>
/// <tip>
/// Use <c>type="table"</c> for two-column reference tables, <c>type="number"</c> for
/// step-by-step instructions, and <c>type="bullet"</c> (the default) for unordered lists.
/// </tip>
/// <list type="table">
/// <listheader><term>Tag</term><description>Description</description></listheader>
/// <item><term><c>list type="bullet"</c></term><description>Unordered bullet list — each <c>item</c> becomes a <c>-</c> entry.</description></item>
/// <item><term><c>list type="number"</c></term><description>Ordered list — each <c>item/description</c> becomes a numbered entry.</description></item>
/// <item><term><c>list type="table"</c></term><description>Pipe table — <c>listheader</c> provides column headers; each <c>item</c> becomes a row.</description></item>
/// <item><term><c>listheader</c></term><description>Column header row for <c>type="table"</c> lists.</description></item>
/// <item><term><c>item</c></term><description>A single list entry; contains <c>term</c> and/or <c>description</c>.</description></item>
/// <item><term><c>term</c></term><description>Bold label in a table row or bullet entry.</description></item>
/// <item><term><c>description</c></term><description>Description text within an <c>item</c>.</description></item>
/// </list>
///
/// <h4>Inline Formatting Tags</h4>
/// <list type="table">
/// <listheader><term>Tag</term><description>Markdown output</description></listheader>
/// <item><term><c>c</c></term><description>Inline code span: <c>`text`</c></description></item>
/// <item><term><c>tt</c></term><description>Teletype / monospace — same output as <c>c</c>: <c>`text`</c></description></item>
/// <item><term><c>b</c></term><description><b>Bold</b> text: <c>**text**</c></description></item>
/// <item><term><c>i</c></term><description><i>Italic</i> text: <c>*text*</c></description></item>
/// <item><term><c>u</c></term><description><u>Underline</u> via HTML pass-through: <c>&lt;u&gt;text&lt;/u&gt;</c></description></item>
/// <item><term><c>para</c></term><description>Paragraph break inside a block element.</description></item>
/// <item><term><c>code</c></term><description>Fenced code block. Use the <c>lang</c> attribute for syntax highlighting, e.g. <c>lang="csharp"</c>.</description></item>
/// </list>
///
/// <h4>HTML Pass-Through Tags</h4>
/// <list type="table">
/// <listheader><term>Tag</term><description>Markdown output</description></listheader>
/// <item><term><c>a href="…"</c></term><description>Hyperlink: <c>[text](url)</c></description></item>
/// <item><term><c>br</c></term><description>Hard line break.</description></item>
/// <item><term><c>h1</c> – <c>h4</c></term><description>ATX headings at the corresponding level.</description></item>
/// </list>
///
/// <h4>GFM Alert Blocks <i>(DocFX / Sandcastle compatibility)</i></h4>
/// <note>
/// These tags map to GitHub-Flavored Markdown alert syntax (<c>&gt; [!NOTE]</c> etc.)
/// and render as coloured callout boxes on GitHub and MkDocs Material.
/// </note>
/// <list type="table">
/// <listheader><term>Tag</term><description>GFM alert type</description></listheader>
/// <item><term><c>note</c></term><description><c>&gt; [!NOTE]</c> — informational callout.</description></item>
/// <item><term><c>tip</c></term><description><c>&gt; [!TIP]</c> — helpful suggestion.</description></item>
/// <item><term><c>important</c></term><description><c>&gt; [!IMPORTANT]</c> — key information.</description></item>
/// <item><term><c>warning</c></term><description><c>&gt; [!WARNING]</c> — potential pitfall.</description></item>
/// <item><term><c>caution</c></term><description><c>&gt; [!CAUTION]</c> — dangerous action ahead.</description></item>
/// </list>
/// </remarks>
public abstract class AssemblyDoc
{
    // No implementation required.
}
