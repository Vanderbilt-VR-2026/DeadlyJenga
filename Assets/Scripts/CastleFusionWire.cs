using System.IO;
using System.IO.Compression;

namespace DeadlyJenga.CastlePrototype
{
    public static class CastleFusionWire
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        public static byte[] Compress(byte[] source)
        {
            if (source.Length > MaxBytes) throw new InvalidDataException("Castle snapshot exceeds the transfer limit");
            using var output = new MemoryStream();
            using (var zip = new DeflateStream(output, CompressionLevel.Fastest, true)) zip.Write(source, 0, source.Length);
            return output.ToArray();
        }
        public static byte[] Decompress(byte[] source)
        {
            if (source.Length > MaxBytes) throw new InvalidDataException("Castle transfer exceeds the limit");
            using var input = new MemoryStream(source, false);
            using var zip = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = zip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + count > MaxBytes) throw new InvalidDataException("Expanded castle snapshot exceeds the limit");
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
    }
}
