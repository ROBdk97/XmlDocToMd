# XmlDocToMd
 Converts XML documentation files into GitHub-Flavored Markdown, producing output compatible with [MkDocs](https://www.mkdocs.org/) and GitHub Pages.   
  The tool reads the standard `.xml` file emitted by the C# compiler alongside the compiled assembly, enriches member signatures via Reflection, and writes a structured `.md` file.     
  See the [Options](#robdk97xmldoctomdclioptions) class for every available command-line parameter, and the *Supported XML Documentation Tags* section in [AssemblyDoc](#robdk97xmldoctomdassemblydoc) remarks for the full list of handled tags.     
  The tool looks for an empty public class named  `AssemblyDoc`  (abstract is recommended) and places its XML documentation at the top of the generated Markdown file. This is used to create a general documentation overview with references to the most important parts and a high-level description, similar to the main section of a README.   
> [!NOTE]
>  Because the tool uses Reflection to resolve member types and visibility, the compiled `.dll` must reside in the same directory as the `.xml` file. Conversion still succeeds without the DLL, but type information in tables will be reduced to the raw prefix letter (`F`, `P`, …). 


##### Example
#### Basic single-file conversion


```bat
    XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md"
```

#### Batch conversion — search a directory tree

  
  Searches `C:\Project\Docs` for sub-folders named `Release`, converts every `.xml` found inside, and writes `.md` files to the output dir:   
```bat
    XmlDocToMd.exe -s "C:\Project\Docs" -d "Release" -o "C:\Project\MarkdownDocs"
```

#### With a secondary (backup) output directory


```bat
    XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\Markdown\output.md" -l "C:\Backup\Docs"
```

#### Soft tag handling — warn instead of throwing


```bat
    XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\output.md" -u Warn
```

#### GitHub-flavored URLs + README output


```bat
    XmlDocToMd.exe -i "C:\Docs\input.xml" -o "C:\Docs\README.md" -g -r
```

#### Visual Studio post-build event (XMLtoMD configuration)


```xml
    <Target Name="PostBuild" AfterTargets="PostBuildEvent" Condition="'$(ConfigurationName)' == 'XMLtoMD'">
      <Exec Command="call &quot;$(TargetDir)$(TargetName).exe&quot; -s &quot;$(ProjectDir)\&quot; -d &quot;$(ConfigurationName)&quot; -o &quot;$(ProjectDir)\&quot; -g -r" />
    </Target>
```



### Supported XML Documentation Tags




#### Standard C# XML Doc Tags


| Tag | Markdown output |
|---|---|
| `doc` | Top-level document root — emits an  `#`  heading with the assembly name. |
| `summary` | Rendered as a plain paragraph below the member heading. |
| `remarks` | Rendered as a paragraph block below the summary. |
| `returns` | Emits a **Returns:** line with the resolved return type. |
| `value` | Emits a **Value:** line for property descriptions. |
| `example` | Rendered under an  `##### Example`  sub-heading. |
| `param` | Collected into a two-column Markdown table (Name / Description). |
| `typeparam` | Collected into a two-column Generics table. |
| `exception` | Emits a **Throws:** linked entry per exception type. |
| `inheritdoc` | Emits *Inherited from `BaseType`*. The  `cref`  attribute controls the display name. |

#### Cross-Reference Tags


| Tag / attribute | Markdown output |
|---|---|
| `see cref=""` | Inline hyperlink to the referenced member. |
| `see langword=""` | Renders the keyword as inline code, e.g. `null`, `true`. |
| `see href=""` | Inline hyperlink using the raw URL (anchor variant). |
| `seealso` | Appended to a *See also* bullet list at the end of the member block. |
| `paramref` | Renders the parameter name as inline code. |
| `typeparamref` | Renders the type-parameter name as inline code. |

#### List Tags


> [!TIP]
>  Use  `type="table"`  for two-column reference tables,  `type="number"`  for step-by-step instructions, and  `type="bullet"`  (the default) for unordered lists. 


| Tag | Description |
|---|---|
| `list type="bullet"` | Unordered bullet list — each  `item`  becomes a  `-`  entry. |
| `list type="number"` | Ordered list — each  `item/description`  becomes a numbered entry. |
| `list type="table"` | Pipe table —  `listheader`  provides column headers; each  `item`  becomes a row. |
| `listheader` | Column header row for  `type="table"`  lists. |
| `item` | A single list entry; contains  `term`  and/or  `description` . |
| `term` | Bold label in a table row or bullet entry. |
| `description` | Description text within an  `item` . |

#### Inline Formatting Tags


| Tag | Markdown output |
|---|---|
| `c` | Inline code span:  ``text`` |
| `tt` | Teletype / monospace — same output as  `c` :  ``text`` |
| `b` | **Bold** text:  `**text**` |
| `i` | *Italic* text:  `*text*` |
| `u` | <u>Underline</u> via HTML pass-through:  `<u>text</u>` |
| `para` | Paragraph break inside a block element. |
| `code` | Fenced code block. Use the  `lang`  attribute for syntax highlighting, e.g.  `lang="csharp"` . |

#### HTML Pass-Through Tags


| Tag | Markdown output |
|---|---|
| `a href="…"` | Hyperlink:  `[text](url)` |
| `br` | Hard line break. |
| `h1`  –  `h4` | ATX headings at the corresponding level. |

#### GFM Alert Blocks *(DocFX / Sandcastle compatibility)*


> [!NOTE]
>  These tags map to GitHub-Flavored Markdown alert syntax ( `> [!NOTE]`  etc.) and render as coloured callout boxes on GitHub and MkDocs Material. 


| Tag | GFM alert type |
|---|---|
| `note` | `> [!NOTE]`  — informational callout. |
| `tip` | `> [!TIP]`  — helpful suggestion. |
| `important` | `> [!IMPORTANT]`  — key information. |
| `warning` | `> [!WARNING]`  — potential pitfall. |
| `caution` | `> [!CAUTION]`  — dangerous action ahead. |






---
## ROBdk97.XmlDocToMd.Cli.Options

 Command-line options for the XmlDocToMd converter. 


 Options are parsed by [CommandLineParser](https://github.com/commandlineparser/commandline) and can also be populated programmatically for testing or embedding purposes. 
| Flag | Purpose |
|---|---|
| `-i`  /  `--inputfile` | Single XML file to convert. |
| `-o`  /  `--outputfile` | Destination Markdown file. |
| `-s`  /  `-d` | Batch mode: search directory + subdirectory name. |
| `-u` | Unexpected-tag policy ([UnexpectedTagActionEnum](#robdk97xmldoctomdcliunexpectedtagactionenum)). |
| `-g` | Use GitHub-Flavored Markdown anchor links. |
| `-r` | Name the output file `README.md`. |
| `-f` | Path to a custom `settings.json`. |




#### Properties:
|Name | Type | Description |
|-----|------|------|
|InputFile|[String](#string)|Path to the input XML documentation file to convert. > [!NOTE] > Mutually exclusive with `--cin` . When both are supplied, the file path takes precedence and [ConsoleIn](#p:robdk97xmldoctomdclioptionsconsolein) is ignored.|
|ConsoleIn|[Boolean](#boolean)|When `true`, reads the XML document from `stdin` instead of a file. > [!TIP] > Pipe output directly from MSBuild or another tool: `bat type MyLib.xml \| XmlDocToMd.exe --cin -o docs\README.md `|
|OutputFile|[String](#string)|Path of the Markdown file to write. > [!NOTE] > In batch mode ( `-s` / `-d` ) this is treated as the *output directory* rather than a single file path.|
|SecondaryOutputDirectory|[String](#string)|Optional secondary output directory. The generated Markdown file is copied here after the primary write, for example to a network share or documentation repo.|
|ConsoleOut|[Boolean](#boolean)|When `true`, writes the Markdown output to `stdout` instead of a file.|
|UnexpectedTagAction|[UnexpectedTagActionEnum](#unexpectedtagactionenum)|Determines how XML tags that have no registered renderer are treated. Defaults to [Error](#f:robdk97xmldoctomdcliunexpectedtagactionenumerror). \| Value \| Behaviour \| \|---\|---\| \| [Error](#f:robdk97xmldoctomdcliunexpectedtagactionenumerror) \| Throws an exception and halts conversion. \| \| [Warn](#f:robdk97xmldoctomdcliunexpectedtagactionenumwarn) \| Emits a `WARN:` line to `stderr` and continues. \| \| [Accept](#f:robdk97xmldoctomdcliunexpectedtagactionenumaccept) \| Silently skips the unknown tag. \||
|SearchDirectory|[String](#string)|Root directory to search for XML documentation files when running in batch mode. Must be combined with [Directory](#p:robdk97xmldoctomdclioptionsdirectory) ( `-d` ). > [!TIP] > Point this at the project root; use `-d` to name the build-output subfolder (e.g. `Release` or `XMLtoMD`).|
|Directory|[String](#string)|Name of the subdirectory within [SearchDirectory](#p:robdk97xmldoctomdclioptionssearchdirectory) that contains the compiled `.xml` and `.dll` files. Defaults to `"Release"`.|
|Git|[Boolean](#boolean)|When `true`, formats anchor links using GitHub-Flavored Markdown conventions (lower-case, hyphens instead of spaces). > [!TIP] > Enable this when the output will be published to GitHub, GitLab, or any host that renders GFM — it ensures heading anchors resolve correctly.|
|Readme|[Boolean](#boolean)|When `true`, names the output file `README.md` instead of `{AssemblyName}.md`. > [!TIP] > Use together with `-g` when generating the landing-page documentation for a GitHub repository.|
|SettingsFile|[String](#string)|Path to the JSON settings file that controls file and namespace exclusions. Defaults to `"settings.json"` in the working directory. > [!NOTE] > If the file does not exist it is created automatically with empty default values. See [Settings](#robdk97xmldoctomdclisettings) for the available properties.|

---
## ROBdk97.XmlDocToMd.Cli.Settings

 Persistent configuration loaded from a JSON file (`settings.json` by default). 


 The settings file is created automatically with empty defaults when it does not yet exist. The path can be overridden with the  `-f`  /  `--settings`  CLI option. 


#### Properties:
|Name | Type | Description |
|-----|------|------|
|FilesToIgnore|[List‹String›](#System.Collections.Generic.List`1[System.String])|File names (without path) that should be skipped during directory-wide conversion. > [!TIP] > Add system-generated XML files such as `System.Runtime.xml` here to keep the output focused on your own assemblies.|
|NameSpacesToRemove|[List‹String›](#System.Collections.Generic.List`1[System.String])|Namespace fragments whose members are excluded from conversion. Any member whose fully-qualified name contains one of these strings is silently dropped before rendering begins. > [!TIP] > Use this to hide internal or generated namespaces such as `CompilerServices` or `Internal`.|

---
## ROBdk97.XmlDocToMd.Cli.UnexpectedTagActionEnum

 Controls how the converter reacts when it encounters an XML documentation tag that has no registered [ITagRenderStrategy](#robdk97xmldoctomdrenderingitagrenderstrategy). 


 Choose the policy that best fits the maturity of your XML documentation: 
> [!TIP]
>  Start with [Warn](#f:robdk97xmldoctomdcliunexpectedtagactionenumwarn) while authoring documentation, then switch to [Error](#f:robdk97xmldoctomdcliunexpectedtagactionenumerror) once all tags are accounted for. 




#### Fields:
|Name | Type | Description |
|-----|------|------|
|Error|[UnexpectedTagActionEnum](#unexpectedtagactionenum)|Throws an [KeyNotFoundException](#systemcollectionsgenerickeynotfoundexception) when an unknown tag is encountered, halting conversion immediately. > [!WARNING] > Using [Error](#f:robdk97xmldoctomdcliunexpectedtagactionenumerror) in a post-build event will cause the build to fail when any unsupported tag is present in the XML documentation.|
|Warn|[UnexpectedTagActionEnum](#unexpectedtagactionenum)|Emits a `WARN: ` diagnostic to `stderr` via the configured [IWarningLogger](#robdk97xmldoctomdloggingiwarninglogger) and continues conversion, producing partial output.|
|Accept|[UnexpectedTagActionEnum](#unexpectedtagactionenum)|Silently skips tags with no registered renderer. No diagnostic is emitted and no output is produced for the skipped element.|




---

Generated by [XmlDocToMd](https://github.com/ROBdk97/XmlDocToMd) by [ROBdk97](https://github.com/ROBdk97)