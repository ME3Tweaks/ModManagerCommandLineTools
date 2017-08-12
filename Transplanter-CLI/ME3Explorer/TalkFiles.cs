using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TransplanterLib
{
    public static class TalkFiles
    {
        public static List<TalkFile> tlkList = new List<TalkFile>();

        public static void LoadTlkData(string fileName)
        {
            if (File.Exists(fileName))
            {
                TalkFile tlk = new TalkFile();
                tlk.LoadTlkData(fileName);
                tlkList.Add(tlk);
            }
        }
        public static string findDataById(int strRefID, bool withFileName = false)
        {
            string s = "No Data";
            foreach (TalkFile tlk in tlkList)
            {
                s = tlk.findDataById(strRefID);
                if (s != "No Data")
                {
                    if (withFileName)
                    {
                        s += " (" + tlk.name + ")";
                    }
                    return s;
                }
            }
            return s;
        }

        public static void LoadSavedTlkList()
        {
            string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase) + "\\LoadedTLKs.JSON";
            if (File.Exists(path))
            {
                List<string> files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path));
                foreach (string filePath in files)
                {
                    LoadTlkData(filePath);
                }
            }
            else
            {
                string tlkPath = TransplanterLib.GamePath + "CookedPCConsole\\BIOGame_INT.tlk";
                LoadTlkData(tlkPath);
            }
        }

        public static void addTLK(string fileName)
        {
            LoadTlkData(fileName);
        }

        public static void removeTLK(int index)
        {
            tlkList.RemoveAt(index);
        }

        public static void moveTLKUp(int index)
        {
            TalkFile tlk = tlkList[index];
            tlkList.RemoveAt(index);
            tlkList.Insert(index - 1, tlk);
        }

        public static void moveTLKDown(int index)
        {
            TalkFile tlk = tlkList[index];
            tlkList.RemoveAt(index);
            tlkList.Insert(index + 1, tlk);
        }
    }
}
