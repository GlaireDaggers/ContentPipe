using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ContentPipe.Core
{
    /// <summary>
    /// Class which keeps track of content processors and applies them to input files, producing processed files in an output directory
    /// </summary>
    public class Builder
    {
        private struct PendingFile
        {
            public string inFile;
            public string inFileMeta;
            public string outFile;
            public BuildProcessor processor;
        }

        private Dictionary<string, BuildProcessor> _rules = new Dictionary<string, BuildProcessor>();

        /// <summary>
        /// Add a content processor for the given file extension
        /// </summary>
        /// <param name="extension">The file extension</param>
        /// <param name="processor">The processor to register for this extension</param>
        public void AddRule(string extension, BuildProcessor processor)
        {
            if (!extension.StartsWith(".")) extension = "." + extension;
            _rules.Add(extension, processor);
        }

        internal void Clean(string outDirectory)
        {
            if (Directory.Exists(outDirectory))
            {
                foreach (var file in Directory.GetFiles(outDirectory))
                {
                    File.Delete(file);
                }

                foreach (var dir in Directory.GetDirectories(outDirectory))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        internal int Run(int threadCount, string sourceDirectory, string outDirectory)
        {
            List<PendingFile> pendingFiles = new List<PendingFile>();
            Explore(Path.GetFullPath(sourceDirectory), Path.GetFullPath(outDirectory), pendingFiles);

            int successCount = 0;
            int errCount = 0;
            int skipCount = 0;

            Parallel.ForEach(pendingFiles, new ParallelOptions() { MaxDegreeOfParallelism = threadCount }, (file) =>
            {
                if (File.Exists(file.outFile))
                {
                    DateTime outTimestamp = File.GetLastWriteTime(file.outFile);

                    if ((!File.Exists(file.inFileMeta) || outTimestamp >= File.GetLastWriteTime(file.inFileMeta)) &&
                        outTimestamp >= File.GetLastWriteTime(file.inFile))
                    {
                        System.Threading.Interlocked.Increment(ref skipCount);
                        return;
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(file.outFile));

                try
                {
                    file.processor.Process(file.inFile, file.inFileMeta, file.outFile);
                    Console.WriteLine($"{file.inFile} -> {file.outFile}");
                    System.Threading.Interlocked.Increment(ref successCount);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR: {e.Message} ({file.inFile})");
                    System.Threading.Interlocked.Increment(ref errCount);
                }
            });

            Console.WriteLine($"Build {(errCount == 0 ? "successful" : "failed")} - {successCount} succeeded, {errCount} failed, {skipCount} up-to-date");
            return errCount;
        }

        private void Explore(string sourceDirectory, string outDirectory, List<PendingFile> outPendingFiles)
        {
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                var filename = Path.GetFileName(file);
                var ext = Path.GetExtension(file);

                // always skip meta files
                if (ext == ".meta") continue;

                if (_rules.TryGetValue(ext, out var processor))
                {
                    outPendingFiles.Add(new PendingFile
                    {
                        inFile = Path.Combine(sourceDirectory, filename),
                        inFileMeta = File.Exists(file + ".meta") ? file + ".meta" : null,
                        outFile = Path.ChangeExtension(Path.Combine(outDirectory, filename), processor.GetOutputExtension(ext)),
                        processor = processor
                    });
                }
                else
                {
                    Console.WriteLine("Unknown file extension: " + file + ", skipping");
                }
            }

            foreach (var subdir in Directory.GetDirectories(sourceDirectory))
            {
                var folderName = subdir.Substring(sourceDirectory.Length + 1);
                Explore(Path.Combine(sourceDirectory, folderName), Path.Combine(outDirectory, folderName), outPendingFiles);
            }
        }
    }
}
