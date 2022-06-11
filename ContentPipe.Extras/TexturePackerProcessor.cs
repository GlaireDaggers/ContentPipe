using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using StbRectPackSharp;
using System.Drawing;
using System.Drawing.Imaging;
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
            Image[] imgArray = new Image[batch.inputfiles.Length];

            // load all input images
            for (int i = 0; i < batch.inputfiles.Length; i++)
            {
                imgArray[i] = Image.FromFile(batch.inputfiles[i].filepath);
            }

            // pack image rects
            Packer packer = new Packer();

            for (int i = 0; i < imgArray.Length; i++)
            {
                PackerRectangle pr = packer.PackRect(imgArray[i].Width, imgArray[i].Height, i);

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
                    pr = packer.PackRect(imgArray[i].Width, imgArray[i].Height, i);
                }
            }

            // create atlas & blit images into it
            Bitmap atlasBmp = new Bitmap(packer.Width, packer.Height, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(atlasBmp);

            foreach (var packedRect in packer.PackRectangles)
            {
                Rectangle srcRect = new Rectangle(0, 0, packedRect.Width, packedRect.Height);
                Rectangle dstRect = new Rectangle(packedRect.X, packedRect.Y, packedRect.Width, packedRect.Height);
                Image img = imgArray[(int)packedRect.Data];
                g.DrawImage(img, dstRect, srcRect, GraphicsUnit.Pixel);
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
            QoiImage qoiImage = QoiProcessor.BmpToQoiImage(atlasBmp, Channels.RgbWithAlpha, ColorSpace.SRgb);

            // we don't need atlasBmp or any of the original images anymore
            atlasBmp.Dispose();

            foreach (var img in imgArray)
            {
                img.Dispose();
            }

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
