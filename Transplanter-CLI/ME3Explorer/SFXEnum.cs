using System;
using System.Collections.Generic;

namespace TransplanterLib
{
    class SFXEnum
    {
        public static readonly int enumCountOffset = 21;
        private byte[] data;
        List<String> names = new List<String>();
        int numItems = 0;
        private PCCObject pcc;


        public SFXEnum(PCCObject pcc, byte[] data)
        {
            this.pcc = pcc;
            this.data = data;
            numItems = BitConverter.ToInt32(data, 20);

            int i = 0;
            while (i < numItems)
            {
                int nameindex = BitConverter.ToInt32(data, i * 8 + 24);
                i++;
                names.Add(pcc.Names[nameindex]);
            }
        }


        public override string ToString()
        {
            String str = "";
            foreach (string name in names)
            {
                str += name + "\n";
            }
            return str;
        }
    }
}
