﻿/*
 * C# Zlib Helper for wrapper
 *
 * Copyright (C) 2015 Pawel Kolodziejski <aquadran at users.sourceforge.net>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ZlibHelper
{
    public static class Zlib
    {
        [DllImport("zlibwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZlibDecompress([In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        [DllImport("zlibwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZlibCompress(int compressionLevel, [In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        public unsafe static uint Decompress(byte[] src, uint srcLen, byte[] dst)
        {
            uint dstLen = (uint)dst.Length;

            int status = ZlibDecompress(src, srcLen, dst, ref dstLen);
            if (status != 0)
                return 0;

            return dstLen;
        }

        public static uint Decompress(Stream inStream, uint size, byte[] destBuf)
        {
            if (size < 0)
                throw new FormatException();
            if (inStream.Position + size > inStream.Length)
                return 0;
            byte[] buffer = new byte[size];
            inStream.Read(buffer, 0, (int)size);
            return Decompress(buffer, size, destBuf);
        }

        public unsafe static byte[] Compress(byte[] src, int compressionLevel = -1)
        {
            byte[] tmpbuf = new byte[(src.Length * 2) + 128];
            uint dstLen = (uint)tmpbuf.Length;

            int status = ZlibCompress(compressionLevel, src, (uint)src.Length, tmpbuf, ref dstLen);
            if (status != 0)
                return new byte[0];

            byte[] dst = new byte[dstLen];
            Array.Copy(tmpbuf, dst, (int)dstLen);

            return dst;
        }
    }

    public static class Zip
    {
        [DllImport("zlibwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ZipOpen([In] byte[] srcBuf, ulong srcLen, ref ulong numEntries, int tpf);

        [DllImport("zlibwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZipGetCurrentFileInfo(IntPtr handle, [Out] byte[] fileName, uint sizeOfFileName, ref uint dstLen);

        [DllImport("zlibwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZipGoToFirstFile(IntPtr handle);

        [DllImport("zlibwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZipGoToNextFile(IntPtr handle);

        [DllImport("zlibwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZipLocateFile(IntPtr handle, [In] byte[] filename);

        [DllImport("zlibwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZipReadCurrentFile(IntPtr handle, [Out] byte[] dstBuf, uint dstLen, [In] byte[] password);

        [DllImport("zlibwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZipClose(IntPtr handle);

        public unsafe static IntPtr Open(byte[] srcBuf, ref ulong numEntries, int tpf)
        {
            return ZipOpen(srcBuf, (ulong)srcBuf.Length, ref numEntries, tpf);
        }

        public unsafe static int GetCurrentFileInfo(IntPtr handle, ref string fileName, ref uint dstLen)
        {
            byte[] fileN = new byte[256];
            int result = ZipGetCurrentFileInfo(handle, fileN, (uint)fileN.Length, ref dstLen);
            if (result == 0)
            {
                fileName = Encoding.ASCII.GetString(fileN).Trim('\0');
            }
            return result;
        }

        public unsafe static int GoToFirstFile(IntPtr handle)
        {
            return ZipGoToFirstFile(handle);
        }

        public unsafe static int GoToNextFile(IntPtr handle)
        {
            return ZipGoToNextFile(handle);
        }

        public unsafe static int LocateFile(IntPtr handle, string filename)
        {
            return ZipLocateFile(handle, Encoding.ASCII.GetBytes(filename + '\0'));
        }

        public unsafe static int ReadCurrentFile(IntPtr handle, byte[] dstBuf, uint dstLen, byte[] password = null)
        {
            return ZipReadCurrentFile(handle, dstBuf, dstLen, password);
        }

        public unsafe static int Close(IntPtr handle)
        {
            return ZipClose(handle);
        }
    }
}