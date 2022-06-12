using System.IO;
using System.Runtime.CompilerServices;
using System.Drawing;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using ContentPipe.Core;
using QoiSharp;
using QoiSharp.Codec;

namespace ContentPipe.Extras
{
    /// <summary>
    /// Content processor which converts source PNG files into QOI files
    /// https://github.com/phoboslab/qoi
    /// </summary>
    public class QoiProcessor : SingleAssetProcessor<QoiProcessor.QoiMetadata>
    {
        public static QoiImage BmpToQoiImage(Bitmap bmp, Channels channels, ColorSpace colorSpace)
        {
            int fmtSize = (int)channels;

            var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] srcBytes = new byte[bmp.Width * bmp.Height * 4];
            byte[] dstBytes = new byte[bmp.Width * bmp.Height * fmtSize];

            // copy bitmap data to srcBytes array
            unsafe
            {
                fixed (void* bytePtr = srcBytes)
                {
                    Unsafe.CopyBlock(bytePtr, (void*)bmpData.Scan0, (uint)srcBytes.Length);
                }
            }

            bmp.UnlockBits(bmpData);

            if (channels == Channels.Rgb)
            {
                // we need to swizzle from bgr to rgb
                int dstIdx = 0;
                for (int px = 0; px < srcBytes.Length; px += 4)
                {
                    dstBytes[dstIdx + 2] = srcBytes[px + 0]; // b
                    dstBytes[dstIdx + 1] = srcBytes[px + 1]; // g
                    dstBytes[dstIdx + 0] = srcBytes[px + 2]; // r
                    dstIdx += 3;
                }
            }
            else
            {
                // we need to swizzle from bgra to rgba
                for (int px = 0; px < dstBytes.Length; px += 4)
                {
                    dstBytes[px + 2] = srcBytes[px + 0]; // b
                    dstBytes[px + 1] = srcBytes[px + 1]; // g
                    dstBytes[px + 0] = srcBytes[px + 2]; // r
                    dstBytes[px + 3] = srcBytes[px + 3]; // a
                }
            }

            // convert into a QOI image
            return new QoiImage(dstBytes, bmp.Width, bmp.Height, channels, colorSpace);
        }

        public struct QoiMetadata
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public Channels channels;

            [JsonConverter(typeof(StringEnumConverter))]
            public ColorSpace colorSpace;
        }

        protected override QoiMetadata DefaultMetadata => new QoiMetadata { channels = Channels.RgbWithAlpha, colorSpace = ColorSpace.SRgb };

        protected override string GetOutputExtension(string inFileExtension)
        {
            return "qoi";
        }

        protected override void Process(BuildInputFile<QoiMetadata> inputFile, string outputPath, BuildOptions options)
        {
            using (var srcImage = Image.FromFile(inputFile.filepath))
            using (var bmp = new Bitmap(srcImage))
            {
                // convert into a QOI image
                QoiImage image = BmpToQoiImage(bmp, inputFile.metadata.channels, inputFile.metadata.colorSpace);

                // encode to output
                using (var outstream = File.OpenWrite(outputPath))
                {
                    byte[] data = QoiEncoder.Encode(image);
                    outstream.Write(data, 0, data.Length);
                }
            }
        }
    }
}
