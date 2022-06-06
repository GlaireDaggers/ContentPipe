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
    public class GzipProcessor : BuildProcessor<GzipProcessor.GzipMetadata>
    {
        public struct GzipMetadata
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public CompressionLevel level;
        }

        protected override GzipMetadata DefaultMetadata => new GzipMetadata { level = CompressionLevel.Optimal };

        public override string GetOutputExtension(string inFileExtension)
        {
            return inFileExtension + ".gz";
        }

        protected override void Process(string infile, string outfile, GzipMetadata meta)
        {
            using (var instream = File.OpenRead(infile))
            using (var outstream = File.OpenWrite(outfile))
            using (var compressor = new GZipStream(outstream, meta.level))
            {
                instream.CopyTo(compressor);
            }
        }
    }
}
