using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFARTools
{
    class Options
    {
        [OptionArray('x', "ExtractFilenames", HelpText = "List of paths in the archive to extract")]
        public string[] ExtractList { get; set; }

        [Option('s', "SFARPath", HelpText = "Path to SFAR archive to operate on.")]
        public string SFARPath { get; set; }

        [Option('d', "OutputPath", HelpText = "Directory to extract files to. Will extract to the same filename path unless the --FlatFolderExtraction parameter is specified.")]
        public string OutputPath { get; set; }

        [Option('f', "FlatFolderExtraction", DefaultValue = false, HelpText = "Specifies that files being extracted will be placed all into the --OutputPath folder directly, rather than with the original SFAR file path.")]
        public bool FlatFolder { get; set; }

        [Option('a', "ExtractEntireArchive", DefaultValue = false, HelpText = "Extracts the entire spcified archive (--sfarpath). Requires --OutputPath.")]
        public bool ExtractEntireArchive { get; set; }

        [Option('k', "KeepaAchiveIntact", DefaultValue = false, HelpText = "Leaves the original SFAR alone, performing read-only operations. Can be used with --GamePath and --ExtractEntireArchive.")]
        public bool KeepArchiveIntact { get; set; }

        [Option('g', "GamePath", HelpText = "Extracts all archives available from the game directory specified.")]
        public string GamePath { get; set; }

        [OptionList('i', "IgnoreMissingPaths", DefaultValue = false, HelpText = "Supresses errors about missing files in the archive. Only works with --extractfilenames.")]
        public bool IgnoreMissingPaths { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                if (options.SFARPath == null && options.GamePath == null)
                {
                    Console.WriteLine("This program requires --gamepath or --sfarpath, or both depending on the chosen options.");
                    EndProgram(1);
                }
                if (options.SFARPath != null)
                {
                    //We're extracting a single SFAR
                    //Validation...
                    if (options.ExtractEntireArchive && options.ExtractList != null && options.ExtractList.Length > 0)
                    {
                        Console.WriteLine("Ambiguous input: --extractfilenames and --gamepath were both specified. You can only use one.");
                        EndProgram(1);
                    }
                    if (!options.ExtractEntireArchive && options.ExtractList == null || options.ExtractList.Length == 0)
                    {
                        Console.WriteLine("No extraction operation was specified. Use --ExtractEntireArchive or a list following --ExtractFilenames.");
                        EndProgram(1);
                    }
                    if (options.ExtractEntireArchive && options.ExtractList.Length > 0)
                    {
                        Console.WriteLine("Ambiguous input: --ExtractFilenames and --gamepath were both specified. You can only use one.");
                        EndProgram(1);
                    }
                    if (!File.Exists(options.SFARPath))
                    {
                        Console.WriteLine("Specified SFAR file doesn't exist: " + options.SFARPath);
                        EndProgram(1);
                    }
                    SFAR sfar = new SFAR(options.SFARPath);

                    if (options.ExtractList.Length > 0)
                    {
                        //Extract a list of files
                        sfar.extractfiles(options.OutputPath, options.ExtractList, true);
                        sfar.Dispose();

                    }
                    else
                    {
                        //extract the whole archive
                        Console.WriteLine("Extracting archive...");
                        sfar.extract(options.OutputPath);
                    }

                }

                // Values are available here
                // if (options.Verbose) Console.WriteLine("Filename: {0}", options.InputFile);
            }
            EndProgram(0);
        }

        /// <summary>
        /// Ends the program with the specified code. If running in debug mode, the program will wait for user input.
        /// </summary>
        /// <param name="code">Exit code</param>
        private static void EndProgram(int code)
        {
#if DEBUG
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
#endif
            Environment.Exit(code);
        }
    }
}
