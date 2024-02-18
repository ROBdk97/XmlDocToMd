# XmlDocToMd
 This application converts XML documentation files into Markdown format, facilitating documentation generation compatible with MkDocs. MkDocs is a static site generator optimized for creating project documentation from Markdown files, configured via a YAML file. The tool supports a range of customization options for processing XML documentation, including file and namespace exclusion, output formatting, and handling of unsupported XML tags.   
 For comprehensive guidance on leveraging MkDocs with the generated markdown, see [MkDocs website](https://www.mkdocs.org/).    
 Review the [Options](#robdk97xmldoctomdoptions) class for an enumeration of configurable parameters impacting conversion behavior.    
 See supported XML documentation tags see [here](#supported-xml-documentation-tags).    
 To add a description of the assembly, add a summary tag to a [AssemblyDoc](AssemblyDoc.cs) class.    
 There is also a settings.json [Example](settings.json) to filter out Files and Namespaces    
 Notice: As this tool uses Reflection to add additional information to the XML documentation, the DLL´s of the project must be in the same directory as the XML documentation files.  
##### Example
 The following examples demonstrate various ways to use the XmlDocToMd tool to convert XML documentation to Markdown format, showcasing the flexibility and range of options available: Basic conversion of a single XML file to Markdown: 
```bat
    XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md"
```
 Convert all XML documentation files in a specific directory to Markdown, outputting to a designated directory. This example uses the search directory and directory name options to filter XML files located in a 'Release' subdirectory: 
```bat
    XmlDocToMd.exe -s "C:\Project\Docs" -d "Release" -o "C:\Project\MarkdownDocs"
```
 Use a secondary output directory for additional file handling, such as backup or versioning, demonstrating the tool's support for complex documentation workflows: 
```bat
    XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\Markdown\output.md" -l "C:\Backup\Docs"
```
 Handle unexpected XML tags by issuing a warning, allowing for the identification and resolution of issues in the source XML without halting the conversion process: 
```bat
    XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md" -u Warn
```
 Convert XML documentation to Markdown using Git Markdown format for URLs, which is useful when the generated Markdown will be hosted on GitHub or a similar platform: 
```bat
    XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\README.md" -g
```
 Name the output Markdown file "README.md" instead of using the assembly name, making it ready for use as a GitHub repository's landing page documentation: 
```bat
    XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md" -r
```
**Visual Studio post-build event example to convert XML documentation to Markdown when building a project with custom configuration:**
```xml
    <Target Name = "PostBuild" AfterTargets="PostBuildEvent" Condition="'$(ConfigurationName)' == 'XMLtoMD'">
    	<Exec Command = "call &quot;$(TargetDir)$(TargetName).exe&quot; -s &quot;$(ProjectDir)\&quot; -d &quot;$(ConfigurationName)&quot; -o &quot;$(ProjectDir)\&quot; -g -r" />
    </Target>
```
 These examples illustrate just a few of the ways the XmlDocToMd tool can be configured to meet specific documentation conversion needs, from simple file conversions to more complex scenarios involving multiple directories, console integration, and special formatting requirements. 

### Supported XML Documentation Tags




  
  Common Tags: 
-   `doc` : The root tag for the documentation file. It's not used within code comments but in the generated XML file.  
-   `summary` : Describes a type, method, property, field, or event.  
-   `remarks` : Provides additional information about a type or member, often used to explain the behavior not covered in the  `summary` .  
-   `example` : Gives an example of how to use a method, class, or other member.  
-   `code` : Encloses example code. The  `lang`  attribute can specify the language of the code snippet (e.g., lang="C#").  
-   `param name="name"` : Describes one of the parameters for a method. Replace  `name`  with the name of the parameter.  
-   `typeparam name="name"` : Describes the type parameter for a generic type or method. Replace  `name`  with the name of the type parameter.  
-   `returns` : Describes the return value of a method.  
-   `exception cref="exceptionType"` : Indicates what exceptions can be thrown by a method. Replace  `exceptionType`  with the type of the exception.  
-   `see cref=""/>` : Creates a hyperlink to the specified member or type. Use the  `cref`  attribute to specify the code element.  
-   `seealso cref=""/>` : Similar to  `see` , but typically used in the context of a "See Also" section.  
-   `value` : Describes the value of a property.  
-   `list` : Used to format a list of items. The  `type`  attribute specifies the list type (e.g., bullet, number, table), and the  `name`  attribute gives a name to the list.  
-   `item` : Defines an item in a list. Often used within  `list` .      

  

  
  Formatting Tags: 
-   `para` : Starts a new paragraph.  
-   `c` : Marks text as code within a description.  
-   `paramref name="name"` : References a parameter within a description. Replace the placeholder with the parameter's name.  
-   `typeparamref name="name"` : References a type parameter within a description.  
-   `name` : Typically used within other tags to specify names but not a standalone tag in XML documentation comments.  
-   `description` : Not a standard XML documentation tag in C#. Descriptions are typically included within the content of tags like  `item`  .      

  

  
  HTML-like Tags: 
-   `b` : Bold text.  
-   `br` : Line break.  
-   `a href="url"` : Creates a hyperlink. Replace  `url`  with the target URL.  
-   `h1` ,  `h2` ,  `h3` ,  `h4` : Headers of different levels.    




---
## ROBdk97.XmlDocToMd.Options

 Options for customizing the behavior of the XML documentation to Markdown conversion. These options allow for detailed control over input and output paths, handling of console input/output, secondary output directories, unexpected tag management, and more. 



#### Properties:
|Name | Type | Description |
|-----|------|------|
|InputFile|[String](#string)| Gets or sets the input XML file path. Use -i or --inputfile followed by the file path to specify the input XML file.|
|ConsoleIn|[Boolean](#boolean)| Gets or sets a value indicating whether to read input from the console. Use --cin to read input from the console instead of a file.|
|OutputFile|[String](#string)| Gets or sets the output markdown file path. Use -o or --outputfile followed by the file path to specify the output markdown file.|
|SecondaryOutputDirectory|[String](#string)| Gets or sets the path to a secondary output directory. To copy to a network share for example. Use -l or --secondaryDir followed by the directory path to specify a secondary output directory.|
|ConsoleOut|[Boolean](#boolean)| Gets or sets a value indicating whether to write output to the console. Use --cout to write output to the console instead of a file.|
|UnexpectedTagAction|[UnexpectedTagActionEnum](#unexpectedtagactionenum)| Gets or sets the action to take on unexpected tags. Use -u or --unexpected followed by Accept, Warn, or Error to specify how to handle unexpected tags.|
|SearchDirectory|[String](#string)| Gets or sets the search directory for XML files. Use -s or --search followed by the directory path to specify where to search for .xml files. This is used in combination with the -d or --directory argument.|
|Directory|[String](#string)| Gets or sets the directory name to search for .xml files. Use -d or --directory followed by the directory name. This is used in combination with the -s or --search argument. Defaults to "Release".|
|Git|[Boolean](#boolean)| Gets or sets a value indicating whether to use Git Markdown format for URLs. Use -g or --git to use Git Markdown format.|
|Readme|[Boolean](#boolean)| Call the Markdown file "README.md" instead of AssemblyName.md Use -r or --readme to call the Markdown file "README.md" instead of AssemblyName.md|
|SettingsFile|[String](#string)| Gets or sets the settings file. Defaults to "settings.json". Use -f or --settings followed by the file path to specify the settings file to use.|

---
## ROBdk97.XmlDocToMd.Settings

 Json Settings file for the XmlDocToMd 



#### Properties:
|Name | Type | Description |
|-----|------|------|
|FilesToIgnore|[List‹String›](#System.Collections.Generic.List`1[System.String])| Wich xml files to ignore when converting to markdown|
|NameSpacesToRemove|[List‹String›](#System.Collections.Generic.List`1[System.String])| Wich namespaces to ignore when converting to markdown|

---
## ROBdk97.XmlDocToMd.UnexpectedTagActionEnum

 Specifies the manner in which unexpected tags will be handled 



#### Fields:
|Name | Type | Description |
|-----|------|------|
|Error|[UnexpectedTagActionEnum](#unexpectedtagactionenum)| No unexpected tags are allowed |
|Warn|[UnexpectedTagActionEnum](#unexpectedtagactionenum)| Warn on unexpected tags |
|Accept|[UnexpectedTagActionEnum](#unexpectedtagactionenum)| All unexpected tags are allowed |




---

Generated by [XmlDocToMd](https://github.com/ROBdk97/XmlDocToMd) by [ROBdk97](https://github.com/ROBdk97)