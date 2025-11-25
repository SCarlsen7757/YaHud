using System.Text;

namespace R3E.Extensions
{
    /// <summary>
    /// Extension methods for byte array operations.
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Converts a null-terminated UTF-8 byte array to a string.
        /// </summary>
        /// <param name="bytes">UTF-8 encoded byte array</param>
        /// <returns>Decoded string</returns>
        public static string ToNullTerminatedString(this byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            // Find the null terminator
            var endIndex = Array.IndexOf(bytes, (byte)0);
            if (endIndex < 0)
            {
                endIndex = bytes.Length;
            }

            return Encoding.UTF8.GetString(bytes, 0, endIndex);
        }
    }
}