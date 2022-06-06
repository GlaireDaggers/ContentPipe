using System;
using System.Diagnostics;

using ContentPipe.Core;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ContentPipe.FNA
{
    /// <summary>
    /// A processor for shader files which invokes an fxc or compatible executable to compile HLSL shaders into DXBC for FNA games
    /// </summary>
    public class ShaderProcessor : BuildProcessor<ShaderProcessor.ShaderMetadata>
    {
        public enum ShaderOptimizationLevel
        {
            Od,
            O0,
            O1,
            O2,
            O3,
        }

        public enum ShaderMatrixPacking
        {
            Default,
            ColumnOrder,
            RowOrder,
        }

        public struct ShaderMetadata
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public ShaderOptimizationLevel optLevel;

            [JsonConverter(typeof(StringEnumConverter))]
            public ShaderMatrixPacking matrixPacking;
        }

        private readonly string _fxcPath;
        private readonly string[] _includePaths;
        private readonly bool _disableValidation;
        private readonly bool _treatWarningsAsErrors;

        public ShaderProcessor(string fxcPath, string[] includePaths, bool disableValidation = false, bool treatWarningsAsErrors = false)
        {
            _fxcPath = fxcPath;
            _includePaths = includePaths;
            _disableValidation = disableValidation;
            _treatWarningsAsErrors = treatWarningsAsErrors;
        }

        protected override ShaderMetadata DefaultMetadata => new ShaderMetadata {
            optLevel = ShaderOptimizationLevel.O1,
            matrixPacking = ShaderMatrixPacking.ColumnOrder,
        };

        public override string GetOutputExtension(string inFileExtension)
        {
            return "fxo";
        }

        protected override void Process(string infile, string outfile, ShaderMetadata meta)
        {
            string fxcArgs = "";

            if (_includePaths != null)
            {
                foreach (string includePath in _includePaths)
                {
                    fxcArgs += $" /I \"{includePath}\"";
                }
            }

            if (_disableValidation)
            {
                fxcArgs += " /Vd";
            }

            if (_treatWarningsAsErrors)
            {
                fxcArgs += " /WX";
            }

            if (meta.matrixPacking == ShaderMatrixPacking.ColumnOrder)
            {
                fxcArgs += " /Zpc";
            }
            else if(meta.matrixPacking == ShaderMatrixPacking.RowOrder)
            {
                fxcArgs += " /Zpr";
            }

            fxcArgs += $" /{meta.optLevel}";

            // invoke FXC
            string cmd = $"{fxcArgs} {infile} {outfile}";

            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = _fxcPath;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.Arguments = cmd;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;

            process.StartInfo = startInfo;
            process.Start();

            string stdOut = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception("Shader compilation failed: " + stdOut);
            }
        }
    }
}
