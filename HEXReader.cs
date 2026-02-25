using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECanTest.HEX
{
    class HEXReader
    {
        public static HEXLine[] Read(string file,bool NeedSort=true)
        {
            string[] lines = File.ReadAllLines(file);
            IList<HEXLine> rLines = new List<HEXLine>();
            uint linearBase = 0;
            uint segmentBase = 0;
            bool useSegmentBase = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                if (string.IsNullOrWhiteSpace(raw)) continue;
                HEXLine parsed;
                try
                {
                    parsed = new HEXLine(raw, i + 1);
                }
                catch (Exception ex)
                {
                    throw new Exception($"hex parse error at line {i + 1}: {ex.Message}");
                }

                if (parsed.DataType == DataType.ExtendSegmentAddressRecord)
                {
                    segmentBase = (uint)((parsed.data[0] << 8) | parsed.data[1]) << 4;
                    useSegmentBase = true;
                }
                else if (parsed.DataType == DataType.ExtendedLinearAddressRecord)
                {
                    linearBase = (uint)((parsed.data[0] << 8) | parsed.data[1]) << 16;
                    useSegmentBase = false;
                }
                else if (parsed.DataType == DataType.DataRecord)
                {
                    parsed.Address = (useSegmentBase ? segmentBase : linearBase) + parsed.Address;
                }
                rLines.Add(parsed);
            }
            if (rLines.Count == 0)
            {
                throw new Exception("File is empty or have not access privilege");
            }
            var endLine = rLines.Last();
            if(endLine.DataType!= DataType.EndOfFileRecord)
            {
                throw new Exception("Hex file hasn't correctly endline");
            }
#if DEBUG
            var fs = File.Open("OriginHex.Bin", FileMode.Create);
            var dataLines = rLines.Where(s => s.DataType == DataType.DataRecord).OrderBy(s => s.Address).ToList();
            if (dataLines.Count > 0)
            {
                uint cursor = dataLines[0].Address;
                foreach (var item in dataLines)
                {
                    if (item.Address > cursor)
                    {
                        var gap = item.Address - cursor;
                        if (gap > 0)
                        {
                            var fill = Enumerable.Repeat((byte)0xFF, (int)Math.Min(gap, int.MaxValue)).ToArray();
                            fs.Write(fill, 0, fill.Length);
                            cursor += (uint)fill.Length;
                            if (cursor != item.Address) break;
                        }
                    }
                    fs.Write(item.data, 0, item.data.Length);
                    cursor = item.Address + item.DataLength;
                }
            }
            fs.Flush();
            fs.Close();
#endif
            if(NeedSort)SortLines(ref rLines,lines);
            return rLines.ToArray();
        }
        public static void SortLines(ref IList<HEXLine> lines,string[] rawLines,int blockSize=512)
        {
            var rawLineIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < rawLines.Length; i++)
            {
                var s = rawLines[i];
                if (!rawLineIndex.ContainsKey(s)) rawLineIndex.Add(s, i);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.DataType != DataType.DataRecord) continue;
                while (true)
                {
                    #region For KEA128,Align To 512 Byte
                    if (line.Address % 512 + line.DataLength > 512)
                    {
                        uint _overSize =line.Address % 512 + line.DataLength - 512;
                        var newline = new HEXLine
                        {
                            Address = line.Address + (line.DataLength - _overSize),
                            DataLength = _overSize,
                            data = line.data.Skip((int)(line.DataLength - _overSize)).Take((int)_overSize).ToArray(),
                            DataType = DataType.DataRecord,
                            RAW = "<generated>"
                        };
                        if (i + 1 >= lines.Count)
                        {
                            lines.Add(newline);
                        }
                        else
                        {
                            lines.Insert(i+1, newline);
                        }
                        line.DataLength = line.DataLength - _overSize;
                        line.data = line.data.Take((int)(line.DataLength)).ToArray();
                    }
                    #endregion
                    if (i + 1 >= lines.Count) break;
                    var nextLine = lines[i + 1];
                    if (nextLine.DataType != DataType.DataRecord) break;
                    if (line.DataLength + nextLine.DataLength > blockSize) break;
                    if (line.Address + line.DataLength != nextLine.Address) break;
                    if (line.Address % 512 + line.DataLength + nextLine.DataLength > 512) break;
                    {
                        line.data = line.data.Concat(nextLine.data).ToArray();
                        line.DataLength += nextLine.DataLength;
                        nextLine.DataType = DataType.SkipThisLine;
                        i++;
                    }
                }
            }
            lines = lines.Where(s => s.DataType != DataType.SkipThisLine).ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.DataType != DataType.DataRecord) continue;
                var misalignment = line.Address % 8;
                if (misalignment != 0)
                {
                    var bytesToMove = 8 - misalignment;
                    //i>0才能保证i-1不为负，否则索引越界
                    if(i>0 && lines[i-1].DataType == DataType.DataRecord )
                    {
                        if(line.Address < lines[i-1].Address + lines[i - 1].DataLength)
                        {
                            int k = -1;
                            if (!string.IsNullOrEmpty(lines[i].RAW) && rawLineIndex.TryGetValue(lines[i].RAW, out var idx)) k = idx;
                            throw new Exception("can not process the hex file,line:" + (k == -1 ? i : k));
                        }
                        if((lines[i - 1].Address + lines[i - 1].DataLength == line.Address))
                        {
                            var moveCount = (uint)Math.Min((long)bytesToMove, (long)line.DataLength);
                            lines[i - 1].data = lines[i - 1].data.Concat(line.data.Take((int)moveCount)).ToArray();
                            lines[i - 1].DataLength += moveCount;
                            line.data = line.data.Skip((int)moveCount).ToArray();
                            line.DataLength -= moveCount;
                            line.Address += moveCount;
                        }
                        else if(line.Address > lines[i - 1].Address + lines[i - 1].DataLength)
                        {
                            //地址如果不比上一行大，是有问题的，无法处理
                            if (line.Address >= bytesToMove && line.Address - bytesToMove < lines[i - 1].Address + lines[i - 1].DataLength)
                            {
                                //地址比上一行末尾大，并且向后退rest字节后，还落入上一行的空间内，那么再两行之间填补FF
                                List<byte> _toFill = new List<byte>();
                                uint k = line.Address - (lines[i - 1].Address + lines[i - 1].DataLength);
                                for (uint j = 0; j < k; j++)
                                {
                                    _toFill.Add(0xff);
                                }
                                lines[i - 1].DataLength += k;
                                lines[i - 1].data = lines[i - 1].data.Concat(_toFill).ToArray();
                                //在上一行补完ff后，两行数据应该能连起来，然后再开始把本行多出来的数据移到上一行
                                var moveCount = (uint)Math.Min((long)bytesToMove, (long)line.DataLength);
                                lines[i - 1].data = lines[i - 1].data.Concat(line.data.Take((int)moveCount)).ToArray();
                                lines[i - 1].DataLength += moveCount;
                                line.data = line.data.Skip((int)moveCount).ToArray();
                                line.DataLength -= moveCount;
                                line.Address += moveCount;
                            }
                            else
                            {
                                //否则直接在本行前补FF，并退回rest数量的地址
                                line.Address -= misalignment;
                                line.DataLength += misalignment;
                                List<byte> _toFill = new List<byte>();
                                for (uint j = 0; j < misalignment; j++)
                                {
                                    _toFill.Add(0xff);
                                }
                                line.data = _toFill.Concat(line.data).ToArray();
                            }
                        }
                    }
                    else
                    {
                        line.Address -= misalignment;
                        line.DataLength += misalignment;
                        List<byte> _toFill = new List<byte>();
                        for (uint j = 0; j < misalignment; j++)
                        {
                            _toFill.Add(0xff);
                        }
                        line.data = _toFill.Concat(line.data).ToArray();
                    }
                }
            }
            lines = lines.Where(s => s.DataType != DataType.DataRecord || s.DataLength != 0).ToList();

        }
    }
}
