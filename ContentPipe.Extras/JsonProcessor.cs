using System.IO;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json;

using ContentPipe.Core;

namespace ContentPipe.Extras
{
    /// <summary>
    /// Content processor which converts source JSON files into equivalent BSON files
    /// </summary>
    public class JsonProcessor : BuildProcessor
    {
        public override string GetOutputExtension(string inFileExtension)
        {
            return inFileExtension + ".b";
        }

        public override void Process(string infile, string infileMetadata, string outfile)
        {
            using (var reader = File.OpenText(infile))
            using (var jsonReader = new JsonTextReader(reader))
            using (var outstream = File.OpenWrite(outfile))
            using (var writer = new BsonDataWriter(outstream))
            {
                writer.WriteToken(jsonReader);
            }
        }
    }
}
