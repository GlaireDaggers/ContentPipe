using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ContentPipe.Core
{
    internal class ArgumentInfo
    {
        public string id;
        public int numArgs;
        public string help;

        public ArgumentInfo(string id, int numArgs = 0, string help = "")
        {
            this.id = id;
            this.numArgs = numArgs;
            this.help = help;
        }
    }

    internal class ProgramArgumentException : Exception
    {
        public string argId;

        public ProgramArgumentException(string argid, string message)
            : base(message)
        {
            this.argId = argid;
        }
    }

    internal class ProgramHelpException : Exception
    {
    }

    internal class ProgramArguments
    {
        private List<ArgumentInfo> _optionalArgs = new List<ArgumentInfo>();
        private List<ArgumentInfo> _requiredArgs = new List<ArgumentInfo>();
        private HashSet<string> _allArgs = new HashSet<string>();

        public ProgramArguments()
        {
        }

        public void AddOptionalArgument(string name, int numArgs = 0, string help = "")
        {
            _allArgs.Add(name);
            _optionalArgs.Add(new ArgumentInfo(name, numArgs, help));
        }

        public void AddRequiredArgument(string name, int numArgs = 1, string help = "")
        {
            _allArgs.Add(name);
            _requiredArgs.Add(new ArgumentInfo(name, numArgs, help));
        }

        public void PrintUsageStr()
        {
            string procName = Process.GetCurrentProcess().ProcessName;
            string paramStr = "";

            foreach (var arg in _requiredArgs)
            {
                if (arg.numArgs == 1)
                {
                    paramStr += $" {arg.id}";
                }
                else
                {
                    for (int i = 0; i < arg.numArgs; i++)
                    {
                        paramStr += $" {arg.id}{i + 1}";
                    }
                }
            }

            foreach (var arg in _optionalArgs)
            {
                if (arg.numArgs == 0)
                {
                    paramStr += $" [-{arg.id}]";
                }
                else
                {
                    paramStr += $" [-{arg.id}";

                    for (int i = 0; i < arg.numArgs; i++)
                    {
                        paramStr += $" v{i + 1}";
                    }

                    paramStr += "]";
                }
            }

            Console.WriteLine($"USAGE: {procName}{paramStr}");
        }

        public void PrintHelpString()
        {
            foreach (var arg in _requiredArgs)
            {
                Console.WriteLine($"{arg.id}: {arg.help}");
            }

            foreach (var arg in _optionalArgs)
            {
                Console.WriteLine($"(OPTIONAL) {arg.id}: {arg.help}");
            }
        }

        public Dictionary<string, string[]> ParseArguments(string[] args)
        {
            if (args.Length > 1 && (args[1] == "help" || args[1] == "-help"))
            {
                throw new ProgramHelpException();
            }

            Dictionary<string, string[]> result = new Dictionary<string, string[]>();

            // parse required args
            if (args.Length <= _requiredArgs.Count)
            {
                throw new ProgramArgumentException(_requiredArgs[args.Length - 1].id, "Required argument not provided");
            }

            int idx = 1;
            foreach (var arg in _requiredArgs)
            {
                string[] values = new string[arg.numArgs];

                for (int j = 0; j < arg.numArgs; j++)
                {
                    values[j] = args[idx++];
                }

                result[arg.id] = values;
            }

            // parse optional args
            for (int i = idx; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    string argName = args[i].Substring(1);

                    var argInfo = _optionalArgs.FirstOrDefault(x => x?.id == argName);

                    if (argInfo != null)
                    {
                        if (argInfo.numArgs > 0)
                        {
                            // not enough arguments
                            if (i + argInfo.numArgs >= args.Length)
                            {
                                throw new ProgramArgumentException(argName, "Not enough arguments provided");
                            }

                            // parse arguments
                            string[] values = new string[argInfo.numArgs];

                            for (int j = 0; j < argInfo.numArgs; j++)
                            {
                                values[j] = args[++i];
                            }

                            result[argName] = values;
                        }
                        else
                        {
                            result[argName] = null;
                        }
                    }
                    else
                    {
                        throw new ProgramArgumentException(argName, "Unknown argument");
                    }
                }
            }

            return result;
        }
    }
}
