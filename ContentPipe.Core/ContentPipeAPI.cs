using System;
using System.IO;

namespace ContentPipe.Core
{
    public static class ContentPipeAPI
    {
        /// <summary>
        /// Execute the builder, using command line args to process all files in the input directory and placing them in the output directory
        /// </summary>
        /// <param name="builder">The builder to use to process content into an intermediate directory</param>
        /// <param name="postProcessor">The builder to use for post-processing intermediate content into the output directory</param>
        /// <returns>0 if successful, -1 if invalid args were passed, number of failed content files otherwise</returns>
        public static int Build(Builder builder, Builder postProcessor = null)
        {
            var argParser = new ProgramArguments();
            argParser.AddRequiredArgument("srcdir", 1, "Source directory containing files to process");
            argParser.AddRequiredArgument("dstdir", 1, "Output directory to write files to");
            argParser.AddOptionalArgument("profile", 1, "Set build profile name (default is \"Default\")");
            argParser.AddOptionalArgument("threads", 1, "Limit number of concurrent processes (must be >0) (default is machine processor count)");
            argParser.AddOptionalArgument("clean", 0, "Clean directory before building");
            argParser.AddOptionalArgument("idir", 1, "Override intermediate build directory (default is \"intermediate\" folder in same directory as srcdir)");

            try
            {
                var argTable = argParser.ParseArguments(Environment.GetCommandLineArgs());
                string srcdir = Path.GetFullPath(argTable["srcdir"][0]);
                string dstdir = Path.GetFullPath(argTable["dstdir"][0]);
                int threads = Environment.ProcessorCount;
                string buildProfile = "Default";
                string intermediateDir = Path.Combine(Directory.GetParent(srcdir.TrimEnd('/', '\\')).FullName, "intermediate");

                if (argTable.TryGetValue("idir", out var objdir))
                {
                    intermediateDir = argTable["idir"][0];
                }

                srcdir = BuildProcessor.NormalizeDirectoryString(srcdir);
                dstdir = BuildProcessor.NormalizeDirectoryString(dstdir);
                intermediateDir = BuildProcessor.NormalizeDirectoryString(intermediateDir);

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

                if (argTable.TryGetValue("profile", out var profilestr))
                {
                    buildProfile = profilestr[0];
                }

                Console.WriteLine($"Building {buildProfile} using {threads} processes");

                int errCount = builder.Run(buildProfile, threads, srcdir, intermediateDir);

                if (errCount != 0)
                {
                    PrintBuildResult(errCount);
                    return errCount;
                }

                // now run post-processor (or just copy files if none is provided)
                if (postProcessor != null)
                {
                    errCount = postProcessor.Run(buildProfile, threads, intermediateDir, dstdir);
                    PrintBuildResult(errCount);
                    return errCount;
                }
                else
                {
                    var files = Directory.GetFiles(intermediateDir, "*", SearchOption.AllDirectories);

                    foreach (var f in files)
                    {
                        string outpath = BuildProcessor.GetOutputPath(f, intermediateDir, dstdir);

                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(outpath));
                            File.Copy(f, outpath);
                        }
                        catch(IOException e)
                        {
                            Console.WriteLine($"Failed copying {f} to destination: {e.Message}");
                            errCount++;
                        }
                    }

                    PrintBuildResult(errCount);
                    return errCount;
                }
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

        private static void PrintBuildResult(int errCount)
        {
            Console.WriteLine($"BUILD {(errCount == 0 ? "SUCCEEDED" : "FAILED")} - {errCount} error(s)");
        }
    }
}
