using System;
using System.Diagnostics;

using ContentPipe.Core;

namespace ContentPipe.Vulkan
{
    /// <summary>
    /// A processor for shader files which invokes a glslangValidator or compatible executable to compile GLSL shaders into SPIR-V for Vulkan games
    /// </summary>
    public class ShaderProcessor : SingleAssetProcessor
    {
        private readonly string _glslangPath;
        private readonly string[] _includePaths;

        public ShaderProcessor(string glslangPath, string[] includePaths)
        {
            _glslangPath = glslangPath;
            _includePaths = includePaths;
        }

        protected override string GetOutputExtension(string inFileExtension)
        {
            return inFileExtension + ".spv";
        }

        protected override void Process(BuildInputFile inputFile, string outputPath, BuildOptions options)
        {
            string glslangArgs = "";

            if (_includePaths != null)
            {
                foreach (string includePath in _includePaths)
                {
                    glslangArgs += $" -I\"{includePath}\"";
                }
            }

            // invoke glslangValidator
            string cmd = $"{glslangArgs} -V {inputFile.filepath} -o {outputPath}";

            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = _glslangPath;
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
