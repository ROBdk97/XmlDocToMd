# XmlDocToMd
Converts XML documentation files into GitHub-Flavored Markdown, producing output compatible with [MkDocs](https://www.mkdocs.org/) and GitHub Pages.

The tool reads the standard `.xml` file emitted by the C# compiler alongside the compiled assembly, enriches member signatures via Reflection, and writes a structured `.md` file.

See the [Options](#robdk97xmldoctomdclioptions) class for every available command-line parameter, and the *Supported XML Documentation Tags* section in [AssemblyDoc](AssemblyDoc.cs) remarks for the full list of handled tags.

Use FilesToIgnore to skip files during batch conversion. The wildcard `*` is supported, for example `System*`, `Microsoft*`.

The tool looks for an empty public class named [AssemblyDoc](AssemblyDoc.cs) (abstract is recommended) and places its XML documentation at the top of the generated Markdown file. This is used to create a general documentation overview with references to the most important parts and a high-level description, similar to the main section of a README.

> [!NOTE]
> Because the tool uses Reflection to resolve member types and visibility, the compiled `.dll` must reside in the same directory as the `.xml` file. Conversion still succeeds without the DLL, but type information in tables will be reduced to the raw prefix letter (`F`, `P`, …).

**Example**

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

|Tag|Markdown output|
|---|---|
|`doc`|Top-level document root — emits an `#` heading with the assembly name.|
|`summary`|Rendered as a plain paragraph below the member heading.|
|`remarks`|Rendered as a paragraph block below the summary.|
|`returns`|Emits a **Returns:** line with the resolved return type.|
|`value`|Emits a **Value:** line for property descriptions.|
|`example`|Rendered under an `##### Example` sub-heading.|
|`param`|Collected into a two-column Markdown table (Name / Description).|
|`typeparam`|Collected into a two-column Generics table.|
|`exception`|Emits a **Throws:** linked entry per exception type.|
|`inheritdoc`|Emits *Inherited from `BaseType`*. The `cref` attribute controls the display name.|

#### Cross-Reference Tags

|Tag / attribute|Markdown output|
|---|---|
|`see cref=""`|Inline hyperlink to the referenced member.|
|`see langword=""`|Renders the keyword as inline code, e.g. `null`, `true`.|
|`see href=""`|Currently not rendered as a link. Use `a href=""` for explicit URL links.|
|`seealso`|Appended to a *See also* bullet list at the end of the member block.|
|`paramref`|Renders the parameter name as inline code.|
|`typeparamref`|Renders the type-parameter name as inline code.|

#### List Tags

> [!TIP]
> Use `type="table"` for two-column reference tables, `type="number"` for step-by-step instructions, and `type="bullet"` (the default) for unordered lists.

|Tag|Description|
|---|---|
|`list type="bullet"`|Unordered bullet list — each `item` becomes a `-` entry.|
|`list type="number"`|Ordered list — each `item/description` becomes a numbered entry.|
|`list type="table"`|Pipe table — `listheader` provides column headers; each `item` becomes a row.|
|`listheader`|Column header row for `type="table"` lists.|
|`item`|A single list entry; contains `term` and/or `description`.|
|`term`|Bold label in a table row or bullet entry.|
|`description`|Description text within an `item`.|

#### Inline Formatting Tags

|Tag|Markdown output|
|---|---|
|`c`|Inline code span: ``text``|
|`tt`|Teletype / monospace — same output as `c`: ``text``|
|`b`|**Bold** text: `**text**`|
|`strong`|**Bold** text: `**text**`|
|`i`|*Italic* text: `*text*`|
|`u`|Currently rendered as bold text: `**text**`|
|`para`|Paragraph break inside a block element.|
|`code`|Fenced code block. Use the `lang` attribute for syntax highlighting, e.g. `lang="csharp"`.|

#### HTML Pass-Through Tags

|Tag|Markdown output|
|---|---|
|`a href="…"`|Hyperlink: `[text](url)`|
|`br`|Hard line break.|
|`h1` – `h4`|ATX headings at the corresponding level.|

#### GFM Alert Blocks *(DocFX / Sandcastle compatibility)*

> [!NOTE]
> These tags map to GitHub-Flavored Markdown alert syntax (`> [!NOTE]` etc.) and render as coloured callout boxes on GitHub and MkDocs Material.

|Tag|GFM alert type|
|---|---|
|`note`|`> [!NOTE]` — informational callout.|
|`tip`|`> [!TIP]` — helpful suggestion.|
|`important`|`> [!IMPORTANT]` — key information.|
|`warning`|`> [!WARNING]` — potential pitfall.|
|`caution`|`> [!CAUTION]` — dangerous action ahead.|

---
## ROBdk97.XmlDocToMd.Cli.Options

Command-line options for the XmlDocToMd converter.

Options are parsed by [CommandLineParser](https://github.com/commandlineparser/commandline) and can also be populated programmatically for testing or embedding purposes.

|Flag|Purpose|
|---|---|
|`-i` / `--inputfile`|Single XML file to convert.|
|`--cin`|Read XML input from standard input instead of a file.|
|`-o` / `--outputfile`|Destination Markdown file.|
|`--cout`|Write generated Markdown to standard output instead of a file.|
|`-l` / `--secondaryDir`|Optional secondary output directory for copying generated Markdown.|
|`-s` / `-d`|Batch mode: search directory + subdirectory name.|
|`-u`|Unexpected-tag policy ([UnexpectedTagActionEnum](#robdk97xmldoctomdcliunexpectedtagactionenum)).|
|`-g`|Use GitHub-Flavored Markdown anchor links.|
|`-r`|Name the output file `README.md`.|
|`-f`|Path to a custom `settings.json`.|

**Properties**

|Name|Type|Description|
|---|---|---|
|InputFile|String|Path to the input XML documentation file to convert. Mutually exclusive with `--cin`; when both are supplied, the file path takes precedence and ConsoleIn is ignored.|
|ConsoleIn|Boolean|When `true`, reads the XML document from `stdin` instead of a file. Example: `type MyLib.xml \| XmlDocToMd.exe --cin -o docs\README.md`.|
|OutputFile|String|Path of the Markdown file to write. In batch mode (`-s`/`-d`) this is treated as the output directory rather than a single file path.|
|SecondaryOutputDirectory|String|Optional secondary output directory. The generated Markdown file is copied here after the primary write, for example to a network share or documentation repo.|
|ConsoleOut|Boolean|When `true`, writes the Markdown output to `stdout` instead of a file.|
|UnexpectedTagAction|[UnexpectedTagActionEnum](#robdk97xmldoctomdcliunexpectedtagactionenum)|Determines how XML tags that have no registered renderer are treated. Defaults to Error. Values: Error (throws and halts), Warn (writes WARN and continues), Accept (silently skips).|
|SearchDirectory|String|Root directory to search for XML documentation files when running in batch mode. Must be combined with Directory (`-d`). Tip: point this at the project root and use `-d` for the build-output subfolder (for example `Release` or `XMLtoMD`).|
|Directory|String|Name of the subdirectory within SearchDirectory that contains the compiled `.xml` and `.dll` files. Defaults to `"Release"`.|
|Git|Boolean|When `true`, formats anchor links using GitHub-Flavored Markdown conventions (lower-case, hyphens instead of spaces). Tip: enable this when publishing to GitHub/GitLab so heading anchors resolve correctly.|
|Readme|Boolean|When `true`, names the output file `README.md` instead of `{AssemblyName}.md`. Use together with `-g` when generating the landing-page documentation for a GitHub repository.|
|SettingsFile|String|Path to the JSON settings file that controls file and namespace exclusions. Defaults to `"settings.json"` in the working directory. If the file does not exist it is created automatically with empty default values. See [Settings](#robdk97xmldoctomdclisettings) for available properties.|

---
## ROBdk97.XmlDocToMd.Cli.Settings

Persistent configuration loaded from a JSON file (`settings.json` by default).

The settings file is created automatically with empty defaults when it does not yet exist. The path can be overridden with the `-f` / `--settings` CLI option.

**Properties**

|Name|Type|Description|
|---|---|---|
|FilesToIgnore|List‹String›|File names (without path) that should be skipped during directory-wide conversion. Supports the wildcard `*`, for example `System*`, `Microsoft*`. Add system-generated XML files such as `System.Runtime.xml` to keep the output focused on your own assemblies.|
|NameSpacesToRemove|List‹String›|Namespace fragments whose members are excluded from conversion. Any member whose fully-qualified name contains one of these strings is silently dropped before rendering begins. > [!TIP] > Use this to hide internal or generated namespaces such as `CompilerServices` or `Internal`.|

---
## ROBdk97.XmlDocToMd.Cli.UnexpectedTagActionEnum

Controls how the converter reacts to recoverable conversion problems, such as XML documentation tags without a registered [ITagRenderStrategy](#robdk97xmldoctomdrenderingitagrenderstrategy) or other non-fatal rendering issues encountered while walking the XML tree.

Choose the policy that best fits the maturity of your XML documentation:
> [!TIP]
> Start with Warn while authoring documentation, then switch to Error once all conversion issues are accounted for.

**Fields**

|Name|Type|Description|
|---|---|---|
|Error|[UnexpectedTagActionEnum](#robdk97xmldoctomdcliunexpectedtagactionenum)|Throws when a recoverable conversion problem is encountered, halting conversion immediately.|
|Warn|[UnexpectedTagActionEnum](#robdk97xmldoctomdcliunexpectedtagactionenum)|Emits a `WARN: ` diagnostic to `stderr` via the configured [IWarningLogger](#robdk97xmldoctomdloggingiwarninglogger) and continues conversion, producing partial output.|
|Accept|[UnexpectedTagActionEnum](#robdk97xmldoctomdcliunexpectedtagactionenum)|Silently ignores recoverable conversion problems. No diagnostic is emitted and no output is produced for the skipped element.|

---

Generated by [XmlDocToMd](https://github.com/ROBdk97/XmlDocToMd) by [ROBdk97](https://github.com/ROBdk97)
