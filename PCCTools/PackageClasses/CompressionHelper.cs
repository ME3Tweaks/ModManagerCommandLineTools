﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ManagedLZO;
using System.IO;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Threading.Tasks;
using System.Windows;

namespace PCCTools.PackageClasses
{
    public static class CompressionHelper
    {
        public struct CompressedChunkBlock
        {
            public int cprSize;
            public int uncSize;
            public byte[] rawData;
        }

        public struct Chunk
        {
            public int uncompressedOffset;
            public int uncompressedSize;
            public int compressedOffset;
            public int compressedSize;
            public byte[] Compressed;
            public byte[] Uncompressed;
            public ChunkHeader header;
            public List<Block> blocks;
        }

        public struct ChunkHeader
        {
            public int magic;
            public int blocksize;
            public int compressedsize;
            public int uncompressedsize;
        }

        public struct Block
        {
            public int compressedsize;
            public int uncompressedsize;
        }

        /// <summary>
        ///     decompress an entire ME1 or 2 pcc file.
        /// </summary>
        /// <param name="raw">pcc file passed in stream format</param>
        /// <returns>a decompressed stream.</returns>
        public static MemoryStream DecompressME1orME2(Stream raw)
        {
            raw.Seek(4, SeekOrigin.Begin);
            ushort versionLo = raw.ReadValueU16();
            ushort versionHi = raw.ReadValueU16();
            raw.Seek(12, SeekOrigin.Begin);
            int tempNameSize = raw.ReadValueS32();
            raw.Seek(64 + tempNameSize, SeekOrigin.Begin);
            int tempGenerations = raw.ReadValueS32();
            raw.Seek(36 + tempGenerations * 12, SeekOrigin.Current);

            //if ME1
            if (versionLo == 491 && versionHi == 1008)
            {
                raw.Seek(4, SeekOrigin.Current);
            }

            int pos = 4;
            int NumChunks = raw.ReadValueS32();
            List<Chunk> Chunks = new List<Chunk>();

            //Console.WriteLine("Reading chunk headers...");
            for (int i = 0; i < NumChunks; i++)
            {
                Chunk c = new Chunk();
                c.uncompressedOffset = raw.ReadValueS32();
                c.uncompressedSize = raw.ReadValueS32();
                c.compressedOffset = raw.ReadValueS32();
                c.compressedSize = raw.ReadValueS32();
                c.Compressed = new byte[c.compressedSize];
                c.Uncompressed = new byte[c.uncompressedSize];
                //Console.WriteLine("Chunk " + i + ", compressed size = " + c.compressedSize + ", uncompressed size = " + c.uncompressedSize);
                //Console.WriteLine("Compressed offset = " + c.compressedOffset + ", uncompressed offset = " + c.uncompressedOffset);
                Chunks.Add(c);
            }

            //Console.WriteLine("\tRead Chunks...");
            int count = 0;
            for (int i = 0; i < Chunks.Count; i++)
            {
                Chunk c = Chunks[i];
                raw.Seek(c.compressedOffset, SeekOrigin.Begin);
                c.Compressed = raw.ReadBytes(c.compressedSize);

                ChunkHeader h = new ChunkHeader();
                h.magic = BitConverter.ToInt32(c.Compressed, 0);
                if (h.magic != -1641380927)
                    throw new FormatException("Chunk magic number incorrect");
                h.blocksize = BitConverter.ToInt32(c.Compressed, 4);
                h.compressedsize = BitConverter.ToInt32(c.Compressed, 8);
                h.uncompressedsize = BitConverter.ToInt32(c.Compressed, 12);
                //Console.WriteLine("Chunkheader read: Magic = " + h.magic + ", Blocksize = " + h.blocksize + ", Compressed Size = " + h.compressedsize + ", Uncompressed size = " + h.uncompressedsize);
                pos = 16;
                int blockCount = (h.uncompressedsize % h.blocksize == 0)
                    ?
                    h.uncompressedsize / h.blocksize
                    :
                    h.uncompressedsize / h.blocksize + 1;
                List<Block> BlockList = new List<Block>();
                //Console.WriteLine("\t\t" + count + " Read Blockheaders...");
                for (int j = 0; j < blockCount; j++)
                {
                    Block b = new Block();
                    b.compressedsize = BitConverter.ToInt32(c.Compressed, pos);
                    b.uncompressedsize = BitConverter.ToInt32(c.Compressed, pos + 4);
                    //Console.WriteLine("Block " + j + ", compressed size = " + b.compressedsize + ", uncompressed size = " + b.uncompressedsize);
                    pos += 8;
                    BlockList.Add(b);
                }
                int outpos = 0;
                //Console.WriteLine("\t\t" + count + " Read and decompress Blocks...");
                foreach (Block b in BlockList)
                {
                    byte[] datain = new byte[b.compressedsize];
                    byte[] dataout = new byte[b.uncompressedsize];
                    for (int j = 0; j < b.compressedsize; j++)
                        datain[j] = c.Compressed[pos + j];
                    pos += b.compressedsize;

                    try
                    {
                        LZO1X.Decompress(datain, dataout);
                    }
                    catch (DllNotFoundException ex)
                    {
                        //var mbResult = Console.WriteLine("Decompression failed! This may be the fault of a missing 2010 VC++ redistributable. Would you like to install this now?\n(make sure to restart ME3Explorer after installation.)",
                        //    "", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                        //f (mbResult == MessageBoxResult.Yes)
                        //{
                        //    System.Diagnostics.Process.Start("https://www.microsoft.com/en-us/download/details.aspx?id=5555");
                        //}
                        throw new Exception("LZO decompression failed - dll not found!", ex);
                    }
                    for (int j = 0; j < b.uncompressedsize; j++)
                        c.Uncompressed[outpos + j] = dataout[j];
                    outpos += b.uncompressedsize;
                }
                c.header = h;
                c.blocks = BlockList;
                count++;
                Chunks[i] = c;
            }

            MemoryStream result = new MemoryStream();
            foreach (Chunk c in Chunks)
            {
                result.Seek(c.uncompressedOffset, SeekOrigin.Begin);
                result.WriteBytes(c.Uncompressed);
            }
            
            return result;
        }
        
        #region Decompression
        /// <summary>
        ///     decompress an entire ME3 pcc file.
        /// </summary>
        /// <param name="rawData">pcc file passed in byte array format.</param>
        /// <returns>a decompressed array of bytes.</returns>
        public static byte[] DecompressME3(byte[] rawData)
        {
            using (MemoryStream input = new MemoryStream(rawData))
            {
                return DecompressME3(input).ToArray();
            }
        }

        /// <summary>
        ///     decompress an entire ME3, 2, or 1 package file.
        /// </summary>
        /// <param name="pccFileName">pcc file's name to open.</param>
        /// <returns>a decompressed array of bytes.</returns>
        public static byte[] Decompress(string pccFileName)
        {
            using (FileStream input = File.OpenRead(pccFileName))
            {
                input.Seek(4, SeekOrigin.Begin);
                ushort versionLo = input.ReadValueU16();
                ushort versionHi = input.ReadValueU16();

                //ME3
                if (versionLo == 684 && versionHi == 194)
                {
                    return DecompressME3(input).ToArray();
                }
                //ME2 || ME1
                else if (versionLo == 512 && versionHi == 130 || versionLo == 491 && versionHi == 1008)
                {
                    return DecompressME1orME2(input).ToArray();
                }
                else
                {
                    throw new FormatException("Not an ME1, ME2, or ME3 package file.");
                }
            }
        }

        /// <summary>
        ///     decompress an entire ME3 pcc file into a new stream
        /// </summary>
        /// <param name="input">pcc file passed in stream format</param>
        /// <returns>a decompressed array of bytes</returns>
        public static MemoryStream DecompressME3(Stream input)
        {
            input.Seek(0, SeekOrigin.Begin);
            var magic = input.ReadValueU32(Endian.Little);
            if (magic != 0x9E2A83C1 &&
                magic.Swap() != 0x9E2A83C1)
            {
                throw new FormatException("not a pcc file");
            }
            var endian = magic == 0x9E2A83C1 ? Endian.Little : Endian.Big;

            var versionLo = input.ReadValueU16(endian);
            var versionHi = input.ReadValueU16(endian);

            if (versionLo != 684 &&
                versionHi != 194)
            {
                throw new FormatException("unsupported pcc version");
            }

            long headerSize = 8;

            input.Seek(4, SeekOrigin.Current);
            headerSize += 4;

            var folderNameLength = input.ReadValueS32(endian);
            headerSize += 4;

            var folderNameByteLength =
                folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
            input.Seek(folderNameByteLength, SeekOrigin.Current);
            headerSize += folderNameByteLength;

            var packageFlagsOffset = input.Position;
            var packageFlags = input.ReadValueU32(endian);
            headerSize += 4;

            if ((packageFlags & 0x02000000u) == 0)
            {
                throw new FormatException("pcc file is already decompressed");
            }

            if ((packageFlags & 8) != 0)
            {
                input.Seek(4, SeekOrigin.Current);
                headerSize += 4;
            }

            uint nameCount = input.ReadValueU32(endian);
            uint nameOffset = input.ReadValueU32(endian);

            input.Seek(52, SeekOrigin.Current);
            headerSize += 60;

            var generationsCount = input.ReadValueU32(endian);
            input.Seek(generationsCount * 12, SeekOrigin.Current);
            headerSize += generationsCount * 12;

            input.Seek(20, SeekOrigin.Current);
            headerSize += 24;

            var blockCount = input.ReadValueU32(endian);
            int headBlockOff = (int)input.Position;
            var afterBlockTableOffset = headBlockOff + (blockCount * 16);
            var indataOffset = afterBlockTableOffset + 8;
            byte[] buff;

            input.Seek(0, SeekOrigin.Begin);
            MemoryStream output = new MemoryStream();
            output.Seek(0, SeekOrigin.Begin);

            output.WriteFromStream(input, headerSize);
            output.WriteValueU32(0, endian); // block count

            input.Seek(afterBlockTableOffset, SeekOrigin.Begin);
            output.WriteFromStream(input, 8);

            //check if has extra name list (don't know it's usage...)
            if ((packageFlags & 0x10000000) != 0)
            {
                long curPos = output.Position;
                output.WriteFromStream(input, nameOffset - curPos);
            }

            //decompress blocks in parallel
            Task<byte[]>[] tasks = new Task<byte[]>[blockCount];
            uint[] uncompressedOffsets = new uint[blockCount];
            for (int i = 0; i < blockCount; i++)
            {
                input.Seek(headBlockOff, SeekOrigin.Begin);
                uncompressedOffsets[i] = input.ReadValueU32(endian);
                var uncompressedSize = input.ReadValueU32(endian);
                var compressedOffset = input.ReadValueU32(endian);
                var compressedSize = input.ReadValueU32(endian);
                headBlockOff = (int)input.Position;

                buff = new byte[compressedSize];
                input.Seek(compressedOffset, SeekOrigin.Begin);
                input.Read(buff, 0, buff.Length);

                tasks[i] = ZBlock.DecompressAsync(buff);
            }
            Task.WaitAll(tasks);
            for (int i = 0; i < blockCount; i++)
            {
                output.Seek(uncompressedOffsets[i], SeekOrigin.Begin);
                output.WriteBytes(tasks[i].Result);
            }

            output.Seek(packageFlagsOffset, SeekOrigin.Begin);
            output.WriteValueU32(packageFlags & ~0x02000000u, endian);
            return output;
        }
        #endregion

        #region Compression
        /// <summary>
        ///     compress an entire ME3 pcc into a byte array.
        /// </summary>
        /// <param name="uncompressedPcc">uncompressed pcc file stored in a byte array.</param>
        /// <returns>a compressed array of bytes.</returns>
        public static byte[] Compress(byte[] uncompressedPcc)
        {
            MemoryStream uncPccStream = new MemoryStream(uncompressedPcc);
            return ((MemoryStream)Compress(uncPccStream)).ToArray();
        }

        /// <summary>
        ///     compress an entire ME3 pcc into a stream.
        /// </summary>
        /// <param name="uncompressedPcc">uncompressed pcc stream.</param>
        /// <returns>compressed pcc stream</returns>
        public static Stream Compress(Stream uncompressedPcc)
        {
            uncompressedPcc.Position = 0;

            var magic = uncompressedPcc.ReadValueU32(Endian.Little);
            if (magic != 0x9E2A83C1 &&
                magic.Swap() != 0x9E2A83C1)
            {
                throw new FormatException("not a pcc package");
            }
            var endian = magic == 0x9E2A83C1 ?
                Endian.Little : Endian.Big;
            var encoding = endian == Endian.Little ?
                Encoding.Unicode : Encoding.BigEndianUnicode;

            var versionLo = uncompressedPcc.ReadValueU16(endian);
            var versionHi = uncompressedPcc.ReadValueU16(endian);

            if (versionLo != 684 &&
                versionHi != 194)
            {
                throw new FormatException("unsupported version");
            }

            uncompressedPcc.Seek(4, SeekOrigin.Current);

            var folderNameLength = uncompressedPcc.ReadValueS32(endian);
            var folderNameByteLength =
                folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
            uncompressedPcc.Seek(folderNameByteLength, SeekOrigin.Current);

            var packageFlagsOffset = uncompressedPcc.Position;
            var packageFlags = uncompressedPcc.ReadValueU32(endian);

            if ((packageFlags & 8) != 0)
            {
                uncompressedPcc.Seek(4, SeekOrigin.Current);
            }

            var nameCount = uncompressedPcc.ReadValueU32(endian);
            var namesOffset = uncompressedPcc.ReadValueU32(endian);
            var exportCount = uncompressedPcc.ReadValueU32(endian);
            var exportInfosOffset = uncompressedPcc.ReadValueU32(endian);
            SortedDictionary<uint, uint> exportDataOffsets = new SortedDictionary<uint, uint>();

            Stream data;
            if ((packageFlags & 0x02000000) == 0)
            {
                data = uncompressedPcc;
            }
            else
            {
                throw new FormatException("pcc data is compressed");
            }

            // get info about export data, sizes and offsets
            data.Seek(exportInfosOffset, SeekOrigin.Begin);
            for (uint i = 0; i < exportCount; i++)
            {
                var classIndex = data.ReadValueS32(endian);
                data.Seek(4, SeekOrigin.Current);
                var outerIndex = data.ReadValueS32(endian);
                var objectNameIndex = data.ReadValueS32(endian);
                data.Seek(16, SeekOrigin.Current);

                uint exportDataSize = data.ReadValueU32(endian);
                uint exportDataOffset = data.ReadValueU32(endian);
                exportDataOffsets.Add(exportDataOffset, exportDataSize);

                data.Seek(4, SeekOrigin.Current);
                var count = data.ReadValueU32(endian);
                data.Seek(count * 4, SeekOrigin.Current);
                data.Seek(20, SeekOrigin.Current);
            }

            const uint maxBlockSize = 0x100000;
            Stream outputStream = new MemoryStream();
            // copying pcc header
            byte[] buffer = new byte[130];
            uncompressedPcc.Seek(0, SeekOrigin.Begin);
            uncompressedPcc.Read(buffer, 0, 130);
            outputStream.Write(buffer, 0, buffer.Length);

            //add compressed pcc flag
            uncompressedPcc.Seek(12, SeekOrigin.Begin);
            folderNameLength = uncompressedPcc.ReadValueS32();
            folderNameByteLength =
                folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
            uncompressedPcc.Seek(folderNameByteLength, SeekOrigin.Current);
            outputStream.Seek(uncompressedPcc.Position, SeekOrigin.Begin);

            packageFlags = uncompressedPcc.ReadValueU32();
            packageFlags |= 0x02000000; // add compression flag
            outputStream.WriteValueU32(packageFlags);

            outputStream.Seek(buffer.Length, SeekOrigin.Begin);

            long outOffsetData;
            long outOffsetBlockInfo;
            long inOffsetData = namesOffset;
            List<int> blockSizes = new List<int>();
            int countSize = (int)(exportDataOffsets.Min(obj => obj.Key) - namesOffset);

            //count the number of blocks and relative sizes
            uint lastOffset = exportDataOffsets.Min(obj => obj.Key);
            foreach (KeyValuePair<uint, uint> exportInfo in exportDataOffsets)
            {
                // part that adds empty spaces (leaved when editing export data and moved to the end of pcc) into the count
                if (exportInfo.Key != lastOffset)
                {
                    int emptySpace = (int)(exportInfo.Key - lastOffset);
                    if (countSize + emptySpace > maxBlockSize)
                    {
                        blockSizes.Add(countSize);
                        countSize = 0;
                    }
                    else
                        countSize += emptySpace;
                }

                // adds export data into the count
                if (countSize + exportInfo.Value > maxBlockSize)
                {
                    blockSizes.Add(countSize);
                    countSize = (int)exportInfo.Value;
                }
                else
                {
                    countSize += (int)exportInfo.Value;
                }

                lastOffset = exportInfo.Key + exportInfo.Value;
            }
            blockSizes.Add(countSize);

            outputStream.WriteValueS32(blockSizes.Count);
            outOffsetBlockInfo = outputStream.Position;
            outOffsetData = namesOffset + (blockSizes.Count * 16);

            uncompressedPcc.Seek(namesOffset, SeekOrigin.Begin);
            //divide the block in segments
            for (int i = 0; i < blockSizes.Count; i++)
            {
                int currentUncBlockSize = blockSizes[i];

                outputStream.Seek(outOffsetBlockInfo, SeekOrigin.Begin);
                outputStream.WriteValueU32((uint)uncompressedPcc.Position);
                outputStream.WriteValueS32(currentUncBlockSize);
                outputStream.WriteValueU32((uint)outOffsetData);

                byte[] inputBlock = new byte[currentUncBlockSize];
                uncompressedPcc.Read(inputBlock, 0, currentUncBlockSize);
                byte[] compressedBlock = ZBlock.Compress(inputBlock, 0, inputBlock.Length);

                outputStream.WriteValueS32(compressedBlock.Length);
                outOffsetBlockInfo = outputStream.Position;

                outputStream.Seek(outOffsetData, SeekOrigin.Begin);
                outputStream.Write(compressedBlock, 0, compressedBlock.Length);
                outOffsetData = outputStream.Position;
            }

            //copying some unknown values + extra names list
            int bufferSize = (int)namesOffset - 0x86;
            buffer = new byte[bufferSize];
            uncompressedPcc.Seek(0x86, SeekOrigin.Begin);
            uncompressedPcc.Read(buffer, 0, buffer.Length);
            outputStream.Seek(outOffsetBlockInfo, SeekOrigin.Begin);
            outputStream.Write(buffer, 0, buffer.Length);

            outputStream.Seek(0, SeekOrigin.Begin);

            return outputStream;
        }

        /// <summary>
        ///     compress an entire ME3 pcc into a file.
        /// </summary>
        /// <param name="uncompressedPcc">uncompressed pcc stream.</param>
        /// <param name="pccFileName">pcc file name to save.</param>
        /// <returns>a compressed pcc file.</returns>
        public static void CompressAndSave(Stream uncompressedPcc, string pccFileName)
        {
            using (FileStream outputStream = new FileStream(pccFileName, FileMode.Create, FileAccess.Write))
            {
                Compress(uncompressedPcc).CopyTo(outputStream);
            }
        }
        
        /// <summary>
        ///     compress an entire ME3 pcc into a file.
        /// </summary>
        /// <param name="uncompressedPcc">uncompressed pcc file stored in a byte array.</param>
        /// <param name="pccFileName">pcc file name to save.</param>
        /// <returns>a compressed pcc file.</returns>
        public static void CompressAndSave(byte[] uncompressedPcc, string pccFileName)
        {
            CompressAndSave(new MemoryStream(uncompressedPcc), pccFileName);
        }
        #endregion
    }
}
