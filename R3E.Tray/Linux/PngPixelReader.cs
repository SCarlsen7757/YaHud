#if LINUX
namespace R3E.Tray.Linux;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

/// <summary>
/// Minimal PNG decoder that extracts ARGB32 pixel data in network byte order (big-endian)
/// suitable for the D-Bus StatusNotifierItem IconPixmap property.
/// Supports 8-bit RGBA, RGB, Grayscale+Alpha, and Grayscale color types.
/// </summary>
internal static class PngPixelReader
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static (int Width, int Height, byte[] ArgbData) ReadPng(Stream pngStream)
    {
        using var ms = new MemoryStream();
        pngStream.CopyTo(ms);
        var data = ms.ToArray();

        if (data.Length < 8 || !data.AsSpan(0, 8).SequenceEqual(PngSignature))
            throw new InvalidDataException("Not a valid PNG file.");

        int width = 0, height = 0;
        byte bitDepth = 0, colorType = 0;
        var idatChunks = new List<byte[]>();

        int offset = 8;
        while (offset + 12 <= data.Length)
        {
            int chunkLength = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            var chunkType = Encoding.ASCII.GetString(data, offset + 4, 4);

            if (offset + 12 + chunkLength > data.Length)
                break;

            var chunkData = data.AsSpan(offset + 8, chunkLength);

            switch (chunkType)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(chunkData);
                    height = BinaryPrimitives.ReadInt32BigEndian(chunkData.Slice(4));
                    bitDepth = chunkData[8];
                    colorType = chunkData[9];
                    break;
                case "IDAT":
                    idatChunks.Add(chunkData.ToArray());
                    break;
                case "IEND":
                    goto doneChunks;
            }

            offset += 12 + chunkLength;
        }

        doneChunks:

        if (width == 0 || height == 0)
            throw new InvalidDataException("PNG IHDR chunk not found.");

        if (bitDepth != 8)
            throw new NotSupportedException($"Only 8-bit PNGs are supported (got {bitDepth}-bit).");

        int channels = colorType switch
        {
            0 => 1, // Grayscale
            2 => 3, // RGB
            4 => 2, // Grayscale + Alpha
            6 => 4, // RGBA
            _ => throw new NotSupportedException($"PNG color type {colorType} is not supported.")
        };

        var compressed = ConcatenateChunks(idatChunks);
        var decompressed = ZlibDecompress(compressed);
        var pixels = UnfilterScanlines(decompressed, width, height, channels);

        return (width, height, ConvertToArgb32BigEndian(pixels, width, height, channels, colorType));
    }

    private static byte[] ConcatenateChunks(List<byte[]> chunks)
    {
        int total = 0;
        foreach (var chunk in chunks)
            total += chunk.Length;

        var result = new byte[total];
        int pos = 0;
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(result, pos);
            pos += chunk.Length;
        }
        return result;
    }

    private static byte[] ZlibDecompress(byte[] compressed)
    {
        // Skip 2-byte zlib header (CMF + FLG)
        using var compStream = new MemoryStream(compressed, 2, compressed.Length - 2);
        using var deflate = new DeflateStream(compStream, CompressionMode.Decompress);
        using var outStream = new MemoryStream();
        deflate.CopyTo(outStream);
        return outStream.ToArray();
    }

    private static byte[] UnfilterScanlines(byte[] decompressed, int width, int height, int channels)
    {
        int bytesPerPixel = channels;
        int stride = width * bytesPerPixel;
        var pixels = new byte[height * stride];

        int srcOffset = 0;
        for (int y = 0; y < height; y++)
        {
            byte filterType = decompressed[srcOffset++];
            int rowStart = y * stride;
            int prevRowStart = (y - 1) * stride;

            for (int x = 0; x < stride; x++)
            {
                byte raw = decompressed[srcOffset++];
                byte a = (x >= bytesPerPixel) ? pixels[rowStart + x - bytesPerPixel] : (byte)0;
                byte b = (y > 0) ? pixels[prevRowStart + x] : (byte)0;
                byte c = (x >= bytesPerPixel && y > 0) ? pixels[prevRowStart + x - bytesPerPixel] : (byte)0;

                pixels[rowStart + x] = filterType switch
                {
                    0 => raw,
                    1 => (byte)(raw + a),
                    2 => (byte)(raw + b),
                    3 => (byte)(raw + ((a + b) >> 1)),
                    4 => (byte)(raw + PaethPredictor(a, b, c)),
                    _ => raw
                };
            }
        }

        return pixels;
    }

    private static byte[] ConvertToArgb32BigEndian(byte[] pixels, int width, int height, int channels, byte colorType)
    {
        var argb = new byte[width * height * 4];

        for (int i = 0; i < width * height; i++)
        {
            int srcPixel = i * channels;
            int dstPixel = i * 4;

            byte r, g, b, alpha;
            switch (colorType)
            {
                case 6: // RGBA
                    r = pixels[srcPixel];
                    g = pixels[srcPixel + 1];
                    b = pixels[srcPixel + 2];
                    alpha = pixels[srcPixel + 3];
                    break;
                case 2: // RGB
                    r = pixels[srcPixel];
                    g = pixels[srcPixel + 1];
                    b = pixels[srcPixel + 2];
                    alpha = 255;
                    break;
                case 4: // Grayscale + Alpha
                    r = g = b = pixels[srcPixel];
                    alpha = pixels[srcPixel + 1];
                    break;
                default: // Grayscale (0)
                    r = g = b = pixels[srcPixel];
                    alpha = 255;
                    break;
            }

            // SNI IconPixmap: ARGB32 in network byte order (big-endian)
            argb[dstPixel] = alpha;
            argb[dstPixel + 1] = r;
            argb[dstPixel + 2] = g;
            argb[dstPixel + 3] = b;
        }

        return argb;
    }

    private static byte PaethPredictor(byte a, byte b, byte c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }
}
#endif
