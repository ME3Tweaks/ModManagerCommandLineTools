using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFARTools_Inject
{
    class Options
    {
        [Option('r', "replacefiles", HelpText = "Replaces files in an SFAR archive. This used to be known as injection.")]
        public bool ReplaceFiles { get; set; }

        [Option('s', "sfarpath", Required = true, HelpText = "Path to SFAR archive to operate on.")]
        public string SFARPath { get; set; }

        [Option('a', "addfiles", HelpText = "Adds files to an SFAR archive. If the files already exist, they will be replaced.")]
        public bool AddFiles { get; set; }

        [Option('d', "deletefiles", HelpText = "Deletes files from an SFAR archive. If the file in the archive is not found, it is skipped.")]
        public bool DeleteFiles { get; set; }

        [OptionList('f', "files", Required = true, HelpText = "List of files and paths. For --addfiles and --replacefiles the files in this list should be in alternating fashion, starting with the path to the source file then the path in the SFAR archive. For --deletefiles this is a list of paths in the SFAR archive to delete.")]
        public List<String> Files { get; set; }

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
            Options options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                // validation
                if (options.ReplaceFiles == false && options.AddFiles == false && options.DeleteFiles == false)
                {
                    Console.WriteLine(options.GetUsage());
                    Console.WriteLine("This program requires one of the following: --replacefiles, --addfiles, or --deletefiles.");
                    EndProgram(1);
                }
                if (!File.Exists(options.SFARPath))
                {
                    Console.WriteLine("SFAR file does not exist: " + options.SFARPath);
                    EndProgram(1);
                }
            }


        }
        static void EndProgram(int code)
        {
#if DEBUG
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();

#endif
            Environment.Exit(code);
        }
    }

}
