using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

using ContentPipe.Core;

namespace ContentPipe.Extras
{
    /// <summary>
    /// Example asset processor which appends all input files to an output ZIP
    /// </summary>
    public class ArchiveProcessor : BatchAssetProcessor
    {
        private readonly string _outputZip;

        public ArchiveProcessor(string outputZip)
        {
            _outputZip = outputZip;
        }

        protected override Batch[] GatherBatches(BuildInputFile[] inputFiles, BuildOptions options)
        {
            return new Batch[]
            {
                new Batch() { inputfiles = inputFiles, outputfile = Path.Combine(options.outputDirectory, _outputZip) }
            };
        }

        protected override void ProcessBatch(Batch batch, BuildOptions options)
        {
            using (ZipArchive archive = ZipFile.Open(batch.outputfile, ZipArchiveMode.Create))
            {
                foreach (var inputfile in batch.inputfiles)
                {
                    archive.CreateEntryFromFile(inputfile.filepath, MakeRelativePath(inputfile.filepath, options.inputDirectory));
                }
            }
        }
    }
}
