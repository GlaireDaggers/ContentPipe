using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ContentPipe.Core
{
    /// <summary>
    /// Represents options for the current build
    /// </summary>
    public struct BuildOptions
    {
        /// <summary>
        /// The input asset directory
        /// </summary>
        public string inputDirectory;

        /// <summary>
        /// The output asset directory
        /// </summary>
        public string outputDirectory;

        /// <summary>
        /// Settings for Parallel functions
        /// </summary>
        public ParallelOptions parallelOptions;
    }

    /// <summary>
    /// Build input file data
    /// </summary>
    public struct BuildInputFile
    {
        /// <summary>
        /// The input file path
        /// </summary>
        public string filepath;

        /// <summary>
        /// The input file meta path, or null if there is no meta file
        /// </summary>
        public string metapath;
    }

    /// <summary>
    /// Build input file with JSON metadata
    /// </summary>
    /// <typeparam name="TMetadata">The metadata type</typeparam>
    public struct BuildInputFile<TMetadata>
    {
        /// <summary>
        /// The input file path
        /// </summary>
        public string filepath;

        /// <summary>
        /// The input file meta path, or null if there is no meta file
        /// </summary>
        public string metapath;

        /// <summary>
        /// The deserialized metadata
        /// </summary>
        public TMetadata metadata;
    }

    /// <summary>
    /// Base class for a processor which operates on content files
    /// </summary>
    public abstract class BuildProcessor
    {
        /// <summary>
        /// Process input files using the given build options
        /// </summary>
        /// <param name="inputFiles">The input files to process</param>
        /// <param name="options">The build options to use</param>
        /// <returns>Number of errors to report, or 0 if all succeeded</returns>
        public abstract int Process(BuildInputFile[] inputFiles, BuildOptions options);

        /// <summary>
        /// Make a path relative to another
        /// </summary>
        /// <param name="filepath">The full path</param>
        /// <param name="relativeTo">The path to make it relative to</param>
        /// <returns>The relative path</returns>
        protected static string MakeRelativePath(string filepath, string relativeTo)
        {
            Uri fullpath = new Uri(Path.GetFullPath(filepath));
            Uri relpath = new Uri(Path.GetFullPath(relativeTo));
            return relpath.MakeRelativeUri(fullpath).ToString();
        }

        /// <summary>
        /// Transform a path from input directory to output directory
        /// </summary>
        /// <param name="filepath">The file path</param>
        /// <param name="srcDir">The input directory</param>
        /// <param name="outDir">The output directory</param>
        /// <returns>The destination path for the file</returns>
        protected static string GetOutputPath(string filepath, string srcDir, string outDir)
        {
            return Path.Combine(outDir, MakeRelativePath(filepath, srcDir));
        }

        /// <summary>
        /// Check if the given file is newer than the given output file and needs rebuilding
        /// </summary>
        /// <param name="filepath">The file path</param>
        /// <param name="metapath">The file's metadata path</param>
        /// <param name="outpath">The output file to compare against</param>
        /// <returns>True if either the input file or its metadata file are newer, false otherwise</returns>
        protected static bool CheckFileDirty(string filepath, string metapath, string outpath)
        {
            bool inFileDirty = File.GetLastWriteTime(filepath) > File.GetLastWriteTime(outpath);
            bool inMetaDirty = File.Exists(metapath) && File.GetLastWriteTime(metapath) > File.GetLastWriteTime(outpath);

            return inFileDirty || inMetaDirty;
        }
    }

    /// <summary>
    /// Base class for simple processors which operate on one file at a time
    /// Will automatically skip any files which haven't changed
    /// </summary>
    public abstract class SingleAssetProcessor : BuildProcessor
    {
        /// <summary>
        /// Get the output file extension given an input file's extension
        /// </summary>
        /// <param name="inputExtension">The extension of the input file</param>
        /// <returns>The file extension of the output file</returns>
        protected virtual string GetOutputExtension(string inputExtension)
        {
            return inputExtension;
        }

        /// <summary>
        /// Process the given input file
        /// </summary>
        /// <param name="inputFile">The input file</param>
        /// <param name="outputPath">The output path to write to</param>
        protected abstract void Process(BuildInputFile inputFile, string outputPath);

        public override int Process(BuildInputFile[] inputFiles, BuildOptions options)
        {
            int errCount = 0;

            Parallel.ForEach(inputFiles, options.parallelOptions, (x) =>
            {
                string outpath = GetOutputPath(x.filepath, options.inputDirectory, options.outputDirectory);
                string outExt = GetOutputExtension(Path.GetExtension(outpath));
                outpath = Path.ChangeExtension(outpath, outExt);

                Directory.CreateDirectory(Path.GetDirectoryName(outpath));

                if (CheckFileDirty(x.filepath, x.metapath, outpath))
                {
                    try
                    {
                        Process(x, outpath);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed processing {x.filepath}: {e.Message}");
                        Interlocked.Increment(ref errCount);
                    }
                }
            });

            return errCount;
        }
    }

    /// <summary>
    /// Base class for simple processors which operate on one file at a time with metadata
    /// Will automatically skip any files which haven't changed
    /// </summary>
    public abstract class SingleAssetProcessor<TMetadata> : SingleAssetProcessor
    {
        /// <summary>
        /// Gets the default metadata to use when no metadata file is supplied
        /// </summary>
        protected virtual TMetadata DefaultMetadata => default;

        /// <summary>
        /// Deserialize the metadata from the given file
        /// </summary>
        /// <param name="path">The path to the meta file</param>
        /// <returns>The deserialized metadata</returns>
        protected virtual TMetadata DeserializeMetadata(string path)
        {
            return JsonConvert.DeserializeObject<TMetadata>(File.ReadAllText(path));
        }

        protected abstract void Process(BuildInputFile<TMetadata> inputFile, string outputPath);

        protected override void Process(BuildInputFile inputFile, string outputPath)
        {
            TMetadata meta = DefaultMetadata;

            if (!string.IsNullOrEmpty(inputFile.metapath))
            {
                meta = DeserializeMetadata(inputFile.metapath);
            }

            var inputWithMeta = new BuildInputFile<TMetadata>
            {
                filepath = inputFile.filepath,
                metapath = inputFile.metapath,
                metadata = meta
            };

            Process(inputWithMeta, outputPath);
        }
    }

    /// <summary>
    /// Base class for processors which organize input into batches of multiple files each
    /// Handles only rebuilding batches where at least one input file has changed
    /// </summary>
    public abstract class BatchAssetProcessor : BuildProcessor
    {
        /// <summary>
        /// A batch of input files which maps to an output file
        /// </summary>
        protected struct Batch
        {
            /// <summary>
            /// The input files in this batch
            /// </summary>
            public BuildInputFile[] inputfiles;

            /// <summary>
            /// The output files they map to
            /// </summary>
            public string outputfile;
        }

        /// <summary>
        /// Gather input files into a set of batches
        /// </summary>
        /// <param name="inputFiles">The set of all input files for this processor</param>
        /// <returns>A set of batches to process</returns>
        protected abstract Batch[] GatherBatches(BuildInputFile[] inputFiles, BuildOptions options);

        /// <summary>
        /// Process a batch returned from GatherBatches
        /// </summary>
        /// <param name="batch">The batch data</param>
        protected abstract void ProcessBatch(Batch batch, BuildOptions options);

        public override int Process(BuildInputFile[] inputFiles, BuildOptions options)
        {
            // TODO: what if GatherBatches throws?
            Batch[] batches = GatherBatches(inputFiles, options);

            int errCount = 0;

            Parallel.ForEach(batches, options.parallelOptions, (x) =>
            {
                bool dirty = false;

                foreach (var inputFile in x.inputfiles)
                {
                    if (CheckFileDirty(inputFile.filepath, inputFile.metapath, x.outputfile))
                    {
                        dirty = true;
                        break;
                    }
                }

                if (dirty)
                {
                    try
                    {
                        ProcessBatch(x, options);
                    }
                    catch(Exception e)
                    {
                        // TODO: should this count as one error per input file, or just one error per batch?
                        Interlocked.Increment(ref errCount);
                    }
                }
            });

            return errCount;
        }
    }

    /// <summary>
    /// Base class for processors which organize input into batches of multiple files each and consume JSON-formatted metadata
    /// Handles only rebuilding batches where at least one input file has changed
    /// </summary>
    public abstract class BatchAssetProcessor<TMetadata> : BuildProcessor
    {
        /// <summary>
        /// A batch of input files which maps to an output file
        /// </summary>
        protected struct Batch
        {
            /// <summary>
            /// The input files in this batch
            /// </summary>
            public BuildInputFile<TMetadata>[] inputfiles;

            /// <summary>
            /// The output files they map to
            /// </summary>
            public string outputfile;
        }

        /// <summary>
        /// Gets the default metadata to use when no metadata file is supplied
        /// </summary>
        protected virtual TMetadata DefaultMetadata => default;

        /// <summary>
        /// Deserialize the metadata from the given file
        /// </summary>
        /// <param name="path">The path to the meta file</param>
        /// <returns>The deserialized metadata</returns>
        protected virtual TMetadata DeserializeMetadata(string path)
        {
            return JsonConvert.DeserializeObject<TMetadata>(File.ReadAllText(path));
        }

        /// <summary>
        /// Gather input files into a set of batches
        /// </summary>
        /// <param name="inputFiles">The set of all input files for this processor</param>
        /// <returns>A set of batches to process</returns>
        protected abstract Batch[] GatherBatches(BuildInputFile<TMetadata>[] inputFiles, BuildOptions options);

        /// <summary>
        /// Process a batch returned from GatherBatches
        /// </summary>
        /// <param name="batch">The batch data</param>
        protected abstract void ProcessBatch(Batch batch, BuildOptions options);

        public override int Process(BuildInputFile[] inputFiles, BuildOptions options)
        {
            int errCount = 0;
            BuildInputFile<TMetadata>[] inputFilesWithMeta = new BuildInputFile<TMetadata>[inputFiles.Length];

            // load metadata for all input files
            Parallel.For(0, inputFiles.Length, options.parallelOptions, (i) =>
            {
                TMetadata meta = DefaultMetadata;

                if (File.Exists(inputFiles[i].metapath))
                {
                    try
                    {
                        meta = DeserializeMetadata(inputFiles[i].metapath);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine($"Failed reading {inputFiles[i].metapath}: {e.Message}");
                        Interlocked.Increment(ref errCount);
                        meta = DefaultMetadata;
                    }
                }

                inputFilesWithMeta[i] = new BuildInputFile<TMetadata>
                {
                    filepath = inputFiles[i].filepath,
                    metapath = inputFiles[i].metapath,
                    metadata = meta,
                };
            });

            // TODO: what if GatherBatches throws?
            Batch[] batches = GatherBatches(inputFilesWithMeta, options);

            Parallel.ForEach(batches, options.parallelOptions, (x) =>
            {
                bool dirty = false;

                foreach (var inputFile in x.inputfiles)
                {
                    if (CheckFileDirty(inputFile.filepath, inputFile.metapath, x.outputfile))
                    {
                        dirty = true;
                        break;
                    }
                }

                if (dirty)
                {
                    try
                    {
                        ProcessBatch(x, options);
                    }
                    catch (Exception e)
                    {
                        // TODO: should this count as one error per input file, or just one error per batch?
                        Interlocked.Increment(ref errCount);
                    }
                }
            });

            return errCount;
        }
    }

    /// <summary>
    /// Simple processor which just copies input files to the destination
    /// </summary>
    public class CopyProcessor : SingleAssetProcessor
    {
        protected override void Process(BuildInputFile inputFile, string outputPath)
        {
            File.Copy(inputFile.filepath, outputPath, true);
        }
    }

    /// <summary>
    /// Dummy processor which always throws an error
    /// </summary>
    public class DummyProcessor : BuildProcessor
    {
        public override int Process(BuildInputFile[] inputFiles, BuildOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
