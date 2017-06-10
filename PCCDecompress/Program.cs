using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCCDecompress
{
    class Options
    {
        [Option('i', "inputfile",
          HelpText = "Input pcc file to decompress. Will overwrite the original file unless --outputfile is used.")]
        public string InputFile { get; set; }

        [Option('m', "inputfolder",
          HelpText = "Input folder that contains pccs to decompress.  Will overwrite the original files unless --outputfolder is used.")]
        public string InputFolder { get; set; }

        [Option('o', "outputfile",
         HelpText = "Location to put output pcc file. Must include the filename. Can only be used with --inputfile.")]
        public string OutputFile { get; set; }

        [Option('f', "outputfolder",
                 HelpText = "Output folder. Can only be used with --inputfolder.")]
        public string OutputFolder { get; set; }

        // omitting long name, default --verbose
        [Option('c', "compress", DefaultValue = false,
          HelpText = "Compress instead of decompress.")]
        public bool Compress { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

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
            //Check if straight up file is dropped on it
            if (args.Length == 1)
            {
                if (File.Exists(args[0]))
                {
                    try
                    {
                        Console.WriteLine("Decompressing " + args[0] + "...");
                        byte[] decompressedData = PCCHandler.Decompress(args[0]);
                        if (decompressedData != null)
                        {
                            File.WriteAllBytes(args[0], decompressedData);
                            Console.WriteLine("OK");
                        }
                        EndProgram(0);
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("Specified file is not an ME3 PC PCC file: " + args[0]);
                        EndProgram(1);
                    }
                }
            }
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                if (options.InputFile == null && options.InputFolder == null)
                {
                    Console.WriteLine("This program requires --inputfile or --inputfolder in order to be used.");
                    EndProgram(1);
                }
                if (options.InputFile != null && options.InputFolder != null)
                {
                    Console.WriteLine("Ambiguous operation specified: This program requires only --inputfile or --inputfolder to be specified, but not both.");
                    EndProgram(1);
                }

                if (options.InputFile != null && !File.Exists(options.InputFile))
                {
                    Console.WriteLine("Input file does not exist: " + options.InputFile);
                    EndProgram(1);
                }
                if (options.InputFolder != null && !Directory.Exists(options.InputFolder))
                {
                    Console.WriteLine("Input folder does not exist: " + options.InputFolder);
                    EndProgram(1);
                }

                if (options.OutputFolder != null && !options.OutputFolder.EndsWith("\\"))
                {
                    options.OutputFolder += "\\";
                }

                List<string> pccFiles = new List<string>();
                string baseoutputpath = null;
                if (options.InputFile != null)
                {
                    pccFiles.Add(options.InputFile);
                    baseoutputpath = Directory.GetParent(options.OutputFile ?? options.InputFile) + "\\";
                }
                else
                {
                    pccFiles.AddRange(Directory.GetFiles(options.InputFolder, "*.pcc"));
                    baseoutputpath = options.OutputFolder;
                    if (options.OutputFile == null)
                    {
                        baseoutputpath = options.InputFolder + "\\";
                    }
                }

                if (pccFiles.Count == 0)
                {
                    Console.WriteLine("No pcc's found in the specified folder.");
                }

                string prefix = (options.Compress) ? "Compressed" : "Decompressed";

                Parallel.ForEach(pccFiles, f =>
                {
                    //Console.WriteLine("Decompressing " + f + "...");
                    byte[] decompressedData = (options.Compress) ? PCCHandler.Compress(f) : PCCHandler.Decompress(f);
                    if (decompressedData != null)
                    {
                        string fname = Path.GetFileName(f);
                        string outpath = baseoutputpath + fname;
                        //Console.WriteLine("Writing to " + outpath);
                        File.WriteAllBytes(outpath, decompressedData);
                        Console.WriteLine(prefix+" " + f);
                    }
                }
                );
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
