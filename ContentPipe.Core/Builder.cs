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
        private struct BuildRule
        {
            public string searchPattern;
            public BuildProcessor processor;
        }

        private List<BuildRule> _rules = new List<BuildRule>();

        /// <summary>
        /// Add a content processor for the given search pattern
        /// </summary>
        /// <param name="searchPattern">The search pattern</param>
        /// <param name="processor">The processor to register for files matching this search pattern</param>
        public void AddRule(string searchPattern, BuildProcessor processor)
        {
            _rules.Add(new BuildRule
            {
                searchPattern = searchPattern,
                processor = processor
            });
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

        internal int Run(string profile, int threadCount, string sourceDirectory, string outDirectory)
        {
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
                buildProfile = profile,
                inputDirectory = sourceDirectory,
                outputDirectory = outDirectory,
                parallelOptions = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = threadCount
                }
            };

            int errCount = 0;

            foreach (var rule in _rules)
            {
                var files = Directory.GetFiles(sourceDirectory, rule.searchPattern, SearchOption.AllDirectories);
                List<BuildInputFile> inputFiles = new List<BuildInputFile>();

                for (int i = 0; i < files.Length; i++)
                {
                    // always skip meta files
                    if (files[i].EndsWith(".meta")) continue;

                    string metapath = files[i] + ".meta";

                    inputFiles.Add(new BuildInputFile
                    {
                        filepath = files[i],
                        metapath = File.Exists(metapath) ? metapath : null 
                    });
                }

                if (inputFiles.Count == 0) continue;

                rule.processor.Process(inputFiles.ToArray(), buildOptions);
            }

            Console.WriteLine($"BUILD {(errCount == 0 ? "SUCCEEDED" : "FAILED")} - {errCount} error(s)");
            return errCount;
        }
    }
}
