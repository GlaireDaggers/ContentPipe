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
            public Matcher matcher;
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
            AddRule(new Matcher(searchPattern), processor);
        }

        /// <summary>
        /// Add a content processor for the given search pattern
        /// </summary>
        /// <param name="searchPattern">The search pattern</param>
        /// <param name="searchOption">The directory search option</param>
        /// <param name="processor">The processor to register for files matching this search pattern</param>
        public void AddRule(string searchPattern, SearchOption searchOption, BuildProcessor processor)
        {
            AddRule(new Matcher(searchPattern, null, searchOption), processor);
        }

        /// <summary>
        /// Add a content processor for the given search pattern
        /// </summary>
        /// <param name="matcher">The matcher to use to match input files</param>
        /// <param name="processor">The processor to register for files matching this search pattern</param>
        public void AddRule(Matcher matcher, BuildProcessor processor)
        {
            _rules.Add(new BuildRule
            {
                matcher = matcher,
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
                var files = rule.matcher.Match(sourceDirectory);
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

            return errCount;
        }
    }
}
