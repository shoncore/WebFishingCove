namespace Cove.Server.Utils
{
    /// <summary>
    /// Utility class for GZIP compression and decompression.
    /// </summary>
    public static class GzipHelper
    {
        /// <summary>
        /// Decompresses a GZIP-encoded byte array into a byte array.
        /// </summary>
        /// <param name="gzipBytes">The GZIP-compressed byte array.</param>
        /// <returns>The decompressed byte array.</returns>
        public static byte[] DecompressGzip(byte[] gzipBytes)
        {
            using var compressedStream = new MemoryStream(gzipBytes);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            gzipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }

        /// <summary>
        /// Compresses a byte array into a GZIP-encoded byte array.
        /// </summary>
        /// <param name="data">The byte array to compress.</param>
        /// <returns>The GZIP-compressed byte array.</returns>
        public static byte[] CompressGzip(byte[] data)
        {
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            {
                gzipStream.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }

        /// <summary>
        /// Decompresses a GZIP-encoded byte array into a string.
        /// </summary>
        /// <param name="gzipBytes">The GZIP-compressed byte array.</param>
        /// <param name="encoding">The text encoding to use (defaults to UTF-8).</param>
        /// <returns>The decompressed string.</returns>
        public static string DecompressGzipToString(byte[] gzipBytes, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var decompressedBytes = DecompressGzip(gzipBytes);
            return encoding.GetString(decompressedBytes);
        }

        /// <summary>
        /// Compresses a string into a GZIP-encoded byte array.
        /// </summary>
        /// <param name="data">The string to compress.</param>
        /// <param name="encoding">The text encoding to use (defaults to UTF-8).</param>
        /// <returns>The GZIP-compressed byte array.</returns>
        public static byte[] CompressGzipFromString(string data, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var dataBytes = encoding.GetBytes(data);
            return CompressGzip(dataBytes);
        }

        /// <summary>
        /// Asynchronously decompresses a GZIP-encoded byte array into a byte array.
        /// </summary>
        /// <param name="gzipBytes">The GZIP-compressed byte array.</param>
        /// <returns>The decompressed byte array.</returns>
        public static async Task<byte[]> DecompressGzipAsync(byte[] gzipBytes)
        {
            await using var compressedStream = new MemoryStream(gzipBytes);
            await using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            await using var resultStream = new MemoryStream();
            await gzipStream.CopyToAsync(resultStream);
            return resultStream.ToArray();
        }

        /// <summary>
        /// Asynchronously compresses a byte array into a GZIP-encoded byte array.
        /// </summary>
        /// <param name="data">The byte array to compress.</param>
        /// <returns>The GZIP-compressed byte array.</returns>
        public static async Task<byte[]> CompressGzipAsync(byte[] data)
        {
            await using var outputStream = new MemoryStream();
            await using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            {
                await gzipStream.WriteAsync(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }
    }
}
