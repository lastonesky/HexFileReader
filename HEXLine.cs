using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECanTest.HEX
{
    public enum DataType : byte
    {
        DataRecord = 0,
        EndOfFileRecord = 1,
        ExtendSegmentAddressRecord = 2,
        StartSegmentAddressRecord = 3,
        ExtendedLinearAddressRecord = 4,
        StartLinearAddressRecord = 5,
        SkipThisLine = 6
    };
    public class HEXLine
    {
        //原始数据
        public string RAW { get; set; } = string.Empty;
        
        public DataType DataType { get; set; }
        //数据长度
        public uint DataLength { get; set; }
        //校验和
        public byte checkSum { get; set; }
        public byte length { get; set; }
        public uint Address { get; set; }
        public byte[] startAddress { get; set; } = Array.Empty<byte>();
        public byte[] data { get; set; } = Array.Empty<byte>();
        public int SourceLineNumber { get; set; }
        public HEXLine()
        {
        }
        public HEXLine(string line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            RAW = line;
            ParseLine(line.Trim());
        }
        public HEXLine(string line, int sourceLineNumber) : this(line)
        {
            SourceLineNumber = sourceLineNumber;
        }
        private void ParseLine(string line)
        {
            if (line.Length == 0) throw new Exception("hex line is empty");
            if (line[0] != ':') throw new Exception("hex line must start with ':'");
            if (line.Length < 11) throw new Exception("hex line length is invalid");
            if (((line.Length - 1) % 2) != 0) throw new Exception("hex line length is invalid");

            length = (byte)Convert.ToInt32(line.Substring(1, 2), 16);
            DataLength = length;

            var expectedTotalLength = 11 + length * 2;
            if (line.Length != expectedTotalLength) throw new Exception("hex line length does not match data length");

            startAddress =
            [
                (byte)(Convert.ToInt32(line.Substring(3, 2), 16)),
                (byte)(Convert.ToInt32(line.Substring(5, 2), 16))
            ];
            Address = (uint)startAddress[0] * 256 + startAddress[1];

            var typeByte = (byte)Convert.ToInt32(line.Substring(7, 2), 16);
            if (typeByte <= 5)
            {
                DataType = (DataType)typeByte;
            }
            else
            {
                throw new Exception("unknown record type");
            }

            checkSum = (byte)Convert.ToInt32(line.Substring(line.Length - 2), 16);
            if (!CheckSum())
            {
                throw new Exception("checksum invalid");
            }

            if (length == 0)
            {
                data = Array.Empty<byte>();
            }
            else
            {
                string _data = line.Substring(9, line.Length - 11);
                IList<byte> _tmp = new List<byte>(length);
                for (int i = 0; i < _data.Length; i += 2)
                {
                    _tmp.Add((byte)(Convert.ToInt32(_data.Substring(i, 2), 16)));
                }
                data = _tmp.ToArray();
                if (data.Length != length)
                {
                    throw new Exception("data load length error");
                }
            }

            if (DataType == DataType.EndOfFileRecord && length != 0) throw new Exception("EOF record length must be 0");
            if (DataType == DataType.ExtendSegmentAddressRecord && length != 2) throw new Exception("type 02 record length must be 2");
            if (DataType == DataType.ExtendedLinearAddressRecord && length != 2) throw new Exception("type 04 record length must be 2");
            if (DataType == DataType.StartSegmentAddressRecord && length != 4) throw new Exception("type 03 record length must be 4");
            if (DataType == DataType.StartLinearAddressRecord && length != 4) throw new Exception("type 05 record length must be 4");
        }
        public bool CheckSum()
        {
            if (string.IsNullOrEmpty(RAW)) return false;
            var line = RAW.Trim();
            if (line.Length < 11) return false;
            if (line[0] != ':') return false;
            if (((line.Length - 1) % 2) != 0) return false;

            uint sum = 0;
            for (int i = 1; i < line.Length - 2; i += 2)
            {
                sum = (sum + Convert.ToUInt16(line.Substring(i, 2), 16)) & 0xFF;
            }
            var expected = (byte)((0x100 - sum) & 0xFF);
            return expected == checkSum;
        }
    }
}
