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
        [OptionArray('r', "replacefiles", HelpText = "Replaces files in an SFAR archive. This used to be known as injection.")]
        public bool ReplaceFiles { get; set; }

        [Option('s', "sfarpath", Required = true, HelpText = "Path to SFAR archive to operate on.")]
        public string SFARPath { get; set; }

        [OptionArray('a', "addfiles", HelpText = "Adds files to an SFAR archive. If the files already exist, they will be replaced.")]
        public bool AddFiles { get; set; }

        [Option('d', "deletefiles", HelpText = "Deletes files from an SFAR archive. If the file in the archive is not found, it is skipped.")]
        public bool DeleteFiles { get; set; }

        [Option('i', "ignoredeletionerrors", DefaultValue = false, HelpText = "Only usable with --deletfiles. Will ignore missing files in the archive, in the event they've already been deleted before.")]
        public bool IgnoreDeletionErrors { get; set; }

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

                if (options.ReplaceFiles || options.AddFiles)
                {
                    if (options.Files.Count / 2 != 0)
                    {
                        //requires even number of args
                    }
                    int numfiles = options.Files.Count / 2;
                    string[] sfarFiles = new string[options.Files.Count / 2];
                    string[] diskFiles = new string[options.Files.Count / 2];

                    int getindex = 0;
                    for (int i = 0; i < options.Files.Count; i++, getindex++)
                    {
                        sfarFiles[getindex] = options.Files[i];
                        i++;
                        diskFiles[getindex] = options.Files[i];
                    }

                    //Check all newfiles exist
                    foreach (string str in diskFiles)
                    {
                        if (!File.Exists(str))
                        {
                            Console.WriteLine("Source file on disk doesn't exist: " + str);
                            EndProgram(1);
                        }
                    }

                    DLCPackage dlc = new DLCPackage(options.SFARPath);
                    //precheck
                    bool allfilesfound = true;
                    if (options.ReplaceFiles)
                    {
                        for (int i = 0; i < numfiles; i++)
                        {
                            int idx = dlc.FindFileEntry(sfarFiles[i]);
                            if (idx == -1)
                            {
                                Console.WriteLine("Specified file does not exist in the SFAR Archive: " + sfarFiles[i]);
                                allfilesfound = false;
                            }
                        }
                    }

                    //Add or Replace
                    if (allfilesfound)
                    {
                        for (int i = 0; i < numfiles; i++)
                        {
                            int index = dlc.FindFileEntry(sfarFiles[i]);
                            if ((index >= 0 && options.AddFiles) || options.ReplaceFiles)
                            {
                                dlc.ReplaceEntry(diskFiles[i], index);
                            } else
                            {
                                dlc.AddFileQuick(diskFiles[i], sfarFiles[i]);
                            }
                        }
                        EndProgram(0);
                    }
                    else
                    {
                        EndProgram(1);
                    }
                }

                if (options.DeleteFiles)
                {
                    DLCPackage dlc = new DLCPackage(options.SFARPath);
                    List<int> indexesToDelete = new List<int>();
                    foreach (string file in options.Files)
                    {
                        int idx = dlc.FindFileEntry(file);
                        if (idx == -1)
                        {
                            if (options.IgnoreDeletionErrors)
                            {
                                continue;
                            } else
                            {
                                Console.WriteLine("File doesn't exist in archive: " + file);
                                EndProgram(1);
                            }
                        } else
                        {
                            indexesToDelete.Add(idx);
                        }
                    }
                    if (indexesToDelete.Count > 1)
                    {
                        dlc.DeleteEntries(indexesToDelete);
                    } else
                    {
                        Console.WriteLine("No files were found in the archive that matched the input list for --files.");
                    }
                    EndProgram(0);
                }
            }
        }
        /// <summary>
        /// Ends the program with the specified code. If running in debug mode, the program will wait for user input.
        /// </summary>
        /// <param name="code">Exit code</param>
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
