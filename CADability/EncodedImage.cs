using System;
using System.IO;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// Helpers for images kept as the bytes of an image file (PNG, JPEG, GIF or BMP), as <see cref="GeoObject.Picture"/> and
    /// <see cref="GeoObject.Icon"/> keep them. The kernel never decodes the pixels: it only reads the size from the header.
    /// Decoding is the job of the front end, which on Windows uses GDI+ (issue #412).
    /// </summary>
    internal static class EncodedImage
    {
        /// <summary>
        /// The format of the image file in <paramref name="data"/>: "png", "jpeg", "gif", "bmp", or null for anything else.
        /// </summary>
        public static string FormatOf(byte[] data)
        {
            if (data == null) return null;
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 'P' && data[2] == 'N' && data[3] == 'G' && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A) return "png";
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "jpeg";
            if (data.Length >= 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8' && (data[4] == '7' || data[4] == '9') && data[5] == 'a') return "gif";
            if (data.Length >= 2 && data[0] == 'B' && data[1] == 'M') return "bmp";
            return null;
        }

        /// <summary>
        /// Reads the size in pixels from the header of a PNG, JPEG, GIF or BMP file. False for any other data, also for a
        /// damaged or truncated header.
        /// </summary>
        public static bool TryGetSize(byte[] data, out int width, out int height)
        {
            width = height = 0;
            try
            {
                switch (FormatOf(data))
                {
                    case "png": // the IHDR chunk follows the signature
                        if (data.Length < 24 || data[12] != 'I' || data[13] != 'H' || data[14] != 'D' || data[15] != 'R') return false;
                        width = BigEndian32(data, 16);
                        height = BigEndian32(data, 20);
                        break;
                    case "gif": // the logical screen descriptor
                        if (data.Length < 10) return false;
                        width = data[6] | data[7] << 8;
                        height = data[8] | data[9] << 8;
                        break;
                    case "bmp":
                        if (data.Length < 26) return false;
                        int headerSize = LittleEndian32(data, 14);
                        if (headerSize == 12)
                        {   // BITMAPCOREHEADER of OS/2 1.x
                            width = data[18] | data[19] << 8;
                            height = data[20] | data[21] << 8;
                        }
                        else
                        {
                            width = LittleEndian32(data, 18);
                            height = Math.Abs(LittleEndian32(data, 22)); // negative for a top-down bitmap
                        }
                        break;
                    case "jpeg":
                        if (!JpegSize(data, out width, out height)) return false;
                        break;
                    default:
                        return false;
                }
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
            return width > 0 && height > 0;
        }

        private static bool JpegSize(byte[] data, out int width, out int height)
        {
            width = height = 0;
            int pos = 2;
            while (pos + 4 <= data.Length)
            {
                if (data[pos] != 0xFF) return false;
                int marker = data[pos + 1];
                if (marker == 0xFF) { ++pos; continue; } // fill byte
                pos += 2;
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue; // markers without length
                if (marker == 0xD9 || marker == 0xDA) return false; // end of image or start of scan before a frame header
                int length = data[pos] << 8 | data[pos + 1];
                // SOF0 to SOF15 are the frame headers, except DHT (C4), JPG (C8) and DAC (CC)
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    height = data[pos + 3] << 8 | data[pos + 4];
                    width = data[pos + 5] << 8 | data[pos + 6];
                    return true;
                }
                pos += length;
            }
            return false;
        }
        private static int BigEndian32(byte[] data, int pos) => data[pos] << 24 | data[pos + 1] << 16 | data[pos + 2] << 8 | data[pos + 3];
        private static int LittleEndian32(byte[] data, int pos) => data[pos] | data[pos + 1] << 8 | data[pos + 2] << 16 | data[pos + 3] << 24;

        /// <summary>
        /// Checks that <paramref name="data"/> is a PNG, JPEG, GIF or BMP file whose size can be read, and throws an
        /// <see cref="ArgumentException"/> if not. Null is allowed and means "no image".
        /// </summary>
        public static void Validate(byte[] data, string parameterName)
        {
            if (data != null && !TryGetSize(data, out _, out _))
                throw new ArgumentException("The data is not a PNG, JPEG, GIF or BMP file.", parameterName);
        }

        /// <summary>
        /// The bytes of an image file read from <paramref name="path"/>. The file is closed when this method returns.
        /// </summary>
        public static byte[] ReadFile(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            Validate(data, nameof(path));
            return data;
        }

        /// <summary>
        /// The image file bytes of an image read from an old project file: the "Data" that a serialized
        /// <c>System.Drawing.Bitmap</c> contains (see <see cref="LegacyImageData"/>), or a <c>System.Drawing.Image</c> that
        /// the formatter created itself (on Windows, for files read without the binder). Null if there is no image.
        /// </summary>
        public static byte[] FromSerialized(object value)
        {
            if (value is LegacyImageData legacy) return legacy.Data;
            if (value is System.Drawing.Image image) return FromGdiImage(image);
            return null;
        }

        /// <summary>
        /// Encodes a GDI+ image (Windows only): PNG, JPEG, GIF and BMP images keep their format, everything else (e.g. a
        /// bitmap created in memory, TIFF or a metafile) becomes PNG.
        /// </summary>
        public static byte[] FromGdiImage(System.Drawing.Image image)
        {
            if (image == null) return null;
            System.Drawing.Imaging.ImageFormat format = image.RawFormat;
            if (!format.Equals(System.Drawing.Imaging.ImageFormat.Png) && !format.Equals(System.Drawing.Imaging.ImageFormat.Jpeg)
                && !format.Equals(System.Drawing.Imaging.ImageFormat.Gif) && !format.Equals(System.Drawing.Imaging.ImageFormat.Bmp))
                format = System.Drawing.Imaging.ImageFormat.Png;
            using (MemoryStream stream = new MemoryStream())
            {
                image.Save(stream, format);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Decodes the image file bytes with GDI+ (Windows only). The bitmap does not depend on a stream, so it may be kept.
        /// </summary>
        public static System.Drawing.Bitmap ToGdiBitmap(byte[] data)
        {
            if (data == null) return null;
            using (MemoryStream stream = new MemoryStream(data))
            using (System.Drawing.Bitmap decoded = new System.Drawing.Bitmap(stream))
            {
                // GDI+ needs the stream as long as a bitmap made from a stream lives; the copy does not
                return new System.Drawing.Bitmap(decoded);
            }
        }
    }

    /// <summary>
    /// Stands for a <c>System.Drawing.Bitmap</c> in a project file written by CADability 1.x: a serialized GDI+ image
    /// contains nothing but its image file bytes as "Data". Reading these into this class instead of a GDI+ bitmap makes
    /// old files with pictures and icons open on every platform (issue #412).
    /// </summary>
    [Serializable]
    internal sealed class LegacyImageData : ISerializable
    {
        public byte[] Data { get; }

        private LegacyImageData(SerializationInfo info, StreamingContext context)
        {
            Data = info.GetValue("Data", typeof(byte[])) as byte[];
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Data", Data, typeof(byte[]));
        }

        /// <summary>
        /// True for the types of GDI+ images that CADability 1.x wrote into project files.
        /// </summary>
        public static bool IsGdiImageType(string typeName)
        {
            return typeName == "System.Drawing.Bitmap" || typeName == "System.Drawing.Image";
        }
    }
}
