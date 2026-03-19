using CommandLine;

namespace ROBdk97.XmlDocToMd.Cli;

/// <summary>
/// Command-line options for the XmlDocToMd converter.
/// </summary>
/// <remarks>
/// Options are parsed by <a href="https://github.com/commandlineparser/commandline">CommandLineParser</a>
/// and can also be populated programmatically for testing or embedding purposes.
/// <list type="table">
/// <listheader><term>Flag</term><description>Purpose</description></listheader>
/// <item><term><c>-i</c> / <c>--inputfile</c></term><description>Single XML file to convert.</description></item>
/// <item><term><c>-o</c> / <c>--outputfile</c></term><description>Destination Markdown file.</description></item>
/// <item><term><c>-s</c> / <c>-d</c></term><description>Batch mode: search directory + subdirectory name.</description></item>
/// <item><term><c>-u</c></term><description>Unexpected-tag policy (<see cref="UnexpectedTagActionEnum"/>).</description></item>
/// <item><term><c>-g</c></term><description>Use GitHub-Flavored Markdown anchor links.</description></item>
/// <item><term><c>-r</c></term><description>Name the output file <tt>README.md</tt>.</description></item>
/// <item><term><c>-f</c></term><description>Path to a custom <tt>settings.json</tt>.</description></item>
/// </list>
/// </remarks>
public class Options
{
    /// <summary>
    /// Path to the input XML documentation file to convert.
    /// </summary>
    /// <note>
    /// Mutually exclusive with <c>--cin</c>. When both are supplied, the file path takes
    /// precedence and <see cref="ConsoleIn"/> is ignored.
    /// </note>
    [Option('i', "inputfile", HelpText = "Input xml file to read.")]
    public string? InputFile { get; set; }

    /// <summary>
    /// When <see langword="true"/>, reads the XML document from <tt>stdin</tt> instead of
    /// a file.
    /// </summary>
    /// <tip>
    /// Pipe output directly from MSBuild or another tool:
    /// <code lang="bat">type MyLib.xml | XmlDocToMd.exe --cin -o docs\README.md</code>
    /// </tip>
    [Option("cin", HelpText = "Read input from console instead of file.")]
    public bool ConsoleIn { get; set; }

    /// <summary>
    /// Path of the Markdown file to write.
    /// </summary>
    /// <note>
    /// In batch mode (<c>-s</c>/<c>-d</c>) this is treated as the <i>output directory</i>
    /// rather than a single file path.
    /// </note>
    [Option('o', "outputfile", HelpText = "Output md file to write.")]
    public string? OutputFile { get; set; }

    /// <summary>
    /// Optional secondary output directory. The generated Markdown file is copied here
    /// after the primary write, for example to a network share or documentation repo.
    /// </summary>
    [Option('l', "secondaryDir", HelpText = "Secondary Output Directory")]
    public string? SecondaryOutputDirectory { get; set; }

    /// <summary>
    /// When <see langword="true"/>, writes the Markdown output to <tt>stdout</tt> instead
    /// of a file.
    /// </summary>
    [Option("cout", HelpText = "Write output to console instead of file.")]
    public bool ConsoleOut { get; set; }

    /// <summary>
    /// Determines how XML tags that have no registered renderer are treated.
    /// Defaults to <see cref="UnexpectedTagActionEnum.Error"/>.
    /// </summary>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Value</term><description>Behaviour</description></listheader>
    /// <item><term><see cref="UnexpectedTagActionEnum.Error"/></term><description>Throws an exception and halts conversion.</description></item>
    /// <item><term><see cref="UnexpectedTagActionEnum.Warn"/></term><description>Emits a <tt>WARN:</tt> line to <tt>stderr</tt> and continues.</description></item>
    /// <item><term><see cref="UnexpectedTagActionEnum.Accept"/></term><description>Silently skips the unknown tag.</description></item>
    /// </list>
    /// </remarks>
    [Option('u', "unexpected", HelpText = "Handled unexpected tags as Accept, Warn, or Error.")]
    public UnexpectedTagActionEnum UnexpectedTagAction { get; set; }

    /// <summary>
    /// Root directory to search for XML documentation files when running in batch mode.
    /// Must be combined with <see cref="Directory"/> (<c>-d</c>).
    /// </summary>
    /// <tip>
    /// Point this at the project root; use <c>-d</c> to name the build-output subfolder
    /// (e.g. <tt>Release</tt> or <tt>XMLtoMD</tt>).
    /// </tip>
    [Option('s', "search", HelpText = "Search this directory for .xml Files in Folder specified in argument \"d\" and set \"o\" to outputDir instead.")]
    public string? SearchDirectory { get; set; }

    /// <summary>
    /// Name of the subdirectory within <see cref="SearchDirectory"/> that contains the
    /// compiled <tt>.xml</tt> and <tt>.dll</tt> files. Defaults to <tt>"Release"</tt>.
    /// </summary>
    [Option('d', "directory", HelpText = "Directory Name to search for .xml files (like \"Release\") in combination with argument \"s\"")]
    public string Directory { get; set; } = "Release";

    /// <summary>
    /// When <see langword="true"/>, formats anchor links using GitHub-Flavored Markdown
    /// conventions (lower-case, hyphens instead of spaces).
    /// </summary>
    /// <tip>
    /// Enable this when the output will be published to GitHub, GitLab, or any host that
    /// renders GFM — it ensures heading anchors resolve correctly.
    /// </tip>
    [Option('g', "git", HelpText = "Use Git Markdown for URLs")]
    public bool Git { get; set; }

    /// <summary>
    /// When <see langword="true"/>, names the output file <tt>README.md</tt> instead of
    /// <tt>{AssemblyName}.md</tt>.
    /// </summary>
    /// <tip>
    /// Use together with <c>-g</c> when generating the landing-page documentation for a
    /// GitHub repository.
    /// </tip>
    [Option('r', "readme", HelpText = "Call the Markdown file \"README.md\" instead of AssemblyName.md")]
    public bool Readme { get; set; }

    /// <summary>
    /// Path to the JSON settings file that controls file and namespace exclusions.
    /// Defaults to <tt>"settings.json"</tt> in the working directory.
    /// </summary>
    /// <note>
    /// If the file does not exist it is created automatically with empty default values.
    /// See <see cref="Settings"/> for the available properties.
    /// </note>
    [Option('f', "settings", HelpText = "Settings file to use.")]
    public string SettingsFile { get; set; } = "settings.json";
}
