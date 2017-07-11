using System;
using CommandLine;
using CommandLine.Text;
using System.IO;
using System.Diagnostics;
using MassEffectModder;
using TransplanterLib;
using System.Reflection;
using static TransplanterLib.TransplanterLib;

/// <summary>
/// Transplanter CLI - Main Class
/// </summary>
namespace TransplanterLib
{
    class Options
    {

        //Inputs
        [Option('i', "inputfile", MutuallyExclusiveSet = "input", HelpText = "Input file to be processed.")]
        public string InputFile { get; set; }

        [Option('f', "inputfolder", MutuallyExclusiveSet = "input", HelpText = "Input folder to be processed.")]
        public string InputFolder { get; set; }

        //Outputs
        [Option('o', "outputfolder", HelpText = "Output folder, used by some other command line switches.")]
        public string OutputFolder { get; set; }

        [Option('t', "targetfile",
            HelpText = "File to be operated on.")]
        public string TargetFile { get; set; }

        [Option("compress",
            HelpText = "Forces output pcc files to be compressed.")]
        public bool Compress { get; set; }

        //Operations
        [Option('p', "transplant", MutuallyExclusiveSet = "operation",
                HelpText = "Indicates that a transplant operation is going to take place. Requires --targetfile.")]
        public bool Transplant { get; set; }

        [Option('g', "gui-extract", MutuallyExclusiveSet = "operation",
            HelpText = "Extracts all GFX files from the input (--inputfile or --inputfolder) into a folder of the same name as the pcc file. With --outputfolder you can redirect the output.")]
        public bool GuiExtract { get; set; }

        [Option("swfs",
            HelpText = "Option for use with --extract to additionally extract SWF/GFX files. Will place into a subfolder of the PCC's name in the output directory.")]
        public bool SWFs { get; set; }

        [Option('u', "exec-dump", MutuallyExclusiveSet = "operation",
          HelpText = "Dumps all exec functions from the specified file or folder into a file named ExecFunctions.txt (in the same folder as the file or in the specified folder). To redirect the placement of the ExecFunctions.txt file, use the --outputfolder.")]
        public bool ExecDump { get; set; }

        [Option('x', "extract", DefaultValue = false, MutuallyExclusiveSet = "operation", HelpText = "Specifies the extract operation. Requires --inputfolder and at least one of the following: --scripts, --data, --names, --imports , --exports, --coalesced. Use of --outputfolder will redirect where parsed files are placed.")]
        public bool Extract { get; set; }

        [Option('y', "injectswf", DefaultValue = false, MutuallyExclusiveSet = "operation", HelpText = "Injects an SWF (--inputfile) or a folder of SWF files (--inputfolder) into a PCC (--targetfile). The SWF files must be named in PackageName.ObjectName.swf format.")]
        public bool Inject { get; set; }

        [Option('r', "verifypccintegrity", DefaultValue = false, MutuallyExclusiveSet = "operation", HelpText = "Loads a PCC to verify it is valid (or at least loadable). Returns 1 if not, 0 if OK.")]
        public bool VerifyPCC { get; set; }

        [Option('u', "guiscan", DefaultValue = false, MutuallyExclusiveSet = "operation", HelpText = "Loads a PCC to check if it contains any GUIS. Returns 0 if none are found, 1 if any are.")]
        public bool GUIScan { get; set; }

        [Option('b', "dumpmixinsql", DefaultValue = false, MutuallyExclusiveSet = "operation", HelpText = "Dumps Dynamic MixIn SQL statements for a PCCs properties")]
        public bool DMSQL { get; set; }

        [Option('w', "dumpweaponsql", DefaultValue = false, MutuallyExclusiveSet = "operation", HelpText = "Dumps Dynamic MixIn Balance Changed Weapon SQL statements")]
        public bool DWSQL { get; set; }


        [Option('h', "dumppathfindingfile", DefaultValue = false, MutuallyExclusiveSet = "operation", HelpText = "Dumps pathfinding nodes to file readable by MeshViewer")]
        public bool PathFindingDump { get; set; }

        //Extract Options
        [Option('n', "names", DefaultValue = false, HelpText = "Dumps the name table for the PCC.")]
        public bool Names { get; set; }

        [Option('m', "imports", DefaultValue = false, HelpText = "Dumps the list of imports for the PCC.")]
        public bool Imports { get; set; }

        [Option('s', "scripts", DefaultValue = false, HelpText = "Dumps function exports, as part of the --extract switch.")]
        public bool Scripts { get; set; }

        [Option('d', "data", DefaultValue = false, HelpText = "Dumps export binary data. This will cause a significant increase in filesize and will cause some text editors to have problems opening them. It is useful only for file comparison purposes. This will automatically enable the --exports switch.")]
        public bool Data { get; set; }

        [Option('e', "exports", DefaultValue = false, HelpText = "Dumps all exports metadata, such as superclass, export type, superclass, and data offset.")]
        public bool Exports { get; set; }

        [Option('c', "coalesced", DefaultValue = false, HelpText = "Expands all PCC data while scanning and will dump entires with the Coalesced bit set to true. This will significantly slow down dumping. Entries will start with [C].")]
        public bool Coalesced { get; set; }

        [Option('l', "no-line-separators", DefaultValue = false, HelpText = "Does not put line separators between exports.")]
        public bool LineSeparator { get; set; }

        [Option('p', "properties", DefaultValue = false, HelpText = "Includes the properties of each export in the data dump. This can potentially lead to large increases in filesize.")]
        public bool Properties { get; set; }

        //inject options
        [Option('z', "target-export", HelpText = "Specifies the target export to search for to inject the SWF file in with the --injectswf switch. Only works with the --inputfile switch, not --inputfolder.")]
        public string TargetExport { get; set; }

        //Options
        [Option('v', "verbose", DefaultValue = false,
          HelpText = "Prints debugging information to the console")]
        public bool Verbose { get; set; }

        [Option('a', "gamedir",
            HelpText = "Specify a specific game directory. Overrides automatic registry key lookups.")]
        public string GameDir { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }


    }

    class TransplanterCLI
    {
        private static readonly int CODE_NO_INPUT = 9;
        private static readonly int CODE_NO_OPERATION = 10;
        private static readonly int CODE_INPUT_FILE_NOT_FOUND = 11;
        private static readonly int CODE_INPUT_FOLDER_NOT_FOUND = 12;
        //private static readonly int CODE_NO_TRANSPLANT_FILE = 13;
        private static readonly int CODE_NO_DATA_TO_DUMP = 14;
        private static readonly int CODE_SAME_IN_OUT_FILE = 15;


        static void Main(string[] args)
        {
            var options = new Options();
            CommandLine.Parser parser = new CommandLine.Parser(s =>
             {
                 s.MutuallyExclusive = true;
                 s.CaseSensitive = true;
                 s.HelpWriter = Console.Out;
             });

            if (parser.ParseArguments(args, options))
            {
                //Package p = new Package(options.TargetFile);
                //endProgram(0);
                // Values are available here
                if (options.Verbose)
                {
                    Verbose = true;
                    writeVerboseLine("Verbose logging is enabled");
                }
                if (options.Compress && !Environment.Is64BitProcess)
                {
                    Console.WriteLine("Not 64-bit process - Disabling compression due to bugs in zlib.");
                    options.Compress = false;
                }

                if (options.GameDir != null)
                {
                    if (Directory.Exists(options.GameDir))
                    {
                        GamePath = options.GameDir;
                        if (!GamePath.EndsWith("\\"))
                        {
                            GamePath += "\\";
                        }
                        Console.WriteLine("Game path set to " + GamePath);
                    }
                    else
                    {
                        Console.Error.WriteLine("Specified game dir doesn't exist: " + options.GameDir);
                        endProgram(CODE_INPUT_FOLDER_NOT_FOUND);
                    }
                }

                if (options.InputFile == null && options.InputFolder == null)
                {
                    Console.Error.WriteLine("--inputfile or --inputfolder argument is required for all operations.");
                    Console.Error.WriteLine(options.GetUsage());
                    endProgram(CODE_NO_INPUT);
                }

                if (options.InputFile != null && !File.Exists(options.InputFile))
                {
                    Console.Error.WriteLine("Input file does not exist: " + options.InputFile);
                    endProgram(CODE_INPUT_FILE_NOT_FOUND);
                }
                if (options.InputFolder != null && !options.InputFolder.EndsWith(@"\"))
                {
                    options.InputFolder = options.InputFolder + @"\";
                }

                if (options.InputFolder != null && !Directory.Exists(options.InputFolder))
                {
                    Console.Error.WriteLine("Input folder does not exist: " + options.InputFolder);
                    endProgram(CODE_INPUT_FOLDER_NOT_FOUND);
                }

                if (options.OutputFolder != null)
                {
                    if (!options.OutputFolder.EndsWith("\\"))
                    {
                        options.OutputFolder += "\\";
                    }
                    Console.WriteLine("Redirecting output to " + options.OutputFolder);
                }

                //Operation Switch
                if (options.VerifyPCC)
                {
                    if (options.InputFile != null)
                    {
                        Console.WriteLine("Checking PCC can load " + options.TargetFile);
                        int result = VerifyPCC(options.InputFile);
                    }
                    else
                    {
                        Console.Error.WriteLine("Can only verify single pcc integrity, --inputfile is the only allowed input for this operation.");
                        endProgram(CODE_INPUT_FILE_NOT_FOUND);
                    }
                }
                else if (options.PathFindingDump)
                {
                    if (options.InputFile != null)
                    {
                        Console.WriteLine("Dumping Pathfinding on file " + options.InputFile);
                        bool[] dumpargs = new bool[] { false, false, false, false, false, false, false, false, true, false }; //meshmap only
                        dumpPCCFile(options.InputFile, dumpargs, options.OutputFolder);
                    }
                    endProgram(0);

                }
                else if (options.GUIScan)
                {
                    if (options.InputFile != null)
                    {
                        Console.WriteLine("Scanning for whitelisted GFxMovieInfo exports on " + options.InputFile);
                        endProgram(doesPCCContainGUIs(options.InputFile, false) ? 1 : 0);
                    }
                    else
                    {
                        Console.Error.WriteLine("Can only scan 1 pcc at a time, --inputfile is the only allowed input for this operation.");
                        endProgram(2);
                    }
                }
                else if (options.DMSQL)
                {
                    if (options.InputFile != null)
                    {
                        Console.WriteLine("Dumping SQL from " + options.InputFile);
                        dumpDynamicMixInsFromPCC(options.InputFile);
                        Console.WriteLine("done");
                        endProgram(0);
                    }
                    else
                    {
                        Console.Error.WriteLine("Can only scan 1 pcc at a time, --inputfile is the only allowed input for this operation.");
                        endProgram(2);
                    }
                }
                else if (options.DWSQL)
                {
                    if (options.InputFolder != null)
                    {
                        Console.WriteLine("Dumping Weapon SQL from " + options.InputFolder);
                        dumpModMakerWeaponDynamicSQL(options.InputFolder);
                        Console.WriteLine("done");
                        endProgram(0);
                    }
                    else
                    {
                        Console.Error.WriteLine("Can only scan 1 pcc at a time, --inputfile is the only allowed input for this operation.");
                        endProgram(2);
                    }
                }
                else if (options.Inject)
                {
                    if (options.TargetFile == null)
                    {
                        Console.Error.WriteLine("--targetfile is required for this operation.");
                        endProgram(CODE_INPUT_FILE_NOT_FOUND);
                    }

                    if (!File.Exists(options.TargetFile))
                    {
                        Console.Error.WriteLine("Target file does not exist: " + options.TargetFile);
                        endProgram(CODE_INPUT_FILE_NOT_FOUND);
                    }

                    if (options.Compress)
                    {
                        Console.WriteLine("Compression option is enabled.");
                    }

                    if (options.InputFile != null)
                    {
                        Console.WriteLine("Injecting SWF into " + options.TargetFile);
                        endProgram(replaceSingleSWF(options.InputFile, options.TargetFile, options.TargetExport));
                    }
                    else if (options.InputFolder != null)
                    {
                        Console.WriteLine("Injecting SWFs into " + options.TargetFile);
                        endProgram(replaceSWFs_MEM(options.InputFolder, options.TargetFile, options.Compress));
                    }
                }
                else if (options.GuiExtract)
                {
                    if (options.InputFile != null)
                    {
                        writeVerboseLine("Extracting GFX files from " + options.InputFile);
                        extractAllGFxMovies(options.InputFile, options.OutputFolder);
                    }
                    else if (options.InputFolder != null)
                    {
                        writeVerboseLine("Extracting GFX files from " + options.InputFolder);
                        extractAllGFxMoviesFromFolder(options.InputFolder, options.OutputFolder);
                    }
                }
                else if (options.ExecDump)
                {
                    if (options.InputFile != null)
                    {
                        writeVerboseLine("Dumping all Exec functions from " + options.InputFile);
                        dumpAllExecFromFile(options.InputFile, options.OutputFolder);
                    }
                    if (options.InputFolder != null)
                    {
                        writeVerboseLine("Dumping all Exec functions from " + options.InputFolder);
                        dumpAllExecFromFolder(options.InputFolder, options.OutputFolder);
                    }
                }
                else if (options.Extract)
                {
                    if (options.Imports || options.Exports || options.Data || options.Scripts || options.Coalesced || options.Names || options.SWFs)
                    {
                        if (options.Data)
                        {
                            options.Exports = true;
                        }
                        bool[] dumpargs = new bool[] { options.Imports, options.Exports, options.Data, options.Scripts, options.Coalesced, options.Names, !options.LineSeparator, options.Properties, false, options.SWFs };


                        if (options.InputFile != null)
                        {
                            Console.Out.WriteLine("Dumping pcc data of " + options.InputFile +
                            " [Imports: " + options.Imports + ", Exports: " + options.Exports + ", Data: " + options.Data + ", Scripts: " + options.Scripts +
                            ", Coalesced: " + options.Coalesced + ", Names: " + options.Names + ", Properties: " + options.Properties + ", SWF: "+options.SWFs+"]");
                            dumpPCCFile(options.InputFile, dumpargs, options.OutputFolder);
                        }
                        if (options.InputFolder != null)
                        {
                            Console.Out.WriteLine("Dumping pcc data from " + options.InputFolder +
                            " [Imports: " + options.Imports + ", Exports: " + options.Exports + ", Data: " + options.Data + ", Scripts: " + options.Scripts +
                            ", Coalesced: " + options.Coalesced + ", Names: " + options.Names + ", Properties: " + options.Properties + ", SWF: " + options.SWFs + "]");
                            dumpPCCFolder(options.InputFolder, dumpargs, options.OutputFolder);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Nothing was selected to dump. Use --scripts, --names, --data, --imports, --exports, --swf or --coalesced to dump items from a pcc.");
                        endProgram(CODE_NO_DATA_TO_DUMP);
                    }
                }
                else if (options.Transplant)
                {
                    if (options.InputFile == null)
                    {
                        Console.Error.WriteLine("--transplant requires --inputfile.");
                        endProgram(CODE_INPUT_FILE_NOT_FOUND);
                    }
                    if (options.TargetFile == null)
                    {
                        Console.Error.WriteLine("--targetfile is required for this operation.");
                        endProgram(CODE_INPUT_FILE_NOT_FOUND);
                    }
                    if (!File.Exists(options.TargetFile))
                    {
                        Console.Error.WriteLine("Target file does not exist: " + options.TargetFile);
                        endProgram(CODE_INPUT_FILE_NOT_FOUND);
                    }

                    if (options.TargetFile.ToLower() == options.InputFile.ToLower())
                    {
                        Console.Error.WriteLine("Cannot transplant GUI files into self");
                        endProgram(CODE_SAME_IN_OUT_FILE);
                    }

                    Console.WriteLine("Transplanting GUI files from " + options.InputFile + " to " + options.TargetFile);
                    Console.WriteLine("Extracting GUI files");
                    string gfxfolder = AppDomain.CurrentDomain.BaseDirectory + @"extractedgfx\";
                    writeVerboseLine("Extracting GFX Files from source to " + gfxfolder);
                    extractAllGFxMovies(options.InputFile, gfxfolder);
                    Console.WriteLine("Installing GUI files");
                    replaceSWFs(gfxfolder, options.TargetFile,options.Compress);
                }
                else
                {
                    Console.Error.WriteLine("No operation was specified");
                    Console.Error.WriteLine(options.GetUsage());
                    endProgram(CODE_NO_OPERATION);
                }
            }
            endProgram(0);
        }

        private static void endProgram(int code)
        {
            pauseIfDebug();
            Environment.Exit(code);
        }

        [ConditionalAttribute("DEBUG")]
        private static void pauseIfDebug()
        {
            Console.WriteLine("Press Enter to exit");
            Console.ReadLine();
        }
    }
}
