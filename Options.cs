using CommandLine;

namespace ROBdk97.XmlDocToMd
{
    /// <summary>
    /// Options for customizing the behavior of the XML documentation to Markdown conversion. These options allow for detailed control over input and output paths, handling of console input/output, secondary output directories, unexpected tag management, and more.
    /// </summary>
    public class Options
    {
        /// <summary>
        /// Gets or sets the input XML file path.
        /// Use -i or --inputfile followed by the file path to specify the input XML file.
        /// </summary>
        [Option('i', "inputfile", HelpText = "Input xml file to read.")]
        public string InputFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to read input from the console.
        /// Use --cin to read input from the console instead of a file.
        /// </summary>
        [Option("cin", HelpText = "Read input from console instead of file.")]
        public bool ConsoleIn { get; set; }

        /// <summary>
        /// Gets or sets the output markdown file path.
        /// Use -o or --outputfile followed by the file path to specify the output markdown file.
        /// </summary>
        [Option('o', "outputfile", HelpText = "Output md file to write.")]
        public string OutputFile { get; set; }

        /// <summary>
        /// Gets or sets the path to a secondary output directory. To copy to a network share for example.
        /// Use -l or --secondaryDir followed by the directory path to specify a secondary output directory.
        /// </summary>
        [Option('l', "secondaryDir", HelpText = "Secondary Output Directory")]
        public string SecondaryOutputDirectory { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to write output to the console.
        /// Use --cout to write output to the console instead of a file.
        /// </summary>
        [Option("cout", HelpText = "Write output to console instead of file.")]
        public bool ConsoleOut { get; set; }

        /// <summary>
        /// Gets or sets the action to take on unexpected tags.
        /// Use -u or --unexpected followed by Accept, Warn, or Error to specify how to handle unexpected tags.
        /// </summary>
        [Option('u', "unexpected", HelpText = "Handled unexpected tags as Accept, Warn, or Error.")]
        public UnexpectedTagActionEnum UnexpectedTagAction { get; set; }

        /// <summary>
        /// Gets or sets the search directory for XML files.
        /// Use -s or --search followed by the directory path to specify where to search for .xml files. This is used in combination with the -d or --directory argument.
        /// </summary>
        [Option('s', "search", HelpText = "Search this directory for .xml Files in Folder specified in argument \"d\" and set \"o\" to outputDir instead.")]
        public string SearchDirectory { get; set; }

        /// <summary>
        /// Gets or sets the directory name to search for .xml files.
        /// Use -d or --directory followed by the directory name. This is used in combination with the -s or --search argument. Defaults to "Release".
        /// </summary>
        [Option('d', "directory", HelpText = "Directory Name to search for .xml files (like \"Release\") in combination with argument \"s\"")]
        public string Directory { get; set; } = "Release";
        /// <summary>
        /// Gets or sets a value indicating whether to use Git Markdown format for URLs.
        /// Use -g or --git to use Git Markdown format.
        /// </summary>
        [Option('g', "git", HelpText = "Use Git Markdown for URLs")]
        public bool Git { get; set; }
        /// <summary>
        /// Call the Markdown file "README.md" instead of AssemblyName.md
        /// Use -r or --readme to call the Markdown file "README.md" instead of AssemblyName.md
        /// </summary>
        [Option('r', "readme", HelpText = "Call the Markdown file \"README.md\" instead of AssemblyName.md")]
        public bool Readme { get; set; }
        /// <summary>
        /// Gets or sets the settings file. Defaults to "settings.json".
        /// Use -f or --settings followed by the file path to specify the settings file to use.
        /// </summary>
        [Option('f', "settings", HelpText = "Settings file to use.")]
        public string SettingsFile { get; set; } = "settings.json";
    }

}
