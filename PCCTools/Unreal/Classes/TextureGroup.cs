﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using PCCTools.PackageClasses;

namespace PCCTools.Unreal.Classes
{
    internal class TextureGroup
    {
        public class ByteProp
        {
            public string name { get; protected set; }
            public int value { get; protected set; }

            public ByteProp(string name, int value)
            {
                this.name = name;
                this.value = value;
            }
        }

        ME3Package pccRef;
        uint firstVal;
        uint otherVal;

        public List<ByteProp> enumTextureGroups { get; private set; }

        public TextureGroup(ME3Package pccObj, byte[] data)
        {
            enumTextureGroups = new List<ByteProp>();
            pccRef = pccObj;

            MemoryStream buffer = new MemoryStream(data);

            firstVal = buffer.ReadValueU32();
            buffer.Seek(16, SeekOrigin.Begin);
            otherVal = buffer.ReadValueU32();

            int numEnums = buffer.ReadValueS32();
            for (int i = 0; i < numEnums; i++)
            {
                ByteProp aux = new ByteProp(pccRef.Names[buffer.ReadValueS32()], buffer.ReadValueS32());
                enumTextureGroups.Add(aux);
            }
        }

        public bool ExistsTextureGroup(int idxName, int value)
        {
            return ExistsTextureGroup(pccRef.Names[idxName], value);
        }

        public bool ExistsTextureGroup(string name, int value)
        {
            return enumTextureGroups.Exists(texGroup => texGroup.name == name && texGroup.value == value);
        }

        public void Add(int idxName, int value)
        {
            Add(pccRef.Names[idxName], value);
        }

        public void Add(string name, int value)
        {
            if (!ExistsTextureGroup(name, value))
            {
                enumTextureGroups.Add(new ByteProp(name, value));
            }
        }

        public byte[] ToArray()
        {
            MemoryStream buffer = new MemoryStream();
            buffer.WriteValueU32(firstVal);
            buffer.WriteValueS32(pccRef.findName("None"));
            buffer.Seek(16, SeekOrigin.Begin);
            buffer.WriteValueU32(otherVal);
            buffer.WriteValueS32(enumTextureGroups.Count);
            foreach (ByteProp byteProp in enumTextureGroups)
            {
                buffer.WriteValueS32(pccRef.FindNameOrAdd(byteProp.name));
                buffer.WriteValueS32(byteProp.value);
            }

            return buffer.ToArray();
        }
    }
}
