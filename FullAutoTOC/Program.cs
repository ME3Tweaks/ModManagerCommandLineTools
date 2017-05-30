using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FullAutoTOC

{
    /// <summary>
    /// This program runs AutoTOC on the ME3 game directory.
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
            if (args.Length != 1)
            {
                Console.WriteLine("FullAutoTOC by FemShep (based on AutoTOC.exe & ME3Explorer code)");
                Console.WriteLine("Usage: FullAutoTOC.exe <ME3 Directory>");
            }
            else
            {
                GetSFARSizeMap();

                Console.WriteLine("Updating Unpacked and SFAR TOCs");
                string gameDir = args[0];
                if (gameDir.EndsWith("\""))
                {
                    gameDir = gameDir.Remove(gameDir.Length - 1);
                }
                if (!Directory.Exists(gameDir))
                {
                    Console.WriteLine("ERROR: Input game directory does not exist.");
                    Environment.Exit(1);
                }
                string baseDir = Path.Combine(gameDir, @"BIOGame\");
                string dlcDir = Path.Combine(baseDir, @"DLC\");
                if (!Directory.Exists(dlcDir))
                {
                    Console.WriteLine("ERROR: Input directory does not appear to be a Mass Effect 3 root game directory (DLC folder missing).");
                    Environment.Exit(1);
                }
                string testpatchpath = Path.Combine(gameDir, @"BIOGame\Patches\PCConsole\Patch_001.sfar");
                List<string> folders = (new DirectoryInfo(dlcDir)).GetDirectories().Select(d => d.FullName + "\\").Where(x => x.StartsWith(dlcDir + "DLC_", StringComparison.OrdinalIgnoreCase)).ToList();
                folders.Add(baseDir);
                Console.WriteLine("Files:");
                folders.ForEach(x => Console.WriteLine(x));

                Parallel.ForEach(folders, (currentfolder) =>
                {
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
                                //AutoTOC it
                                DLCPackage dlc = new DLCPackage(sfar);
                                dlc.UpdateTOCbin();
                                Console.WriteLine(foldername + " - Ran SFAR TOC");

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
                        prepareToCreateTOC(currentfolder);
                        Console.WriteLine(foldername + " - Ran Unpacked TOC");
                    }
                });
                //TOC TestPatch

                if (File.Exists(testpatchpath))
                {
                    long installedsize = new FileInfo(testpatchpath).Length;
                    if (installedsize != sfarsizemap["DLC_TestPatch"] && installedsize != TESTPATCH_16_SIZE)
                    {
                        {
                            //AutoTOC it
                            DLCPackage dlc = new DLCPackage(testpatchpath);
                            dlc.UpdateTOCbin();
                            Console.WriteLine("TESTPATCH - Ran SFAR TOC");
                        }
                    }

#if DEBUG
                    Console.WriteLine("Any key to continue");
                    Console.ReadKey();
#endif
                    //Task.WhenAll(folders.Select(loc => TOCAsync(loc))).Wait();

                    //Console.WriteLine("Done!");
                }
            }
        }



        static void prepareToCreateTOC(string consoletocFile)
        {
            if (!consoletocFile.EndsWith("\\"))
            {
                consoletocFile = consoletocFile + "\\";
            }
            List<string> files = GetFiles(consoletocFile);
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
                    pathbase = Path.GetDirectoryName(Path.GetDirectoryName(consoletocFile)) + "\\";
                }
                else
                {
                    pathbase = consoletocFile;
                }
                CreateUnpackedTOC(pathbase, consoletocFile + "PCConsoleTOC.bin", files.ToArray());
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
    }
}