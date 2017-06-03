﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
//using AmaroK86.MassEffect3.ZlibBlock;
using System.Collections.Concurrent;
using System.Windows;
using System.Collections.ObjectModel;

namespace PCCTools.PackageClasses
{
    public static class MEPackageHandler
    {
        static Dictionary<string, IMEPackage> openPackages = new Dictionary<string, IMEPackage>();
        public static ObservableCollection<IMEPackage> packagesInTools = new ObservableCollection<IMEPackage>();

        static Func<string, ME1Package> ME1ConstructorDelegate;
        static Func<string, ME2Package> ME2ConstructorDelegate;
        static Func<string, ME3Package> ME3ConstructorDelegate;

        public static void Initialize()
        {
            ME1ConstructorDelegate = ME1Package.Initialize();
            ME2ConstructorDelegate = ME2Package.Initialize();
            ME3ConstructorDelegate = ME3Package.Initialize();
        }

        public static IMEPackage OpenMEPackage(string pathToFile)
        {
            IMEPackage package = null;
            if (!openPackages.ContainsKey(pathToFile))
            {
                ushort version;
                ushort licenseVersion;
                using (FileStream fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(4, SeekOrigin.Begin);
                    version = fs.ReadValueU16();
                    licenseVersion = fs.ReadValueU16();
                }

                if (version == 684 && licenseVersion == 194)
                {
                    package = ME3ConstructorDelegate(pathToFile);
                }
                else if (version == 512 && licenseVersion == 130)
                {
                    package = ME2ConstructorDelegate(pathToFile);
                }
                else if (version == 491 && licenseVersion == 1008)
                {
                    package = ME1ConstructorDelegate(pathToFile);
                }
                else
                {
                    throw new FormatException("Not an ME1, ME2, or ME3 package file.");
                }
                package.noLongerUsed += Package_noLongerUsed;
                openPackages.Add(pathToFile, package);
            }
            else
            {
                package = openPackages[pathToFile];
            }

            package.RegisterUse();
            return package;
        }

        private static void Package_noLongerUsed(object sender, EventArgs e)
        {
            openPackages.Remove((sender as IMEPackage).FileName);
        }

        private static void addToPackagesInTools(IMEPackage package)
        {
            if (!packagesInTools.Contains(package))
            {
                packagesInTools.Add(package);
                package.noLongerOpenInTools += Package_noLongerOpenInTools;
            }
        }

        private static void Package_noLongerOpenInTools(object sender, EventArgs e)
        {
            IMEPackage package = sender as IMEPackage;
            packagesInTools.Remove(package);
            package.noLongerOpenInTools -= Package_noLongerOpenInTools;

        }

        public static ME3Package OpenME3Package(string pathToFile)
        {
            IMEPackage pck = OpenMEPackage(pathToFile);
            ME3Package pcc = pck as ME3Package;
            if (pcc == null)
            {
                pck.Release();
                throw new FormatException("Not an ME3 package file.");
            }
            return pcc;
        }

        public static ME2Package OpenME2Package(string pathToFile)
        {
            IMEPackage pck = OpenMEPackage(pathToFile);
            ME2Package pcc = pck as ME2Package;
            if (pcc == null)
            {
                pck.Release();
                throw new FormatException("Not an ME2 package file.");
            }
            return pcc;
        }

        public static ME1Package OpenME1Package(string pathToFile)
        {
            IMEPackage pck = OpenMEPackage(pathToFile);
            ME1Package pcc = pck as ME1Package;
            if (pcc == null)
            {
                pck.Release();
                throw new FormatException("Not an ME1 package file.");
            }
            return pcc;
        }
    }
}
