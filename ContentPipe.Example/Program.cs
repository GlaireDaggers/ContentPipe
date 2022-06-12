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
            //builder.AddRule("*.txt", new CopyProcessor());
            builder.AddRule("*.txt", new ArchiveProcessor("data.zip"));
            //builder.AddRule("*.png", new QoiProcessor());
            builder.AddRule("*.png", new TexturePackerProcessor("img"));
            builder.AddRule("*.json", new JsonProcessor());
            builder.AddRule("*.fx", new FNAShaderProcessor("./tool/efb.exe", null));
            builder.AddRule("*.vert", new VulkanShaderProcessor("./tool/glslangValidator.exe", null));
            builder.AddRule("*.frag", new VulkanShaderProcessor("./tool/glslangValidator.exe", null));
            builder.AddRule("*.comp", new VulkanShaderProcessor("./tool/glslangValidator.exe", null));

            return ContentPipeAPI.Build(builder);
        }
    }
}