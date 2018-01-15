using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;
using TransplanterLib;

namespace TransplanterLib
{
    public partial class Interpreter
    {

        public PCCObject Pcc { get { return pcc; } set { pcc = value; defaultStructValues.Clear(); } }
        public int Index;
        public string className;
        public byte[] memory;
        public int memsize;
        public int readerpos;

        public struct PropHeader
        {
            public int name;
            public int type;
            public int size;
            public int index;
            public int offset;
        }

        public string[] Types =
        {
            "StructProperty", //0
            "IntProperty",
            "FloatProperty",
            "ObjectProperty",
            "NameProperty",
            "BoolProperty",  //5
            "ByteProperty",
            "ArrayProperty",
            "StrProperty",
            "StringRefProperty",
            "DelegateProperty",//10
            "None",
            "BioMask4Property",
        };

        public enum nodeType
        {
            Unknown = -1,
            StructProperty = 0,
            IntProperty = 1,
            FloatProperty = 2,
            ObjectProperty = 3,
            NameProperty = 4,
            BoolProperty = 5,
            ByteProperty = 6,
            ArrayProperty = 7,
            StrProperty = 8,
            StringRefProperty = 9,
            DelegateProperty = 10,
            None,
            BioMask4Property,

            ArrayLeafObject,
            ArrayLeafName,
            ArrayLeafEnum,
            ArrayLeafStruct,
            ArrayLeafBool,
            ArrayLeafString,
            ArrayLeafFloat,
            ArrayLeafInt,
            ArrayLeafByte,

            StructLeafByte,
            StructLeafFloat,
            StructLeafDeg, //indicates this is a StructProperty leaf that is in degrees (actually unreal rotation units)
            StructLeafInt,
            StructLeafObject,
            StructLeafName,
            StructLeafBool,
            StructLeafStr,
            StructLeafArray,
            StructLeafEnum,
            StructLeafStruct,

            Root,
        }


        private const int HEXBOX_MAX_WIDTH = 650;

        private PCCObject pcc;
        private Dictionary<string, List<PropertyReader.Property>> defaultStructValues;
        public TreeNode topNode;

        public Interpreter()
        {
            defaultStructValues = new Dictionary<string, List<PropertyReader.Property>>();
        }

        public void InitInterpreter()
        {
            memory = pcc.Exports[Index].Data;
            className = pcc.Exports[Index].ClassName;
            StartScan();
        }

        private void StartScan(IEnumerable<string> expandedNodes = null, string topNodeName = null, string selectedNodeName = null)
        {
            readerpos = PropertyReader.detectStart(pcc, memory, pcc.Exports[Index].ObjectFlags);
            BitConverter.IsLittleEndian = true;
            List<PropHeader> topLevelHeaders = ReadHeadersTillNone();
            topNode = new TreeNode("0000 : " + pcc.Exports[Index].ObjectName);
            topNode.Tag = nodeType.Root;
            topNode.Name = "0";
            try
            {
                GenerateTree(topNode, topLevelHeaders);
            }
            catch (Exception ex)
            {
                topNode.Nodes.Add(new TreeNode("Parse error: " + ex.Message));
            }
            memsize = memory.Length;
        }

        public void GenerateTree(TreeNode localRoot, List<PropHeader> headersList)
        {
            foreach (PropHeader header in headersList)
            {
                if (readerpos > memory.Length)
                {
                    throw new IndexOutOfRangeException(": tried to read past bounds of Export Data");
                }
                nodeType type = getType(pcc.getNameEntry(header.type));
                if (type != nodeType.ArrayProperty && type != nodeType.StructProperty)
                    localRoot.Nodes.Add(GenerateNode(header));
                else
                {
                    if (type == nodeType.ArrayProperty)
                    {
                        TreeNode t = GenerateNode(header);
                        int arrayLength = BitConverter.ToInt32(memory, header.offset + 24);
                        readerpos = header.offset + 28;
                        int tmp = readerpos;
                        UnrealObjectInfo.ArrayType arrayType;
                        try
                        {
                            arrayType = UnrealObjectInfo.getArrayType(className, pcc.getNameEntry(header.name));
                        }
                        catch (Exception)
                        {
                            arrayType = UnrealObjectInfo.ArrayType.Int;
                        }
                        if (arrayType == UnrealObjectInfo.ArrayType.Struct)
                        {
                            UnrealObjectInfo.PropertyInfo info = UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(header.name));
                            t.Text = t.Text.Insert(t.Text.IndexOf("Size: ") - 2, $"({info.reference})");
                            for (int i = 0; i < arrayLength; i++)
                            {
                                readerpos = tmp;
                                int pos = tmp;
                                List<PropHeader> arrayListPropHeaders = ReadHeadersTillNone();
                                tmp = readerpos;
                                TreeNode n = new TreeNode(i.ToString());
                                n.Tag = nodeType.ArrayLeafStruct;
                                n.Name = (-pos).ToString();
                                t.Nodes.Add(n);
                                n = t.LastNode;
                                if (info != null && (UnrealObjectInfo.isImmutable(info.reference) || arrayListPropHeaders.Count == 0))
                                {
                                    readerpos = pos;
                                    GenerateSpecialStruct(n, info.reference, header.size / arrayLength);
                                    tmp = readerpos;
                                }
                                else if (arrayListPropHeaders.Count > 0)
                                {
                                    GenerateTree(n, arrayListPropHeaders);
                                }
                                else
                                {
                                    throw new Exception($"at position {readerpos.ToString("X4")}. Could not read element {i} of ArrayProperty {pcc.getNameEntry(header.name)}");
                                }
                                t.LastNode.Remove();
                                t.Nodes.Add(n);
                            }
                            localRoot.Nodes.Add(t);
                        }
                        else
                        {
                            t.Text = t.Text.Insert(t.Text.IndexOf("Size: ") - 2, $"({arrayType.ToString()})");
                            int count = 0;
                            int pos;
                            for (int i = 0; i < (header.size - 4); count++)
                            {
                                pos = header.offset + 28 + i;
                                if (pos > memory.Length)
                                {
                                    throw new Exception(": tried to read past bounds of Export Data");
                                }
                                int val = BitConverter.ToInt32(memory, pos);
                                string s = pos.ToString("X4") + "|" + count + ": ";
                                TreeNode node = new TreeNode();
                                node.Name = pos.ToString();
                                if (arrayType == UnrealObjectInfo.ArrayType.Object)
                                {
                                    node.Tag = nodeType.ArrayLeafObject;
                                    int value = val;
                                    if (value == 0)
                                    {
                                        //invalid
                                        s += "Null [" + value + "] ";
                                    }
                                    else
                                    {

                                        bool isImport = value < 0;
                                        if (isImport)
                                        {
                                            value = -value;
                                        }
                                        value--; //0-indexed
                                        if (isImport)
                                        {
                                            if (pcc.Imports.Count > value)
                                            {
                                                s += pcc.Imports[value].PackageFullName + "." + pcc.Imports[value].ObjectName + " [IMPORT " + value + "]";
                                            }
                                            else
                                            {
                                                s += "Index not in import list [" + value + "]";
                                            }
                                        }
                                        else
                                        {
                                            if (pcc.Exports.Count > value)
                                            {
                                                s += pcc.Exports[value].PackageFullName+"."+pcc.Exports[value].ObjectName + " [EXPORT " + value + "]";
                                            }
                                            else
                                            {
                                                s += "Index not in export list [" + value + "]";
                                            }
                                        }
                                    }
                                    i += 4;
                                }
                                else if (arrayType == UnrealObjectInfo.ArrayType.Name || arrayType == UnrealObjectInfo.ArrayType.Enum)
                                {

                                    node.Tag = arrayType == UnrealObjectInfo.ArrayType.Name ? nodeType.ArrayLeafName : nodeType.ArrayLeafEnum;
                                    int value = val;
                                    if (value < 0)
                                    {
                                        s += "Invalid Name Index [" + value + "]";
                                    }
                                    else
                                    {
                                        if (pcc.Names.Count > value)
                                        {
                                            s += $"\"{pcc.Names[value]}\"_{BitConverter.ToInt32(memory, pos + 4)}[NAMEINDEX {value}]";
                                        }
                                        else
                                        {
                                            s += "Index not in name list [" + value + "]";
                                        }
                                    }
                                    i += 8;
                                }
                                else if (arrayType == UnrealObjectInfo.ArrayType.Float)
                                {
                                    node.Tag = nodeType.ArrayLeafFloat;
                                    s += BitConverter.ToSingle(memory, pos).ToString("0.0######");
                                    i += 4;
                                }
                                else if (arrayType == UnrealObjectInfo.ArrayType.Byte)
                                {
                                    node.Tag = nodeType.ArrayLeafByte;
                                    s += "(byte)" + memory[pos];
                                    i += 1;
                                }
                                else if (arrayType == UnrealObjectInfo.ArrayType.Bool)
                                {
                                    node.Tag = nodeType.ArrayLeafBool;
                                    s += BitConverter.ToBoolean(memory, pos);
                                    i += 1;
                                }
                                else if (arrayType == UnrealObjectInfo.ArrayType.String)
                                {
                                    node.Tag = nodeType.ArrayLeafString;
                                    int sPos = pos + 4;
                                    s += "\"";
                                    int len = val > 0 ? val : -val;
                                    for (int j = 1; j < len; j++)
                                    {
                                        s += BitConverter.ToChar(memory, sPos);
                                        sPos += 2;
                                    }
                                    s += "\"";
                                    i += (len * 2) + 4;
                                }
                                else
                                {
                                    node.Tag = nodeType.ArrayLeafInt;
                                    s += val.ToString();
                                    i += 4;
                                }
                                node.Text = s;
                                t.Nodes.Add(node);
                            }
                            localRoot.Nodes.Add(t);
                        }
                    }
                    if (type == nodeType.StructProperty)
                    {
                        TreeNode t = GenerateNode(header);
                        readerpos = header.offset + 32;
                        List<PropHeader> ll = ReadHeadersTillNone();
                        if (ll.Count != 0)
                        {
                            GenerateTree(t, ll);
                        }
                        else
                        {
                            string structType = pcc.getNameEntry(BitConverter.ToInt32(memory, header.offset + 24));
                            GenerateSpecialStruct(t, structType, header.size);
                        }
                        localRoot.Nodes.Add(t);
                    }

                }
            }
        }

        //structs that are serialized down to just their values.
        private void GenerateSpecialStruct(TreeNode t, string structType, int size)
        {
            TreeNode node;
            //have to handle this specially to get the degrees conversion
            if (structType == "Rotator")
            {
                string[] labels = { "Pitch", "Yaw", "Roll" };
                int val;
                for (int i = 0; i < 3; i++)
                {
                    val = BitConverter.ToInt32(memory, readerpos);
                    node = new TreeNode(readerpos.ToString("X4") + ": " + labels[i] + " : " + val + " (" + ((float)val * 360f / 65536f).ToString("0.0######") + " degrees)");
                    node.Name = readerpos.ToString();
                    node.Tag = nodeType.StructLeafDeg;
                    t.Nodes.Add(node);
                    readerpos += 4;
                }
            }
            else
            {
                if (UnrealObjectInfo.Structs.ContainsKey(structType))
                {
                    List<PropertyReader.Property> props;
                    //memoize
                    if (defaultStructValues.ContainsKey(structType))
                    {
                        props = defaultStructValues[structType];
                    }
                    else
                    {
                        byte[] defaultValue = UnrealObjectInfo.getDefaultClassValue(pcc, structType, true);
                        if (defaultValue == null)
                        {
                            //just prints the raw hex since there's no telling what it actually is
                            node = new TreeNode(readerpos.ToString("X4") + ": " + memory.Skip(readerpos).Take(size).Aggregate("", (b, s) => b + " " + s.ToString("X2")));
                            node.Tag = nodeType.Unknown;
                            t.Nodes.Add(node);
                            readerpos += size;
                            return;
                        }
                        props = PropertyReader.ReadProp(pcc, defaultValue, 0);
                        defaultStructValues.Add(structType, props);
                    }
                    for (int i = 0; i < props.Count; i++)
                    {
                        string s = readerpos.ToString("X4") + ": " + pcc.getNameEntry(props[i].Name) + " : ";
                        readerpos = GenerateSpecialStructProp(t, s, readerpos, props[i]);
                    }
                }
            }

            #region Old method
            //if (structType == "Vector2d" || structType == "RwVector2")
            //{
            //    string[] labels = { "X", "Y" };
            //    for (int i = 0; i < 2; i++)
            //    {
            //        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
            //        node.Name = pos.ToString();
            //        node.Tag = nodeType.StructLeafFloat;
            //        t.Nodes.Add(node);
            //        pos += 4;
            //    }
            //}
            //else if (structType == "Vector" || structType == "RwVector3")
            //{
            //    string[] labels = { "X", "Y", "Z" };
            //    for (int i = 0; i < 3; i++)
            //    {
            //        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
            //        node.Name = pos.ToString();
            //        node.Tag = nodeType.StructLeafFloat;
            //        t.Nodes.Add(node);
            //        pos += 4;
            //    }
            //}
            //else if (structType == "Rotator")
            //{
            //    string[] labels = { "Pitch", "Yaw", "Roll" };
            //    int val;
            //    for (int i = 0; i < 3; i++)
            //    {
            //        val = BitConverter.ToInt32(memory, pos);
            //        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + val + " (" + ((float)val * 360f / 65536f).ToString("0.0######") + " degrees)");
            //        node.Name = pos.ToString();
            //        node.Tag = nodeType.StructLeafDeg;
            //        t.Nodes.Add(node);
            //        pos += 4;
            //    }
            //}
            //else if (structType == "Color")
            //{
            //    string[] labels = { "B", "G", "R", "A" };
            //    for (int i = 0; i < 4; i++)
            //    {
            //        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + memory[pos]);
            //        node.Name = pos.ToString();
            //        node.Tag = nodeType.StructLeafByte;
            //        t.Nodes.Add(node);
            //        pos += 1;
            //    }
            //}
            //else if (structType == "LinearColor")
            //{
            //    string[] labels = { "R", "G", "B", "A" };
            //    for (int i = 0; i < 4; i++)
            //    {
            //        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
            //        node.Name = pos.ToString();
            //        node.Tag = nodeType.StructLeafFloat;
            //        t.Nodes.Add(node);
            //        pos += 4;
            //    }
            //}
            ////uses EndsWith to support RwQuat, RwVector4, and RwPlane
            //else if (structType.EndsWith("Quat") || structType.EndsWith("Vector4") || structType.EndsWith("Plane"))
            //{
            //    string[] labels = { "X", "Y", "Z", "W" };
            //    for (int i = 0; i < 4; i++)
            //    {
            //        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
            //        node.Name = pos.ToString();
            //        node.Tag = nodeType.StructLeafFloat;
            //        t.Nodes.Add(node);
            //        pos += 4;
            //    }
            //}
            //else if (structType == "TwoVectors")
            //{
            //    string[] labels = { "X", "Y", "Z", "X", "Y", "Z" };
            //    for (int i = 0; i < 6; i++)
            //    {
            //        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
            //        node.Name = pos.ToString();
            //        node.Tag = nodeType.StructLeafFloat;
            //        t.Nodes.Add(node);
            //        pos += 4;
            //    }
            //}
            //else if (structType == "Matrix" || structType == "RwMatrix44")
            //{
            //    string[] labels = { "X Plane", "Y Plane", "Z Plane", "W Plane" };
            //    string[] labels2 = { "X", "Y", "Z", "W" };
            //    TreeNode node2;
            //    for (int i = 0; i < 3; i++)
            //    {
            //        node2 = new TreeNode(labels[i]);
            //        node2.Name = pos.ToString();
            //        for (int j = 0; j < 4; j++)
            //        {
            //            node = new TreeNode(pos.ToString("X4") + ": " + labels2[j] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
            //            node.Name = pos.ToString();
            //            node.Tag = nodeType.StructLeafFloat;
            //            node2.Nodes.Add(node);
            //            pos += 4;
            //        }
            //        t.Nodes.Add(node2);
            //    }
            //}
            //else if (structType == "Guid")
            //{
            //    string[] labels = { "A", "B", "C", "D" };
            //    for (int i = 0; i < 4; i++)
            //    {
            //        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToInt32(memory, pos));
            //        node.Name = pos.ToString();
            //        node.Tag = nodeType.StructLeafInt;
            //        t.Nodes.Add(node);
            //        pos += 4;
            //    }
            //}
            //else if (structType == "IntPoint")
            //{
            //    string[] labels = { "X", "Y" };
            //    for (int i = 0; i < 2; i++)
            //    {
            //        node = new TreeNode(pos.ToString("X4") + ": " + labels[i] + " : " + BitConverter.ToInt32(memory, pos));
            //        node.Name = pos.ToString();
            //        node.Tag = nodeType.StructLeafInt;
            //        t.Nodes.Add(node);
            //        pos += 4;
            //    }
            //}
            //else if (structType == "Box" || structType == "BioRwBox")
            //{
            //    string[] labels = { "Min", "Max" };
            //    string[] labels2 = { "X", "Y", "Z" };
            //    TreeNode node2;
            //    for (int i = 0; i < 2; i++)
            //    {
            //        node2 = new TreeNode(labels[i]);
            //        node2.Name = pos.ToString();
            //        for (int j = 0; j < 3; j++)
            //        {
            //            node = new TreeNode(pos.ToString("X4") + ": " + labels2[j] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
            //            node.Name = pos.ToString();
            //            node.Tag = nodeType.StructLeafFloat;
            //            node2.Nodes.Add(node);
            //            pos += 4;
            //        }
            //        t.Nodes.Add(node2);
            //    }
            //    node = new TreeNode(pos.ToString("X4") + ": IsValid : " + memory[pos]);
            //    node.Name = pos.ToString();
            //    node.Tag = nodeType.StructLeafByte;
            //    t.Nodes.Add(node);
            //    pos += 1;
            //}
            //else
            //{
            //    for (int i = 0; i < size / 4; i++)
            //    {
            //        int val = BitConverter.ToInt32(memory, pos);
            //        string s = pos.ToString("X4") + ": " + val.ToString();
            //        t.Nodes.Add(s);
            //        pos += 4;
            //    }
            //}
            //readerpos = pos;
            #endregion
        }

        private int GenerateSpecialStructProp(TreeNode t, string s, int pos, PropertyReader.Property prop)
        {
            if (pos > memory.Length)
            {
                throw new Exception(": tried to read past bounds of Export Data");
            }
            int n;
            TreeNode node;
            UnrealObjectInfo.PropertyInfo propInfo;
            switch (prop.TypeVal)
            {
                case PropertyReader.Type.FloatProperty:
                    s += BitConverter.ToSingle(memory, pos).ToString("0.0######");
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafFloat;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyReader.Type.IntProperty:
                    s += BitConverter.ToInt32(memory, pos).ToString();
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyReader.Type.ObjectProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    s += n + " (" + pcc.getObjectName(n) + ")";
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafObject;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyReader.Type.StringRefProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    s += "#" + n + ": ";
                    s += TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : TalkFiles.findDataById(n);
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyReader.Type.NameProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    pos += 4;
                    s += "\"" + pcc.getNameEntry(n) + "\"_" + BitConverter.ToInt32(memory, pos);
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafName;
                    t.Nodes.Add(node);
                    pos += 4;
                    break;
                case PropertyReader.Type.BoolProperty:
                    s += (memory[pos] > 0).ToString();
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafBool;
                    t.Nodes.Add(node);
                    pos += 1;
                    break;
                case PropertyReader.Type.ByteProperty:
                    if (prop.Size != 1)
                    {
                        string enumName = UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(prop.Name))?.reference;
                        if (enumName != null)
                        {
                            s += "\"" + enumName + "\", ";
                        }
                        s += "\"" + pcc.getNameEntry(BitConverter.ToInt32(memory, pos)) + "\"";
                        node = new TreeNode(s);
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafEnum;
                        t.Nodes.Add(node);
                        pos += 8;
                    }
                    else
                    {
                        s += "(byte)" + memory[pos].ToString();
                        node = new TreeNode(s);
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafByte;
                        t.Nodes.Add(node);
                        pos += 1;
                    }
                    break;
                case PropertyReader.Type.StrProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    pos += 4;
                    s += "\"";
                    for (int i = 0; i < n - 1; i++)
                        s += (char)memory[pos + i * 2];
                    s += "\"";
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafStr;
                    t.Nodes.Add(node);
                    pos += n * 2;
                    break;
                case PropertyReader.Type.ArrayProperty:
                    n = BitConverter.ToInt32(memory, pos);
                    s += n + " elements";
                    node = new TreeNode(s);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafArray;
                    pos += 4;
                    propInfo = UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(prop.Name));
                    UnrealObjectInfo.ArrayType arrayType = UnrealObjectInfo.getArrayType(propInfo);
                    TreeNode node2;
                    string s2;
                    for (int i = 0; i < n; i++)
                    {
                        if (arrayType == UnrealObjectInfo.ArrayType.Struct)
                        {
                            readerpos = pos;
                            node2 = new TreeNode(i + ": (" + propInfo.reference + ")");
                            node2.Name = (-pos).ToString();
                            node2.Tag = nodeType.StructLeafStruct;
                            GenerateSpecialStruct(node2, propInfo.reference, 0);
                            node.Nodes.Add(node2);
                            pos = readerpos;
                        }
                        else
                        {
                            s2 = "";
                            PropertyReader.Type type = PropertyReader.Type.None;
                            int size = 0;
                            switch (arrayType)
                            {
                                case UnrealObjectInfo.ArrayType.Object:
                                    type = PropertyReader.Type.ObjectProperty;
                                    break;
                                case UnrealObjectInfo.ArrayType.Name:
                                    type = PropertyReader.Type.NameProperty;
                                    break;
                                case UnrealObjectInfo.ArrayType.Byte:
                                    type = PropertyReader.Type.ByteProperty;
                                    size = 1;
                                    break;
                                case UnrealObjectInfo.ArrayType.Enum:
                                    type = PropertyReader.Type.ByteProperty;
                                    break;
                                case UnrealObjectInfo.ArrayType.Bool:
                                    type = PropertyReader.Type.BoolProperty;
                                    break;
                                case UnrealObjectInfo.ArrayType.String:
                                    type = PropertyReader.Type.StrProperty;
                                    break;
                                case UnrealObjectInfo.ArrayType.Float:
                                    type = PropertyReader.Type.FloatProperty;
                                    break;
                                case UnrealObjectInfo.ArrayType.Int:
                                    type = PropertyReader.Type.IntProperty;
                                    break;
                            }
                            pos = GenerateSpecialStructProp(node, s2, pos, new PropertyReader.Property { TypeVal = type, Size = size });
                        }
                    }
                    t.Nodes.Add(node);
                    break;
                case PropertyReader.Type.StructProperty:
                    propInfo = UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(prop.Name));
                    s += propInfo.reference;
                    node = new TreeNode(s);
                    node.Name = (-pos).ToString();
                    node.Tag = nodeType.StructLeafStruct;
                    readerpos = pos;
                    GenerateSpecialStruct(node, propInfo.reference, 0);
                    pos = readerpos;
                    t.Nodes.Add(node);
                    break;
                case PropertyReader.Type.DelegateProperty:
                    throw new NotImplementedException($"at position {pos.ToString("X4")}: cannot read Delegate property of Immutable struct");
                case PropertyReader.Type.Unknown:
                    throw new NotImplementedException($"at position {pos.ToString("X4")}: cannot read Unkown property of Immutable struct");
                case PropertyReader.Type.None:
                default:
                    break;
            }

            return pos;
        }

        public TreeNode GenerateNode(PropHeader p)
        {
            string s = p.offset.ToString("X4") + ": ";
            s += "Name: \"" + pcc.getNameEntry(p.name) + "\" ";
            s += "Type: \"" + pcc.getNameEntry(p.type) + "\" ";
            s += "Size: " + p.size.ToString() + " Value: ";
            nodeType propertyType = getType(pcc.getNameEntry(p.type));
            int idx;
            byte val;
            switch (propertyType)
            {
                case nodeType.IntProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString();
                    break;
                case nodeType.ObjectProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString() + " (" + pcc.getObjectName(idx) + ")";
                    break;
                case nodeType.StrProperty:
                    int count = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"";
                    for (int i = 0; i < count * -1 - 1; i++)
                        s += (char)memory[p.offset + 28 + i * 2];
                    s += "\"";
                    break;
                case nodeType.BoolProperty:
                    val = memory[p.offset + 24];
                    s += (val == 1).ToString();
                    break;
                case nodeType.FloatProperty:
                    float f = BitConverter.ToSingle(memory, p.offset + 24);
                    s += f.ToString("0.0######");
                    break;
                case nodeType.NameProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"" + pcc.getNameEntry(idx) + "\"_" + BitConverter.ToInt32(memory, p.offset + 28);
                    break;
                case nodeType.StructProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"" + pcc.getNameEntry(idx) + "\"";
                    break;
                case nodeType.ByteProperty:
                    if (p.size == 1)
                    {
                        val = memory[p.offset + 32];
                        s += val.ToString();
                    }
                    else
                    {
                        idx = BitConverter.ToInt32(memory, p.offset + 24);
                        int idx2 = BitConverter.ToInt32(memory, p.offset + 32);
                        s += "\"" + pcc.getNameEntry(idx) + "\",\"" + pcc.getNameEntry(idx2) + "\"";
                    }
                    break;
                case nodeType.ArrayProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString() + "(count)";
                    break;
                case nodeType.StringRefProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "#" + idx.ToString() + ": ";
                    s += TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : TalkFiles.findDataById(idx);
                    break;
            }
            TreeNode ret = new TreeNode(s);
            ret.Tag = propertyType;
            ret.Name = p.offset.ToString();
            return ret;
        }

        public nodeType getType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return (nodeType)ret;
        }

        public List<PropHeader> ReadHeadersTillNone()
        {
            List<PropHeader> ret = new List<PropHeader>();
            bool run = true;
            while (run)
            {
                PropHeader p = new PropHeader();
                if (readerpos > memory.Length || readerpos < 0)
                {
                    //nothing else to interpret.
                    run = false;
                    continue;
                }
                p.name = BitConverter.ToInt32(memory, readerpos);

                if (readerpos == 4 && pcc.isName(p.name) && pcc.getNameEntry(p.name) == className)
                {
                    //It's a primitive component header
                    //Debug.WriteLine("Primitive Header " + pcc.Names[p.name]);
                    readerpos += 12;
                    continue;
                }

                if (!pcc.isName(p.name))
                    run = false;
                else
                {
                    if (pcc.getNameEntry(p.name) != "None")
                    {
                        p.type = BitConverter.ToInt32(memory, readerpos + 8);
                        if (!pcc.isName(p.type) || getType(pcc.getNameEntry(p.type)) == nodeType.Unknown)
                            run = false;
                        else
                        {
                            p.size = BitConverter.ToInt32(memory, readerpos + 16);
                            p.index = BitConverter.ToInt32(memory, readerpos + 20);
                            p.offset = readerpos;
                            ret.Add(p);
                            readerpos += p.size + 24;
                            if (getType(pcc.getNameEntry(p.type)) == nodeType.BoolProperty)//Boolbyte
                                readerpos++;
                            if (getType(pcc.getNameEntry(p.type)) == nodeType.StructProperty ||//StructName
                                getType(pcc.getNameEntry(p.type)) == nodeType.ByteProperty)//byteprop
                                readerpos += 8;
                        }
                    }
                    else
                    {
                        p.type = p.name;
                        p.size = 0;
                        p.index = 0;
                        p.offset = readerpos;
                        ret.Add(p);
                        readerpos += 8;
                        run = false;
                    }
                }
            }
            return ret;
        }

        public void PrintNodes(List<TreeNode> t, FileStream fs, int depth)
        {
            string tab = "";
            for (int i = 0; i < depth; i++)
                tab += ' ';
            foreach (TreeNode t1 in t)
            {
                string s = tab + t1.Text;
                WriteString(fs, s);
                fs.WriteByte(0xD);
                fs.WriteByte(0xA);
                if (t1.Nodes.Count != 0)
                    PrintNodes(t1.Nodes, fs, depth + 4);
            }
        }

        public void WriteString(FileStream fs, string s)
        {
            for (int i = 0; i < s.Length; i++)
                fs.WriteByte((byte)s[i]);
        }

        private string getEnclosingType(TreeNode node)
        {
            Stack<TreeNode> nodeStack = new Stack<TreeNode>();
            string typeName = className;
            string propname;
            UnrealObjectInfo.PropertyInfo p;
            while (node != null && !node.Tag.Equals(nodeType.Root))
            {
                nodeStack.Push(node);
                node = node.Parent;
            }
            bool isStruct = false;
            while (nodeStack.Count > 0)
            {
                node = nodeStack.Pop();
                if ((nodeType)node.Tag == nodeType.ArrayLeafStruct)
                {
                    continue;
                }
                propname = pcc.getNameEntry(BitConverter.ToInt32(memory, Math.Abs(Convert.ToInt32(node.Name))));
                p = UnrealObjectInfo.getPropertyInfo(typeName, propname, isStruct);
                typeName = p.reference;
                isStruct = true;
            }
            return typeName;
        }

        private bool isArrayLeaf(nodeType type)
        {
            return (type == nodeType.ArrayLeafBool || type == nodeType.ArrayLeafEnum || type == nodeType.ArrayLeafFloat ||
                type == nodeType.ArrayLeafInt || type == nodeType.ArrayLeafName || type == nodeType.ArrayLeafObject ||
                type == nodeType.ArrayLeafString || type == nodeType.ArrayLeafStruct || type == nodeType.ArrayLeafByte);
        }

        private bool isStructLeaf(nodeType type)
        {
            return (type == nodeType.StructLeafByte || type == nodeType.StructLeafDeg || type == nodeType.StructLeafFloat ||
                type == nodeType.StructLeafBool || type == nodeType.StructLeafInt || type == nodeType.StructLeafName ||
                type == nodeType.StructLeafStr || type == nodeType.StructLeafEnum || type == nodeType.StructLeafArray ||
                type == nodeType.StructLeafStruct || type == nodeType.StructLeafObject);
        }




        private T[] RemoveIndices<T>(T[] IndicesArray, int RemoveAt, int NumElementsToRemove)
        {
            if (RemoveAt < 0 || RemoveAt > IndicesArray.Length - 1 || NumElementsToRemove < 0 || NumElementsToRemove + RemoveAt > IndicesArray.Length - 1)
            {
                return IndicesArray;
            }
            T[] newIndicesArray = new T[IndicesArray.Length - NumElementsToRemove];

            int i = 0;
            int j = 0;
            while (i < IndicesArray.Length)
            {
                if (i < RemoveAt || i >= RemoveAt + NumElementsToRemove)
                {
                    newIndicesArray[j] = IndicesArray[i];
                    j++;
                }
                else
                {
                    //Debug.WriteLine("Skipping byte: " + i.ToString("X4"));
                }

                i++;
            }

            return newIndicesArray;
        }

        private void WriteMem(int pos, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];
        }

        /// <summary>
        /// Updates an array properties length and size in bytes. Does not refresh the memory view
        /// </summary>
        /// <param name="startpos">Starting index of the array property</param>
        /// <param name="countDelta">Delta in terms of how many items the array has</param>
        /// <param name="byteDelta">Delta in terms of how many bytes the array data is</param>
        private void updateArrayLength(int startpos, int countDelta, int byteDelta)
        {
            int sizeOffset = 16;
            int countOffset = 24;
            int oldSize = BitConverter.ToInt32(memory, sizeOffset + startpos);
            int oldCount = BitConverter.ToInt32(memory, countOffset + startpos);

            int newSize = oldSize + byteDelta;
            int newCount = oldCount + countDelta;

            WriteMem(startpos + sizeOffset, BitConverter.GetBytes(newSize));
            WriteMem(startpos + countOffset, BitConverter.GetBytes(newCount));

        }
    }
}