﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PCCTools.PackageClasses
{
    public enum MEGame
    {
        ME1 = 1,
        ME2,
        ME3,
    }

    public enum ArrayType
    {
        Object,
        Name,
        Enum,
        Struct,
        Bool,
        String,
        Float,
        Int,
        Byte,
    }

    public class PropertyInfo
    {
        public Unreal.PropertyType type;
        public string reference;
    }

    public class ClassInfo
    {
        public Dictionary<string, PropertyInfo> properties;
        public string baseClass;
        //Relative to BIOGame
        public string pccPath;
        public int exportIndex;

        public ClassInfo()
        {
            properties = new Dictionary<string, PropertyInfo>();
        }
    }

    public interface IMEPackage : IDisposable
    {
        bool IsCompressed { get; }
        bool CanReconstruct { get; }
        bool IsModified { get; }
        int ExportCount { get; }
        int ImportCount { get; }
        int ImportOffset { get; }
        IReadOnlyList<IExportEntry> Exports { get; }
        IReadOnlyList<ImportEntry> Imports { get; }
        IReadOnlyList<string> Names { get; }
        MEGame Game { get; }
        string FileName { get; }
        DateTime LastSaved { get; }
        long FileSize { get; }

        //reading
        bool isExport(int index);
        bool isImport(int index);
        bool isName(int index);
        /// <summary>
        ///     gets Export or Import entry
        /// </summary>
        /// <param name="index">unreal index</param>
        IEntry getEntry(int index);
        IExportEntry getExport(int index);
        ImportEntry getImport(int index);
        int findName(string nameToFind);
        string getObjectName(int index);
        string getNameEntry(int index);
        string getObjectClass(int index);

        //editing
        void addName(string name);
        int FindNameOrAdd(string name);
        void replaceName(int index, string newName);
        void addExport(IExportEntry exportEntry);
        void addImport(ImportEntry importEntry);
        /// <summary>
        ///     exposed so that the property import function can restore the namelist after a failure.
        ///     please don't use it anywhere else.
        /// </summary>
        void setNames(List<string> list);

        //saving
        void save();
        void save(string path);

        void Release();
        event EventHandler noLongerOpenInTools;
        void RegisterUse();
        event EventHandler noLongerUsed;
    }
}