using System;
using System.IO;

using ContentPipe.Core;
using ContentPipe.Extras;

namespace ContentPipe.Test
{
    internal class Program
    {
        static int Main(string[] args)
        {
            var builder = new Builder();
            builder.AddRule("txt", new CopyProcessor());
            builder.AddRule("png", new QoiProcessor());
            builder.AddRule("json", new JsonProcessor());

            return ContentPipeAPI.Build(builder);
        }
    }
}