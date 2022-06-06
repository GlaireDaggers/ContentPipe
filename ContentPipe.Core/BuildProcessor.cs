using System;
using System.IO;
using Newtonsoft.Json;

namespace ContentPipe.Core
{
    /// <summary>
    /// Base class for a processor which operates on content files
    /// </summary>
    public abstract class BuildProcessor
    {
        public virtual string GetOutputExtension(string inFileExtension)
        {
            return inFileExtension;
        }

        public abstract void Process(string infile, string infileMetadata, string outfile);
    }

    /// <summary>
    /// Base class for a processor which operates on content files and consumes optional metadata
    /// </summary>
    /// <typeparam name="TMetadata"></typeparam>
    public abstract class BuildProcessor<TMetadata> : BuildProcessor
    {
        protected virtual TMetadata DefaultMetadata => default;

        protected virtual TMetadata DeserializeMetadata(string path)
        {
            return JsonConvert.DeserializeObject<TMetadata>(File.ReadAllText(path));
        }

        protected abstract void Process(string infile, string outfile, TMetadata meta);

        public override void Process(string infile, string infileMetadata, string outfile)
        {
            TMetadata meta = DefaultMetadata;

            if (!string.IsNullOrEmpty(infileMetadata))
            {
                meta = DeserializeMetadata(infileMetadata);
            }

            Process(infile, outfile, meta);
        }
    }

    /// <summary>
    /// Simple processor which just copies a file to the destination
    /// </summary>
    public class CopyProcessor : BuildProcessor
    {
        public override void Process(string infile, string infileMetadata, string outfile)
        {
            File.Copy(infile, outfile);
        }
    }

    /// <summary>
    /// Dummy processor which always throws an error
    /// </summary>
    public class DummyProcessor : BuildProcessor
    {
        public override void Process(string infile, string infileMetadata, string outfile)
        {
            throw new NotImplementedException();
        }
    }
}
