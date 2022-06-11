using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

namespace ContentPipe.Core
{
    /// <summary>
    /// Class which keeps track of content processors and applies them to input files, producing processed files in an output directory
    /// </summary>
    public class Builder
    {
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
            Dictionary<BuildProcessor, List<BuildInputFile>> pendingFiles = new Dictionary<BuildProcessor, List<BuildInputFile>>();
            Explore(Path.GetFullPath(sourceDirectory), Path.GetFullPath(outDirectory), pendingFiles);

            string sepStr = Path.DirectorySeparatorChar.ToString();
            string altSepStr = Path.AltDirectorySeparatorChar.ToString();

            if (!sourceDirectory.EndsWith(sepStr) && !sourceDirectory.EndsWith(altSepStr))
            {
                sourceDirectory += sepStr;
            }

            if (!outDirectory.EndsWith(sepStr) && !outDirectory.EndsWith(altSepStr))
            {
                outDirectory += sepStr;
            }

            var buildOptions = new BuildOptions
            {
                inputDirectory = sourceDirectory,
                outputDirectory = outDirectory,
                parallelOptions = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = threadCount
                }
            };

            int errCount = 0;

            foreach (var kvp in pendingFiles)
            {
                errCount += kvp.Key.Process(kvp.Value.ToArray(), buildOptions);
            }

            Console.WriteLine($"BUILD {(errCount == 0 ? "SUCCEEDED" : "FAILED")} - {errCount} error(s)");
            return errCount;
        }

        private void Explore(string sourceDirectory, string outDirectory, Dictionary<BuildProcessor, List<BuildInputFile>> outPendingFiles)
        {
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                var filename = Path.GetFileName(file);
                var ext = Path.GetExtension(file);

                // always skip meta files
                if (ext == ".meta") continue;

                if (_rules.TryGetValue(ext, out var processor))
                {
                    if (!outPendingFiles.ContainsKey(processor))
                    {
                        outPendingFiles.Add(processor, new List<BuildInputFile>());
                    }

                    outPendingFiles[processor].Add(new BuildInputFile
                    {
                        filepath = Path.Combine(sourceDirectory, filename),
                        metapath = File.Exists(file + ".meta") ? file + ".meta" : null,
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
