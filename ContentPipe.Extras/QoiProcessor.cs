using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using ContentPipe.Core;
using QoiSharp;
using QoiSharp.Codec;
using StbImageSharp;

namespace ContentPipe.Extras
{
    /// <summary>
    /// Content processor which converts source PNG files into QOI files
    /// https://github.com/phoboslab/qoi
    /// </summary>
    public class QoiProcessor : SingleAssetProcessor<QoiProcessor.QoiMetadata>
    {
        public static QoiImage ToQoiImage(ImageResult image, Channels channels, ColorSpace colorSpace)
        {
            int fmtSize = (int)channels;

            byte[] srcBytes = image.Data;
            byte[] dstBytes = new byte[image.Width * image.Height * fmtSize];

            if (channels == Channels.Rgb)
            {
                int dstIdx = 0;
                for (int px = 0; px < srcBytes.Length; px += 4)
                {
                    dstBytes[dstIdx + 0] = srcBytes[px + 0]; // r
                    dstBytes[dstIdx + 1] = srcBytes[px + 1]; // g
                    dstBytes[dstIdx + 2] = srcBytes[px + 2]; // b
                    dstIdx += 3;
                }
            }
            else
            {
                for (int px = 0; px < dstBytes.Length; px += 4)
                {
                    dstBytes[px + 0] = srcBytes[px + 0]; // r
                    dstBytes[px + 1] = srcBytes[px + 1]; // g
                    dstBytes[px + 2] = srcBytes[px + 2]; // b
                    dstBytes[px + 3] = srcBytes[px + 3]; // a
                }
            }

            // convert into a QOI image
            return new QoiImage(dstBytes, image.Width, image.Height, channels, colorSpace);
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
            using (var fs = File.OpenRead(inputFile.filepath))
            {
                var img = ImageResult.FromStream(fs);

                // convert into a QOI image
                QoiImage image = ToQoiImage(img, inputFile.metadata.channels, inputFile.metadata.colorSpace);

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
