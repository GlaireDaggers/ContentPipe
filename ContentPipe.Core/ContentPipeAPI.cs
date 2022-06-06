using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ContentPipe.Core
{
    public static class ContentPipeAPI
    {
        /// <summary>
        /// Execute the builder, using command line args to process all files in the input directory and placing them in the output directory
        /// </summary>
        /// <param name="builder">The builder to use to process content</param>
        /// <returns>0 if successful, -1 if invalid args were passed, number of failed content files otherwise</returns>
        public static int Build(Builder builder)
        {
            var argParser = new ProgramArguments();
            argParser.AddRequiredArgument("srcdir", 1, "Source directory containing files to process");
            argParser.AddRequiredArgument("dstdir", 1, "Output directory to write files to");
            argParser.AddOptionalArgument("threads", 1, "Limit number of concurrent processes (must be >0) (default is machine processor count)");
            argParser.AddOptionalArgument("clean", 0, "Clean directory before building");

            try
            {
                var argTable = argParser.ParseArguments(Environment.GetCommandLineArgs());
                string srcdir = argTable["srcdir"][0];
                string dstdir = argTable["dstdir"][0];
                int threads = Environment.ProcessorCount;

                if (argTable.TryGetValue("threads", out var threadstr) && !int.TryParse(threadstr[0], out threads) || threads <= 0)
                {
                    Console.WriteLine("INVALID USAGE: expected number >0 for threads argument");
                    argParser.PrintUsageStr();
                    return -1;
                }

                if (argTable.ContainsKey("clean"))
                {
                    Console.WriteLine("Cleaning output directory");
                    builder.Clean(dstdir);
                }

                Console.WriteLine($"Building using {threads} processes");
                return builder.Run(threads, srcdir, dstdir);
            }
            catch (ProgramArgumentException e)
            {
                Console.WriteLine($"INVALID USAGE: {e.Message} ({e.argId})");
                argParser.PrintUsageStr();
                return -1;
            }
            catch (ProgramHelpException)
            {
                argParser.PrintUsageStr();
                argParser.PrintHelpString();
                return 0;
            }
        }
    }
}
