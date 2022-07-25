using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using StbRectPackSharp;
using StbImageSharp;
using QoiSharp;
using QoiSharp.Codec;
using Newtonsoft.Json;

using ContentPipe.Core;

namespace ContentPipe.Extras
{
    public class TexturePackerProcessor : BatchAssetProcessor<TexturePackerProcessor.Metadata>
    {
        public struct TextureSheetRect
        {
            public int x;
            public int y;
            public int width;
            public int height;
        }

        public struct TextureSheetData
        {
            public Dictionary<string, TextureSheetRect> images;
        }

        public struct Metadata
        {
            public string textureSheetId;
        }

        private struct Color32
        {
            public byte R;
            public byte G;
            public byte B;
            public byte A;
        }

        private class AtlasImage
        {
            public Color32[] pixels;
            public int width;
            public int height;

            public AtlasImage(int width, int height)
            {
                this.width = width;
                this.height = height;
                pixels = new Color32[width * height];
            }

            public AtlasImage(ImageResult image) : this(image.Width, image.Height)
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    int srcIdx = i * 4;
                    pixels[i] = new Color32
                    {
                        R = image.Data[srcIdx],
                        G = image.Data[srcIdx + 1],
                        B = image.Data[srcIdx + 2],
                        A = image.Data[srcIdx + 3],
                    };
                }
            }

            public void Blit(AtlasImage image, int x, int y)
            {
                for (int scan = 0; scan < image.height; scan++)
                {
                    Array.Copy(image.pixels, scan * image.width, pixels, (scan * width) + x, image.width);
                }
            }

            public ImageResult ToImageResult()
            {
                ImageResult result = new ImageResult();
                result.Width = width;
                result.Height = height;
                result.SourceComp = ColorComponents.RedGreenBlueAlpha;
                result.Comp = ColorComponents.RedGreenBlueAlpha;
                result.Data = new byte[width * height * 4];

                for (int i = 0; i < pixels.Length; i++)
                {
                    int dstIdx = i * 4;
                    result.Data[dstIdx++] = pixels[i].R;
                    result.Data[dstIdx++] = pixels[i].G;
                    result.Data[dstIdx++] = pixels[i].B;
                    result.Data[dstIdx++] = pixels[i].A;
                }

                return result;
            }
        }

        protected override Metadata DefaultMetadata => new Metadata { textureSheetId = "default" };

        protected readonly string _outputPath;

        public TexturePackerProcessor(string outputPath)
        {
            _outputPath = outputPath;
        }

        protected override Batch[] GatherBatches(BuildInputFile<Metadata>[] inputFiles, BuildOptions options)
        {
            Dictionary<string, List<BuildInputFile<Metadata>>> sheets = new Dictionary<string, List<BuildInputFile<Metadata>>>();

            // sort input files by texture sheet ID
            foreach (var inputfile in inputFiles)
            {
                if (!sheets.ContainsKey(inputfile.metadata.textureSheetId))
                {
                    sheets.Add(inputfile.metadata.textureSheetId, new List<BuildInputFile<Metadata>>());
                }

                sheets[inputfile.metadata.textureSheetId].Add(inputfile);
            }

            List<Batch> batches = new List<Batch>();

            foreach (var kvp in sheets)
            {
                batches.Add(new Batch()
                {
                    inputfiles = kvp.Value.ToArray(),
                    outputfile = Path.Combine(options.outputDirectory, $"{kvp.Key}.qoi")
                });
            }

            return batches.ToArray();
        }

        protected override void ProcessBatch(Batch batch, BuildOptions options)
        {
            AtlasImage[] imgArray = new AtlasImage[batch.inputfiles.Length];

            // load all input images
            for (int i = 0; i < batch.inputfiles.Length; i++)
            {
                using (var fs = File.OpenRead(batch.inputfiles[i].filepath))
                {
                    imgArray[i] = new AtlasImage(ImageResult.FromStream(fs, ColorComponents.RedGreenBlueAlpha));
                }
            }

            // pack image rects
            Packer packer = new Packer();

            for (int i = 0; i < imgArray.Length; i++)
            {
                PackerRectangle pr = packer.PackRect(imgArray[i].width, imgArray[i].height, i);

                while (pr == null)
                {
                    // TODO: if we exceed maximum size, we need to give up

                    // ran out of room, try resizing
                    Packer newPacker = new Packer(packer.Width * 2, packer.Height * 2);

                    // Place existing rectangles
                    foreach (PackerRectangle existingRect in packer.PackRectangles)
                    {
                        newPacker.PackRect(existingRect.Width, existingRect.Height, existingRect.Data);
                    }

                    // Now dispose old packer and assign new one
                    packer.Dispose();
                    packer = newPacker;

                    // try again
                    pr = packer.PackRect(imgArray[i].width, imgArray[i].height, i);
                }
            }

            // create atlas & blit images into it
            AtlasImage atlas = new AtlasImage(packer.Width, packer.Height);

            foreach (var packedRect in packer.PackRectangles)
            {
                AtlasImage img = imgArray[(int)packedRect.Data];
                atlas.Blit(img, packedRect.X, packedRect.Y);
            }

            // also build atlas data
            TextureSheetData sheetData = new TextureSheetData() { images = new Dictionary<string, TextureSheetRect>() };

            foreach (var packedRect in packer.PackRectangles)
            {
                TextureSheetRect dstRect = new TextureSheetRect { x = packedRect.X, y = packedRect.Y, width = packedRect.Width, height = packedRect.Height };
                var inputFile = batch.inputfiles[(int)packedRect.Data];
                var fileid = MakeRelativePath(inputFile.filepath, options.inputDirectory);
                sheetData.images.Add(fileid, dstRect);
            }

            // now convert into QoiImage
            QoiImage qoiImage = QoiProcessor.ToQoiImage(atlas.ToImageResult(), Channels.RgbWithAlpha, ColorSpace.SRgb);

            // encode to output
            using (var outstream = File.OpenWrite(batch.outputfile))
            {
                byte[] data = QoiEncoder.Encode(qoiImage);
                outstream.Write(data, 0, data.Length);
            }

            // also write atlas data
            File.WriteAllText(batch.outputfile + ".json", JsonConvert.SerializeObject(sheetData));
        }
    }
}
