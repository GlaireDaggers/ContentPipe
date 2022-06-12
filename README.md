# ContentPipe

A simple framework for automating content pipelines for indie games written in C# / .NET Framework 4.7

ContentPipe aims to provide an extensible basis for automating content pipelines in indie games built on minimal frameworks such as FNA, MoonWorks, Veldrid, etc. in a way that doesn't make too many assumptions about how a game may structure its content, while still providing helpful utilities such as command line arguments, parallel build support, support for clean rebuilds & skipping up to date files, and a handful of built-in content processors.

## Usage

The ContentPipe.Core API is meant to be embedded in your own .NET Framework 4.7 console application. The intended workflow is to call it from your Program.Main function like this:

```csharp
static int Main(string[] args)
{
    var builder = new Builder();

    // add your game's content processors to the builder here
    builder.AddRule("*.txt", new CopyProcessor());

    // run the build, returning an error code or 0
    return ContentPipeAPI.Build(builder);
}
```

## Commandline arguments

ContentPipeAPI.Build will parse command line arguments on your behalf. At a minimum, it expects input path and output path to be specified like so: `ContentPipe.Example.exe [src path here] [dest path here]`. It will then recursively iterate all files in the source directory, match them with associated BuildProcessors, and write the output files into the given output directory with the same folder structure

A couple of optional arguments may also be specified after the required arguments. These are:

* -threads [n]
  * Specifies how many simultaneous files may be processed at once. By default, ContentPipe will use the environment processor count, but can be limited using this parameter.
* -clean
  * Causes ContentPipe to completely clear the output directory before building. This is useful for performing a clean rebuild of all content.

## Builder and BuildProcessor

The general architecture of ContentPipe is a Builder which maintains a collection of BuildProcessors. A BuildProcessor is responsible for taking an array of input files and writing processed files to the output directory.

Each input file passed to the BuildProcessor may also have an associated *metadata* file, which can be used to customize the behavior of the BuildProcessor for that file (for example, a texture processor may use the metadata file to specify things like compression formats to use). The metadata file should sit in the same directory as the file it's for, with the same name as the file (including extension) with ".meta" added to it. For example, the metadata file for a file named "image.png" would be "image.png.meta"

A handful of built-in BuildProcessors are included in the ContentPipe.Extras project:

* CopyProcessor simply copies each file as-is to the destination
* JsonProcessor takes input JSON files and re-serializes them as a BSON file in the destination
* GzipProcessor compresses each input file into a gzipped file in the destination
* QoiProcessor takes each input image file and re-encodes it as a [QOI image](https://github.com/phoboslab/qoi) in the destination. A JSON-formatted metadata file may be provided to specify color channels & color space (see ContentPipe.Examples for a demonstration)
* ArchiveProcessor takes each input file and appends it to a ZIP archive in the destination
* TexturePackerProcessor takes each input image file and packs them into textue atlases. A JSON-formatted metadata file may be used to specify how images are grouped into multiple atlases (see ContentPipe.Examples for a demonstration)

There are also a couple of extra processors aimed at specific frameworks:

* ContentPipe.FNA.ShaderProcessor takes each input HLSL file and compiles it into DXBC using FXC or an equivalent executable (the example includes an [Effect-Build](https://github.com/GlaireDaggers/Effect-Build/) binary), targeted at games based on the [FNA](https://github.com/FNA-XNA/FNA) framework. A JSON-formatted metadata file may be used to set the optimization level and change the matrix packing order (see ContentPipe.Examples for a demonstration)
* ContentPipe.Vulkan.ShaderProcessor takes each input GLSL file and compiles it into SPIR-V using glslangValidator or an equivalent executable, targeted at games running on Vulkan (for example, games built on [MoonWorks](https://gitea.moonside.games/MoonsideGames/MoonWorks.git))

These can be used by your game's content pipeline and can also serve as examples for how to write your own content processors.

In general, writing a custom processor usually looks like this:

```csharp
public class MyContentProcessor : SingleAssetProcessor
{
  // Allows your content processor to return what the output file extension should be for a given input file's extension
  protected override string GetOutputExtension(string inFileExtension)
  {
    return "myext";
  }

  // Allows your content processor to take an input file, and write the final content to the given output file path
  public override void Process(BuildInputFile inputFile, string outFilePath)
  {
    // write your custom file processing logic here
  }
}
```

To simplify metadata handling, you may also use the generic version of the build processor class which uses Newtonsoft.JSON to deserialize a metadata object:

```csharp
public class MyContentProcessorWithMetadata : SingleAssetProcessor<MyContentProcessorWithMetadata.Data>
{
  public struct Data
  {
    public int x;
  }

  // here you return what the default metadata should be if no metadata file is provided
  public override Data DefaultMetadata => new Data { x = 0 };

  // Allows your content processor to return what the output file extension should be for a given input file's extension
  protected override string GetOutputExtension(string inFileExtension)
  {
    return "myext";
  }

  // Allows your content processor to take an input file path & a metadata object, and write the final content to the given output file path
  public override void Process(BuildInputFile<Data> inputFile, string outFilePath)
  {
    // write your custom file processing logic here
  }
}
```

## QoiSharp

The included QoiProcessor class makes use of [QoiSharp](https://github.com/NUlliiON/QoiSharp), a C#/.NET library for handling QOI images. The code has been modified to support .NET Framework 4.7 and can be found in the QoiSharp folder
