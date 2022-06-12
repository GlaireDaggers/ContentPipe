using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using ContentPipe.Core;

namespace ContentPipe.Extras
{
    /// <summary>
    /// Processor which compresses input content into a GZipped file
    /// </summary>
    public class GzipProcessor : SingleAssetProcessor<GzipProcessor.GzipMetadata>
    {
        public struct GzipMetadata
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public CompressionLevel level;
        }

        protected override GzipMetadata DefaultMetadata => new GzipMetadata { level = CompressionLevel.Optimal };

        protected override string GetOutputExtension(string inFileExtension)
        {
            return inFileExtension + ".gz";
        }

        protected override void Process(BuildInputFile<GzipMetadata> inputFile, string outputPath)
        {
            using (var instream = File.OpenRead(inputFile.filepath))
            using (var outstream = File.OpenWrite(outputPath))
            using (var compressor = new GZipStream(outstream, inputFile.metadata.level))
            {
                instream.CopyTo(compressor);
            }
        }
    }
}
