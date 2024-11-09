using System.IO.Compression;

public class GzipHelper
{
    // Function to decompress a GZIP-encoded byte array to a string
    public static byte[] DecompressGzip(byte[] gzipBytes)
    {
        using (var compressedStream = new MemoryStream(gzipBytes))
        using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
        using (var resultStream = new MemoryStream())
        {
            // Copy the decompressed data into the result stream
            gzipStream.CopyTo(resultStream);
            return resultStream.ToArray(); // Convert the MemoryStream to a byte array
        }
    }

    // Function to compress a string into a GZIP-encoded byte array
    public static byte[] CompressGzip(byte[] data)
    {
        using (var outputStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            {
                gzipStream.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }
    }
}
