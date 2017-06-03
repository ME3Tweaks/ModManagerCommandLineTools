﻿using System.ComponentModel;
using PCCTools.Unreal;

namespace PCCTools.PackageClasses
{
    public interface IEntry
    {
        bool HeaderChanged { get; }
        int Index { get; set; }
        int UIndex { get; }
        byte[] header { get; }
        IMEPackage FileRef { get; }
        int idxLink { get; set; }
        int idxObjectName { get; set; }
        string ClassName { get; }
        string GetFullPath { get; }
        string ObjectName { get; }
        string PackageFullName { get; }
        string PackageName { get; }
    }

    public interface IExportEntry : IEntry
    {
        bool DataChanged { get; set; }
        /// <summary>
        /// RETURNS A CLONE
        /// </summary>
        byte[] Data { get; set; }
        int DataOffset { get; set; }
        int DataSize { get; set; }
        int idxArchtype { get; set; }
        int idxClass { get; set; }
        int idxClassParent { get; set; }
        int indexValue { get; set; }
        string ArchtypeName { get; }
        string ClassParent { get; }
        uint headerOffset { get; set; }
        ulong ObjectFlags { get; set; }
        int OriginalDataSize { get; }

        IExportEntry Clone();
        void setHeader(byte[] v);

        //event PropertyChangedEventHandler PropertyChanged;

        PropertyCollection GetProperties();
        void WriteProperties(PropertyCollection props);
        int propsEnd();
        int GetPropertyStart();
        byte[] getBinaryData();
        void setBinaryData(byte[] binaryData);
        T GetProperty<T>(string name) where T : UProperty;
        void WriteProperty(UProperty prop);
    }
}
