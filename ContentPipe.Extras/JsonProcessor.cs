using System.IO;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json;

using ContentPipe.Core;

namespace ContentPipe.Extras
{
    /// <summary>
    /// Content processor which converts source JSON files into equivalent BSON files
    /// </summary>
    public class JsonProcessor : SingleAssetProcessor
    {
        protected override string GetOutputExtension(string inFileExtension)
        {
            return inFileExtension + ".b";
        }

        protected override void Process(BuildInputFile inputFile, string outputPath, BuildOptions options)
        {
            using (var reader = File.OpenText(inputFile.filepath))
            using (var jsonReader = new JsonTextReader(reader))
            using (var outstream = File.OpenWrite(outputPath))
            using (var writer = new BsonDataWriter(outstream))
            {
                writer.WriteToken(jsonReader);
            }
        }
    }
}
