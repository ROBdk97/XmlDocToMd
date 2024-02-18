using System;
using System.Linq;

namespace ROBdk97.XmlDocToMd
{
    /// <summary>
    /// This application converts XML documentation files into Markdown format, facilitating documentation generation compatible with MkDocs. MkDocs is a static site generator optimized for creating project documentation from Markdown files, configured via a YAML file. The tool supports a range of customization options for processing XML documentation, including file and namespace exclusion, output formatting, and handling of unsupported XML tags.
    /// <para>For comprehensive guidance on leveraging MkDocs with the generated markdown, see <a href="https://www.mkdocs.org/">MkDocs website</a>.</para>
    /// <para>Review the <see cref="Options"/> class for an enumeration of configurable parameters impacting conversion behavior.</para>
    /// <para>See supported XML documentation tags see <a href="#supported-xml-documentation-tags">here</a>.</para>
    /// <para>To add a description of the assembly, add a summary tag to a <a href="AssemblyDoc.cs">AssemblyDoc</a> class.</para>
    /// <para>There is also a settings.json <a href="settings.json">Example</a> to filter out Files and Namespaces</para>
    /// <para><b>Notice:</b> As this tool uses Reflection to add additional information to the XML documentation, the DLL´s of the project must be in the same directory as the XML documentation files.</para>
    /// <example>
    /// The following examples demonstrate various ways to use the XmlDocToMd tool to convert XML documentation to Markdown format, showcasing the flexibility and range of options available:
    /// 
    /// Basic conversion of a single XML file to Markdown:
    /// <code lang="bat">
    /// XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md"
    /// </code>
    /// 
    /// Convert all XML documentation files in a specific directory to Markdown, outputting to a designated directory. This example uses the search directory and directory name options to filter XML files located in a 'Release' subdirectory:
    /// <code lang="bat">
    /// XmlDocToMd.exe -s "C:\Project\Docs" -d "Release" -o "C:\Project\MarkdownDocs"
    /// </code>
    /// 
    /// Use a secondary output directory for additional file handling, such as backup or versioning, demonstrating the tool's support for complex documentation workflows:
    /// <code lang="bat">
    /// XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\Markdown\output.md" -l "C:\Backup\Docs"
    /// </code>
    /// 
    /// Handle unexpected XML tags by issuing a warning, allowing for the identification and resolution of issues in the source XML without halting the conversion process:
    /// <code lang="bat">
    /// XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md" -u Warn
    /// </code>
    /// 
    /// Convert XML documentation to Markdown using Git Markdown format for URLs, which is useful when the generated Markdown will be hosted on GitHub or a similar platform:
    /// <code lang="bat">
    /// XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\README.md" -g
    /// </code>
    /// 
    /// Name the output Markdown file "README.md" instead of using the assembly name, making it ready for use as a GitHub repository's landing page documentation:
    /// <code lang="bat">
    /// XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md" -r
    /// </code>
    /// 
    /// <b>Visual Studio post-build event example to convert XML documentation to Markdown when building a project with custom configuration:</b>
    /// <code lang="xml"><![CDATA[
	///<Target Name = "PostBuild" AfterTargets="PostBuildEvent" Condition="'$(ConfigurationName)' == 'XMLtoMD'">
	///	<Exec Command = "call &quot;$(TargetDir)$(TargetName).exe&quot; -s &quot;$(ProjectDir)\&quot; -d &quot;$(ConfigurationName)&quot; -o &quot;$(ProjectDir)\&quot; -g -r" />
    ///</Target>]]>
    /// </code>
    /// 
    /// These examples illustrate just a few of the ways the XmlDocToMd tool can be configured to meet specific documentation conversion needs, from simple file conversions to more complex scenarios involving multiple directories, console integration, and special formatting requirements.
    /// </example>
    /// <h3>Supported XML Documentation Tags</h3>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Common Tags:
    /// <list type="bullet">
    /// <item>
    /// <description><c>doc</c>: The root tag for the documentation file. It's not used within code comments but in the generated XML file.</description>
    /// </item>
    /// <item>
    /// <description><c>summary</c>: Describes a type, method, property, field, or event.</description>
    /// </item>
    /// <item>
    /// <description><c>remarks</c>: Provides additional information about a type or member, often used to explain the behavior not covered in the <c>summary</c>.</description>
    /// </item>
    /// <item>
    /// <description><c>example</c>: Gives an example of how to use a method, class, or other member.</description>
    /// </item>
    /// <item>
    /// <description><c>code</c>: Encloses example code. The <c>lang</c> attribute can specify the language of the code snippet (e.g., lang="C#").</description>
    /// </item>
    /// <item>
    /// <description><c>param name="name"</c>: Describes one of the parameters for a method. Replace <c>name</c> with the name of the parameter.</description>
    /// </item>
    /// <item>
    /// <description><c>typeparam name="name"</c>: Describes the type parameter for a generic type or method. Replace <c>name</c> with the name of the type parameter.</description>
    /// </item>
    /// <item>
    /// <description><c>returns</c>: Describes the return value of a method.</description>
    /// </item>
    /// <item>
    /// <description><c>exception cref="exceptionType"</c>: Indicates what exceptions can be thrown by a method. Replace <c>exceptionType</c> with the type of the exception.</description>
    /// </item>
    /// <item>
    /// <description><c>see cref=""/></c>: Creates a hyperlink to the specified member or type. Use the <c>cref</c> attribute to specify the code element.</description>
    /// </item>
    /// <item>
    /// <description><c>seealso cref=""/></c>: Similar to <c>see</c>, but typically used in the context of a "See Also" section.</description>
    /// </item>
    /// <item>
    /// <description><c>value</c>: Describes the value of a property.</description>
    /// </item>
    /// <item>
    /// <description><c>list</c>: Used to format a list of items. The <c>type</c> attribute specifies the list type (e.g., bullet, number, table), and the <c>name</c> attribute gives a name to the list.</description>
    /// </item>
    /// <item>
    /// <description><c>item</c>: Defines an item in a list. Often used within <c>list</c>.</description>
    /// </item>
    /// </list>
    /// </para>
    /// <br></br>
    /// <br></br>
    /// <para>
    /// Formatting Tags:
    /// <list type="bullet">
    /// <item>
    /// <description><c>para</c>: Starts a new paragraph.</description>
    /// </item>
    /// <item>
    /// <description><c>c</c>: Marks text as code within a description.</description>
    /// </item>
    /// <item>
    /// <description><c>paramref name="name"</c>: References a parameter within a description. Replace the placeholder with the parameter's name.</description>
    /// </item>
    /// <item>
    /// <description><c>typeparamref name="name"</c>: References a type parameter within a description.</description>
    /// </item>
    /// <item>
    /// <description><c>name</c>: Typically used within other tags to specify names but not a standalone tag in XML documentation comments.</description>
    /// </item>
    /// <item>
    /// <description><c>description</c>: Not a standard XML documentation tag in C#. Descriptions are typically included within the content of tags like <c>item</c> .</description>
    /// </item>
    /// </list>
    /// </para>
    /// <br></br>
    /// <br></br>
    /// <para>
    /// HTML-like Tags:
    /// <list type="bullet">
    /// <item>
    /// <description><c>b</c>: Bold text.</description>
    /// </item>
    /// <item>
    /// <description><c>br</c>: Line break.</description>
    /// </item>
    /// <item>
    /// <description><c>a href="url"</c>: Creates a hyperlink. Replace <c>url</c> with the target URL.</description>
    /// </item>
    /// <item>
    /// <description><c>h1</c>, <c>h2</c>, <c>h3</c>, <c>h4</c>: Headers of different levels.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public abstract class AssemblyDoc
    {
        // No implementation required.
    }
}
