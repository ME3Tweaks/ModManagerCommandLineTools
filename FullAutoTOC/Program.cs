using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FullAutoTOC

{

    class Options
    {
        [Option('t', "tocfile", HelpText = "PCConsoleTOC.bin file to operate on. Using this command will run the program in Local AutoTOC mode. Requires --tocupdates.")]
        public string TOCFile { get; set; }

        [Option('g', "gamepath", HelpText = "Path to the main Mass Effect 3 game directory. All TOC-able item's will be updated - Basegame, SFARs, Custom DLC, and TESTPATCH (Patch_001.sfar).")]
        public string GamePath { get; set; }

        [Option('d', "dumptoc", HelpText = "Prints all entries in the specified --tocfile.")]
        public bool DumpTOC { get; set; }

        [OptionArray('f', "tocfolders", HelpText = "Creates a PCConsoleTOC.bin file at the root of the specified folders, for that folder.")]
        public string[] TOCFolders { get; set; }

        [OptionArray('u', "tocupdates", HelpText = "List of files (as listed in the TOC entries) and filesizes to update in the file listed in --tocfile. Items must alternate, starting with path and then the size.")]
        public string[] TOCUpdates { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    /// <summary>
    /// This program runs AutoTOC on the ME3 game directory. It can also run in local mode which will update a specified TOC bin file with a matching list of filenames followed by sizes (Used by Mod Manager Run AutoTOC on mod - useful for distributing DLC mods)
    /// It is different because it also works on modified SFARs - based on the size, as its nearly
    /// impossible to make SFARs that are the exact right size as the originals.
    /// </summary>
    class AutoTOC
    {
        private const string SFAR_SUBPATH = @"CookedPCConsole\Default.sfar";
        private const long TESTPATCH_16_SIZE = 2455091L;
        private static Dictionary<string, long> sfarsizemap;
        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                //Console.WriteLine("FullAutoTOC by FemShep (based on AutoTOC.exe & ME3Explorer code)");
                //Console.WriteLine("Usage: FullAutoTOC.exe <ME3 Directory>");
                RunFullGameTOC(args[0]);
                EndProgram(0);
            }
            Options options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                if (options.GamePath != null)
                {
                    RunFullGameTOC(options.GamePath);
                    EndProgram(0);
                }

                if (options.TOCFolders != null && options.TOCFolders.Length > 0)
                {
                    //precheck
                    foreach (string folder in options.TOCFolders)
                    {
                        if (!Directory.Exists(folder))
                        {
                            Console.WriteLine("One of the specified folders to make a TOC for does not exist: " +folder);
                            EndProgram(1);
                        }
                    }

                    Parallel.ForEach(options.TOCFolders, folder =>
                    {
                        CreateUnpackedTOC(folder);
                    });
                EndProgram(0);
                }

                if (options.TOCFile != null)
                {
                    if (!File.Exists(options.TOCFile))
                    {
                        Console.WriteLine("TOC file to operate on doesn't exist: " + options.TOCFile);
                        EndProgram(1);
                    }
                    TOCBinFile tbf = new TOCBinFile(options.TOCFile);
                    if (options.DumpTOC)
                    {
                        int index = 0;
                        Console.WriteLine("Index\tOffset\tFilesize\tFilename");

                        foreach (TOCBinFile.Entry e in tbf.Entries)
                        {
                            Console.WriteLine(index+"\t0x"+e.offset.ToString("X6") + "\t" + e.size + "\t" + e.name);
                            index++;
                        }
                        EndProgram(0);
                    }

                    if (options.TOCUpdates == null || options.TOCUpdates.Length == 0 || options.TOCUpdates.Length % 2 != 0)
                    {
                        Console.WriteLine("--tocfile without --dumptoc requires --tocupdates followed by an even number of arguments repeating in a <filepath filesize> pattern.");
                        EndProgram(1);
                    }

                    Dictionary<string, int> updates = new Dictionary<string, int>();

                    int getindex = 0;
                    for (int i = 0; i < options.TOCUpdates.Length; i++, getindex++)
                    {
                        string path = options.TOCUpdates[i];
                        i++;
                        try
                        {
                            int size = Convert.ToInt32(options.TOCUpdates[i]);
                            updates.Add(path, size);
                        } catch (FormatException e)
                        {
                            Console.WriteLine("ERROR READING ARGUMENT (" + options.TOCUpdates[i] + ") - cannot parse integer for size update.");
                            EndProgram(1);
                        }
                    }

                    

                    //ITERATE OVER EACH ENTRY, USE LINQ TO FIND INDEX, UPDATE ENTRY.
                    bool needssaving = false;
                    foreach (KeyValuePair<string, int> update in updates)
                    {
                        bool found = false;
                        for (int i = 0; i < tbf.Entries.Count; i++)
                        {
                            TOCBinFile.Entry entry = tbf.Entries[i];
                            if (entry.name.Contains(update.Key))
                            {
                                {
                                    found = true;
                                    if (entry.size != update.Value || true)
                                    {
                                        Console.WriteLine("Updating entry " + update.Key);
                                        tbf.UpdateEntry(i, update.Value);
                                        //tbf.UpdateEntry(i, 5000);
                                        //Console.WriteLine("Readback: " + tbf.Entries[i].size);
                                        needssaving = true;
                                    }
                                    break;
                                }
                            }
                            
                        }
                        if (!found)
                        {
                            Console.WriteLine("The entry " + update.Key + " was not found in this TOC file.");
                            EndProgram(1);
                        }
                    }
                    if (needssaving)
                    {
                        File.WriteAllBytes(options.TOCFile,tbf.Save().ToArray());
                    }

                    //RunFullGameTOC(args[0]);
                    EndProgram(0);
                }
            }
        }

        private static void RunFullGameTOC(string gameDir)
        {
            GetSFARSizeMap();
            Console.WriteLine("FULL AUTOTOC MODE - Updating Unpacked and SFAR TOCs");
            if (gameDir.EndsWith("\""))
            {
                gameDir = gameDir.Remove(gameDir.Length - 1);
            }
            if (!Directory.Exists(gameDir))
            {
                Console.WriteLine("ERROR: Specified game directory does not exist: "+gameDir);
                EndProgram(1);
            }
            string baseDir = Path.Combine(gameDir, @"BIOGame\");
            string dlcDir = Path.Combine(baseDir, @"DLC\");
            if (!Directory.Exists(dlcDir))
            {
                Console.WriteLine("ERROR: Specified game directory does not appear to be a Mass Effect 3 root game directory (DLC folder missing).");
                EndProgram(1);
            }
            string testpatchpath = Path.Combine(gameDir, @"BIOGame\Patches\PCConsole\Patch_001.sfar");
            List<string> folders = (new DirectoryInfo(dlcDir)).GetDirectories().Select(d => d.FullName + "\\").Where(x => x.StartsWith(dlcDir + "DLC_", StringComparison.OrdinalIgnoreCase)).ToList();
            folders.Add(baseDir);
            Console.WriteLine("Found TOC Targets:");
            folders.ForEach(x => Console.WriteLine(x));
            Console.WriteLine("=====Generating TOC Files=====");
            Parallel.ForEach(folders, (currentfolder) =>
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                // The more computational work you do here, the greater 
                // the speedup compared to a sequential foreach loop.
                string foldername = Path.GetFileName(Path.GetDirectoryName(currentfolder));
                string sfar = currentfolder + SFAR_SUBPATH;
                long size = 0;
                sfarsizemap.TryGetValue(foldername, out size);

                if (size > 0)
                {
                    //Official DLC
                    if (File.Exists(sfar))
                    {
                        long installedsize = new FileInfo(sfar).Length;
                        if (installedsize != size)
                        {
                            if (installedsize > size)
                            {
                                //AutoTOC it - SFAR is not unpacked
                                DLCPackage dlc = new DLCPackage(sfar);
                                dlc.UpdateTOCbin();
                                watch.Stop();
                                var elapsedMs = watch.ElapsedMilliseconds;
                                Console.WriteLine(foldername + " - Ran SFAR TOC, took "+ elapsedMs + "ms");
                            } else
                            {
                                //AutoTOC it - SFAR is unpacked
                                CreateUnpackedTOC(currentfolder);
                                watch.Stop();
                                var elapsedMs = watch.ElapsedMilliseconds;
                                Console.WriteLine(foldername + " - Ran Unpacked TOC, took " + elapsedMs + "ms");
                            }
                        }
                        else
                        {
                            //We're good
                            //Console.WriteLine(foldername + ", - SFAR TOC - Unmodified");
                        }
                    }

                }
                else
                {
                    //TOC it unpacked style
                    // Console.WriteLine(foldername + ", - UNPACKED TOC");
                    CreateUnpackedTOC(currentfolder);
                    var elapsedMs = watch.ElapsedMilliseconds;
                    Console.WriteLine(foldername + " - Ran Unpacked TOC, took " + elapsedMs + "ms");
                }
            });
            //TOC TestPatch

            if (File.Exists(testpatchpath))
            {
                Console.WriteLine("TESTPATCH - Found TESTPATCH at "+testpatchpath);

                long installedsize = new FileInfo(testpatchpath).Length;
                if (installedsize != sfarsizemap["DLC_TestPatch"] && installedsize != TESTPATCH_16_SIZE)
                {
                    {
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        //AutoTOC it
                        DLCPackage dlc = new DLCPackage(testpatchpath);
                        dlc.UpdateTOCbin();
                        var elapsedMs = watch.ElapsedMilliseconds;
                        Console.WriteLine("TESTPATCH - Ran SFAR TOC, took " + elapsedMs + "ms");
                    }
                } else
                {
                    Console.WriteLine("TESTPATCH does not need TOC'd.");

                }

                EndProgram(0);
                //Task.WhenAll(folders.Select(loc => TOCAsync(loc))).Wait();

                //Console.WriteLine("Done!");
            }
        }

        /// <summary>
        /// Creates a PCConsoleTOC.bin file for the specified folder.
        /// </summary>
        /// <param name="folderToTOC">Directory to create TOC for. A PCConsoleTOC.bin file is placed at the root of this folder.</param>
        static void CreateUnpackedTOC(string folderToTOC)
        {
            if (!folderToTOC.EndsWith("\\"))
            {
                folderToTOC = folderToTOC + "\\";
            }
            List<string> files = GetFiles(folderToTOC);
            if (files.Count != 0)
            {
                string t = files[0];
                int n = t.IndexOf("DLC_");
                if (n > 0)
                {
                    for (int i = 0; i < files.Count; i++)
                        files[i] = files[i].Substring(n);
                    string t2 = files[0];
                    n = t2.IndexOf("\\");
                    for (int i = 0; i < files.Count; i++)
                        files[i] = files[i].Substring(n + 1);
                }
                else
                {
                    n = t.IndexOf("BIOGame");
                    if (n > 0)
                    {
                        for (int i = 0; i < files.Count; i++)
                            files[i] = files[i].Substring(n);
                    }
                }
                string pathbase;
                string t3 = files[0];
                int n2 = t3.IndexOf("BIOGame");
                if (n2 >= 0)
                {
                    pathbase = Path.GetDirectoryName(Path.GetDirectoryName(folderToTOC)) + "\\";
                }
                else
                {
                    pathbase = folderToTOC;
                }
                CreateUnpackedTOC(pathbase, folderToTOC + "PCConsoleTOC.bin", files.ToArray());
            }
        }

        static void CreateUnpackedTOC(string basepath, string tocFile, string[] files)
        {
            FileStream fs = new FileStream(tocFile, FileMode.Create, FileAccess.Write);
            fs.Write(BitConverter.GetBytes((int)0x3AB70C13), 0, 4);
            fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
            fs.Write(BitConverter.GetBytes((int)0x1), 0, 4);
            fs.Write(BitConverter.GetBytes((int)0x8), 0, 4);
            fs.Write(BitConverter.GetBytes((int)files.Length), 0, 4);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (i == files.Length - 1)//Entry Size
                    fs.Write(new byte[2], 0, 2);
                else
                    fs.Write(BitConverter.GetBytes((ushort)(0x1D + file.Length)), 0, 2);
                fs.Write(BitConverter.GetBytes((ushort)0), 0, 2);//Flags
                if (Path.GetFileName(file).ToLower() != "pcconsoletoc.bin")
                {
                    FileStream fs2 = new FileStream(basepath + file, FileMode.Open, FileAccess.Read);
                    fs.Write(BitConverter.GetBytes((int)fs2.Length), 0, 4);//Filesize
                    fs2.Close();
                }
                else
                {
                    fs.Write(BitConverter.GetBytes((int)0), 0, 4);//Filesize
                }
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);//SHA1
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                fs.Write(BitConverter.GetBytes((int)0x0), 0, 4);
                foreach (char c in file)
                    fs.WriteByte((byte)c);
                fs.WriteByte(0);
            }
            fs.Close();
        }

        static List<string> GetFiles(string basefolder)
        {
            List<string> res = new List<string>();
            string test = Path.GetFileName(Path.GetDirectoryName(basefolder));
            string[] files = DirFiles(basefolder);
            res.AddRange(files);
            DirectoryInfo folder = new DirectoryInfo(basefolder);
            DirectoryInfo[] folders = folder.GetDirectories();
            if (folders.Length != 0)
                if (test != "BIOGame")
                    foreach (DirectoryInfo f in folders)
                        res.AddRange(GetFiles(basefolder + f.Name + "\\"));
                else
                    foreach (DirectoryInfo f in folders)
                        if (f.Name == "CookedPCConsole" || f.Name == "Movies" || f.Name == "Splash")
                            res.AddRange(GetFiles(basefolder + f.Name + "\\"));
            return res;
        }

        static string[] Pattern = { "*.pcc", "*.afc", "*.bik", "*.bin", "*.tlk", "*.txt", "*.cnd", "*.upk", "*.tfc" };

        static string[] DirFiles(string path)
        {
            List<string> res = new List<string>();
            foreach (string s in Pattern)
                res.AddRange(Directory.GetFiles(path, s));
            return res.ToArray();
        }

        /// <summary>
        /// Gets the sizes of each SFAR in an unmodified state.
        /// This does not account for 1.6 TESTPATCH.
        /// </summary>
        /// <returns></returns>
        static Dictionary<string, long> GetSFARSizeMap()
        {
            if (sfarsizemap == null)
            {
                sfarsizemap = new Dictionary<string, long>();
                sfarsizemap["DLC_CON_MP1"] = 220174473L;
                sfarsizemap["DLC_CON_MP2"] = 139851674L;
                sfarsizemap["DLC_CON_MP3"] = 198668075L;
                sfarsizemap["DLC_CON_MP4"] = 441856666L;
                sfarsizemap["DLC_CON_MP5"] = 208777784L;

                sfarsizemap["DLC_UPD_Patch01"] = 208998L;
                sfarsizemap["DLC_UPD_Patch02"] = 302772L;
                sfarsizemap["DLC_TestPatch"] = 2455154L; //1.6 also has a version

                sfarsizemap["DLC_HEN_PR"] = 594778936L;
                sfarsizemap["DLC_CON_END"] = 1919137514L;

                sfarsizemap["DLC_EXP_Pack001"] = 1561239503L;
                sfarsizemap["DLC_EXP_Pack002"] = 1849136836L;
                sfarsizemap["DLC_EXP_Pack003"] = 1886013531L;
                sfarsizemap["DLC_EXP_Pack003_Base"] = 1896814656L;
                sfarsizemap["DLC_CON_APP01"] = 53878606L;
                sfarsizemap["DLC_CON_GUN01"] = 18708500L;
                sfarsizemap["DLC_CON_GUN02"] = 17134896L;
                sfarsizemap["DLC_CON_DH1"] = 284862077L;
                sfarsizemap["DLC_OnlinePassHidCE"] = 56321927L;
            }
            return sfarsizemap;
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