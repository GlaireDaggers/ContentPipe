using System.IO;

using ContentPipe.Core;
using ContentPipe.Extras;

using FNAShaderProcessor = ContentPipe.FNA.ShaderProcessor;
using VulkanShaderProcessor = ContentPipe.Vulkan.ShaderProcessor;

namespace ContentPipe.Test
{
    internal class Program
    {
        static int Main(string[] args)
        {
            var builder = new Builder();
            
            builder.AddRule("*.txt", new CopyProcessor());

            // using custom matcher to allow QoiProcessor to skip some files which will be processed by the TexturePackerProessor
            builder.AddRule(new Matcher("*.png", "*.pack.png"), new QoiProcessor());
            builder.AddRule("*.pack.png", new TexturePackerProcessor("img"));

            builder.AddRule("*.json", new JsonProcessor());
            
            builder.AddRule("*.fx", new FNAShaderProcessor("./tool/efb.exe", null));
            builder.AddRule("*.vert", new VulkanShaderProcessor("./tool/glslangValidator.exe", null));
            builder.AddRule("*.frag", new VulkanShaderProcessor("./tool/glslangValidator.exe", null));
            builder.AddRule("*.comp", new VulkanShaderProcessor("./tool/glslangValidator.exe", null));

            // using post-processing to zip content into a set of archives in the output
            var postProcessor = new Builder();

            postProcessor.AddRule("data/*", new ArchiveProcessor("data.zip"));
            postProcessor.AddRule("shader/*", new ArchiveProcessor("shader.zip"));
            postProcessor.AddRule("*", SearchOption.TopDirectoryOnly, new ArchiveProcessor("main.zip"));

            return ContentPipeAPI.Build(builder, postProcessor);
        }
    }
}