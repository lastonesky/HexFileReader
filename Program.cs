using System.Text;
using ECanTest.HEX;

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var hex = new[]
{
    ":020000040001F9",
    ":080002000102030405060708D2",
    ":06000A00090A0B0C0D0EAB",
    ":00000001FF"
};

var path = Path.Combine(Path.GetTempPath(), $"hexfilereader_{Guid.NewGuid():N}.hex");
File.WriteAllLines(path, hex, Encoding.ASCII);

try
{
    var lines = HEXReader.Read(path, NeedSort: true);
    Assert(lines.Length > 0, "no lines");
    Assert(lines[^1].DataType == DataType.EndOfFileRecord, "missing EOF");

    var dataLines = lines.Where(l => l.DataType == DataType.DataRecord).OrderBy(l => l.Address).ToList();
    Assert(dataLines.Count > 0, "no data records");
    Assert(dataLines[0].Address == 0x00010000, "first data record address mismatch after alignment");
    Assert(dataLines.Sum(l => (long)l.DataLength) == 16, "data length mismatch");
    Assert(dataLines[0].data.Length >= 2 && dataLines[0].data[0] == 0xFF && dataLines[0].data[1] == 0xFF, "padding mismatch");
    Console.WriteLine("OK");
}
finally
{
    try { File.Delete(path); } catch { }
}
