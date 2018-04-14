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
            lines.ToList().ForEach((s) => {
                rLines.Add(new HEXLine(s));
            });
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
            foreach (var item in rLines)
            {
                if (item.DataType == DataType.DataRecord)
                {
                    fs.Write(item.data, 0, item.data.Length);
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
                            DataType = DataType.DataRecord
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
                var rest = line.Address % 8;
                if (rest != 0)
                {
                    //i>0才能保证i-1不为负，否则索引越界
                    if(i>0 && lines[i-1].DataType == DataType.DataRecord )
                    {
                        if(line.Address < lines[i-1].Address + lines[i - 1].DataLength)
                        {
                            int k = rawLines.ToList().IndexOf(lines[i].RAW);
                            throw new Exception("can not process the hex file,line:" + (k == -1 ? i : k));
                        }
                        if((lines[i - 1].Address + lines[i - 1].DataLength == line.Address))
                        {
                            lines[i - 1].data = lines[i - 1].data.Concat(line.data.Take((int)rest)).ToArray();
                            lines[i - 1].DataLength += rest;
                            line.data = line.data.Skip((int)rest).ToArray();
                            line.DataLength -= rest;
                            line.Address += rest;
                        }
                        else if(line.Address > lines[i - 1].Address + lines[i - 1].DataLength)
                        {
                            //地址如果不比上一行大，是有问题的，无法处理
                            if (line.Address - rest < lines[i - 1].Address + lines[i - 1].DataLength)
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
                                lines[i - 1].data = lines[i - 1].data.Concat(line.data.Take((int)rest)).ToArray();
                                lines[i - 1].DataLength += rest;
                                line.data = line.data.Skip((int)rest).ToArray();
                                line.DataLength -= rest;
                                line.Address += rest;
                            }
                            else
                            {
                                //否则直接在本行前补FF，并退回rest数量的地址
                                line.Address -= rest;
                                line.DataLength += rest;
                                List<byte> _toFill = new List<byte>();
                                for (uint j = 0; j < rest; j++)
                                {
                                    _toFill.Add(0xff);
                                }
                                line.data = _toFill.Concat(line.data).ToArray();
                            }
                        }
                    }
                    else
                    {
                        int k = rawLines.ToList().IndexOf(lines[i].RAW);
                        throw new Exception("can not process the hex file,line:" + (k == -1 ? i : k));
                    }
                }
            }
            lines = lines.Where(s => s.DataLength != 0).ToList();

        }
    }
}
