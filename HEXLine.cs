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
        public string RAW { get; set; }
        
        public DataType DataType { get; set; }
        //数据长度
        public uint DataLength { get; set; }
        //校验和
        public byte checkSum { get; set; }
        public byte length { get; set; }
        public uint Address { get; set; }
        public byte[] startAddress { get; set; }
        public byte[] data { get; set; }
        public HEXLine()
        {
        }
        public HEXLine(string line)
        {
            RAW = line;
            checkSum = (byte)Convert.ToInt32(line.Substring(line.Length - 2),16);
            if (!CheckSum())
            {
                throw new Exception("checksum invalid");
            }
            DataType = (DataType)((byte)Convert.ToInt32(line.Substring(7, 2), 16));
            length = (byte)(Convert.ToInt32(line.Substring(1, 2), 16));
            DataLength = length;
            string _data = line.Substring(9, line.Length - 11);
            IList<byte> _tmp = new List<byte>();
            for (int i = 0; i < _data.Length; i+=2)
            {
                _tmp.Add((byte)(Convert.ToInt32(_data.Substring(i, 2),16)));
            }
            data = _tmp.ToArray();
            if (data.Length != length)
            {
                throw new Exception("data load length error");
            }
            startAddress = new byte[] { (byte)(Convert.ToInt32(line.Substring(3,2),16)), (byte)(Convert.ToInt32(line.Substring(5, 2), 16)) };
            Address = (uint)startAddress[0] * 256 + startAddress[1];
        }
        public bool CheckSum()
        {
            string toBeChecked = RAW.Substring(1, RAW.Length - 3);
            if (toBeChecked.Length % 2 != 0) return false;
            uint sum = 0;
            for (int i = 0; i < toBeChecked.Length; i+=2)
            {
                sum = (sum + Convert.ToUInt16(toBeChecked.Substring(i,2), 16)) % 0x100;
            }
            if ((0x100-sum)%256 == checkSum) return true;
            return false;
        }
    }
}
