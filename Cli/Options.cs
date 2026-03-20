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
/// <item><term><c>--cin</c></term><description>Read XML input from standard input instead of a file.</description></item>
/// <item><term><c>-o</c> / <c>--outputfile</c></term><description>Destination Markdown file.</description></item>
/// <item><term><c>--cout</c></term><description>Write generated Markdown to standard output instead of a file.</description></item>
/// <item><term><c>-l</c> / <c>--secondaryDir</c></term><description>Optional secondary output directory for copying generated Markdown.</description></item>
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
    /// Mutually exclusive with <c>--cin</c>; when both are supplied, the file path takes
    /// precedence and <see cref="ConsoleIn"/> is ignored.
    /// </summary>
    [Option('i', "inputfile", HelpText = "Input xml file to read.")]
    public string? InputFile { get; set; }

    /// <summary>
    /// When <see langword="true"/>, reads the XML document from <tt>stdin</tt> instead of
    /// a file. Example: <c>type MyLib.xml | XmlDocToMd.exe --cin -o docs\README.md</c>.
    /// </summary>
    [Option("cin", HelpText = "Read input from console instead of file.")]
    public bool ConsoleIn { get; set; }

    /// <summary>
    /// Path of the Markdown file to write.
    /// In batch mode (<c>-s</c>/<c>-d</c>) this is treated as the output directory rather
    /// than a single file path.
    /// </summary>
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
    /// Values: <see cref="UnexpectedTagActionEnum.Error"/> (throws and halts),
    /// <see cref="UnexpectedTagActionEnum.Warn"/> (writes WARN and continues),
    /// <see cref="UnexpectedTagActionEnum.Accept"/> (silently skips).
    /// </summary>
    [Option('u', "unexpected", HelpText = "Handled unexpected tags as Accept, Warn, or Error.")]
    public UnexpectedTagActionEnum UnexpectedTagAction { get; set; }

    /// <summary>
    /// Root directory to search for XML documentation files when running in batch mode.
    /// Must be combined with <see cref="Directory"/> (<c>-d</c>).
    /// Tip: point this at the project root and use <c>-d</c> for the build-output
    /// subfolder (for example <tt>Release</tt> or <tt>XMLtoMD</tt>).
    /// </summary>
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
    /// Tip: enable this when publishing to GitHub/GitLab so heading anchors resolve
    /// correctly.
    /// </summary>
    [Option('g', "git", HelpText = "Use Git Markdown for URLs")]
    public bool Git { get; set; }

    /// <summary>
    /// When <see langword="true"/>, names the output file <tt>README.md</tt> instead of
    /// <tt>{AssemblyName}.md</tt>. Use together with <c>-g</c> when generating the
    /// landing-page documentation for a GitHub repository.
    /// </summary>
    [Option('r', "readme", HelpText = "Call the Markdown file \"README.md\" instead of AssemblyName.md")]
    public bool Readme { get; set; }

    /// <summary>
    /// Path to the JSON settings file that controls file and namespace exclusions.
    /// Defaults to <tt>"settings.json"</tt> in the working directory.
    /// If the file does not exist it is created automatically with empty default values.
    /// See <see cref="Settings"/> for available properties.
    /// </summary>
    [Option('f', "settings", HelpText = "Settings file to use.")]
    public string SettingsFile { get; set; } = "settings.json";
}
