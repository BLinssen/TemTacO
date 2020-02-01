using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace TemTacO
{
    class OCR
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int SnipW { get; set; }
        public int SnipH { get; set; }
        public int TemLeftX { get; set; }
        public int TemLeftY { get; set; }
        public int TemRightX { get; set; }
        public int TemRightY { get; set; }
        public string AspectRatio { get; set; }
        public string Resolution { get; set; }

        public static OCR FromCsv(string csvLine)
        {
            string[] values = csvLine.Split(',');
            OCR ocrValues = new OCR();
            ocrValues.Width = Convert.ToInt32(values[0]);
            ocrValues.Height = Convert.ToInt32(values[1]);
            ocrValues.SnipW = Convert.ToInt32(values[2]);
            ocrValues.SnipH = Convert.ToInt32(values[3]);
            ocrValues.TemLeftX = Convert.ToInt32(values[4]);
            ocrValues.TemLeftY = Convert.ToInt32(values[5]);
            ocrValues.TemRightX = Convert.ToInt32(values[6]);
            ocrValues.TemRightY = Convert.ToInt32(values[7]);
            ocrValues.AspectRatio = Convert.ToString(values[8]);
            ocrValues.Resolution = Convert.ToString(values[9]);
            return ocrValues;
        }

        public static string Tesseract(Bitmap image)
        {
            try
            {
                using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                {
                    using (var img = image)
                    {
                        using (var page = engine.Process(img))
                        {
                            var text = page.GetText();
                            //Console.WriteLine("Mean confidence: {0}", page.GetMeanConfidence());
                            //Console.WriteLine("Text (GetText): \r\n{0}", text);
                            //Console.WriteLine("Text (iterator):");
                            using (var iter = page.GetIterator())
                            {
                                iter.Begin();

                                do
                                {
                                    do
                                    {
                                        do
                                        {
                                            do
                                            {
                                                if (iter.IsAtBeginningOf(PageIteratorLevel.Block))
                                                {
                                                    //Console.WriteLine("<BLOCK>");
                                                }

                                                //Console.Write(iter.GetText(PageIteratorLevel.Word));
                                                //Console.Write(" ");

                                                if (iter.IsAtFinalOf(PageIteratorLevel.TextLine, PageIteratorLevel.Word))
                                                {
                                                    //Console.WriteLine();
                                                }
                                            } while (iter.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

                                            if (iter.IsAtFinalOf(PageIteratorLevel.Para, PageIteratorLevel.TextLine))
                                            {
                                                //Console.WriteLine();
                                            }
                                        } while (iter.Next(PageIteratorLevel.Para, PageIteratorLevel.TextLine));
                                    } while (iter.Next(PageIteratorLevel.Block, PageIteratorLevel.Para));
                                } while (iter.Next(PageIteratorLevel.Block));
                            }
                            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text); ;
                        }
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        public static bool ScanForMenu()
        {
            Bitmap memoryImage;
            memoryImage = new Bitmap(454, 3);
            System.Drawing.Size s = new System.Drawing.Size(454, 3);
            Graphics memoryGraphics = Graphics.FromImage(memoryImage);

            //Scan for menu
            memoryGraphics.CopyFromScreen(471, 298, 0, 0, s);

            for (int y = 0; y < memoryImage.Height; y++)
            {
                for (int x = 0; x < memoryImage.Width; x++)
                {
                    System.Drawing.Color pixel = memoryImage.GetPixel(x, y);
                    if (pixel.R != 30 || pixel.G != 31 || pixel.B != 30 || pixel.A != 255)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static Bitmap Whitify(Bitmap image)
        {
            Color black = Color.FromArgb(255, 0, 0, 0);
            Color white = Color.FromArgb(255, 255, 255, 255);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    System.Drawing.Color pixel = image.GetPixel(x, y);
                    if (pixel.R < 220 || pixel.G < 220 || pixel.B < 220 || pixel.A != 255)
                    {
                        image.SetPixel(x, y, white);
                    }
                    else
                    {
                        image.SetPixel(x, y, black);
                    }
                }
            }
            return image;
        }

        public static async Task<string> ImageCorrelation(Bitmap image)
        {
            List<float> similaritiesWhite = new List<float>();
            List<float> similaritiesBlack = new List<float>();
            string[] FilePaths = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Dataset"));
            foreach (string filePath in FilePaths)
            {
                float similarWhite = 0;
                float similarBlack = 0;
                Bitmap datasetImage = new Bitmap(filePath);
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        System.Drawing.Color pixel1 = image.GetPixel(x, y);
                        System.Drawing.Color pixel2 = datasetImage.GetPixel(x, y);

                        //If there is a white pixel in the same position on both images add a point of similarity
                        if ((pixel1.R == 255 && pixel1.G == 255 && pixel1.B == 255 && pixel1.A == 255) && (pixel2.R == 255 && pixel2.G == 255 && pixel2.B == 255 && pixel2.A == 255))
                        {
                            similarWhite += 1;
                        }
                        if ((pixel1.R < 35 && pixel1.G < 35 && pixel1.B < 35 && pixel1.A == 255) && (pixel2.R < 35 && pixel2.G < 35 && pixel2.B < 35 && pixel2.A == 255) && y < 26)
                        {
                            similarBlack += 1;
                        }
                    }
                }
                similaritiesWhite.Add(similarWhite);
                similaritiesBlack.Add(similarBlack);
            }
            //If there are less than 30 pixels equal we assume there is no Tem found.
            if (similaritiesWhite.Max() < 30 || similaritiesBlack.Max() < 30)
                return "";
            else
            {
                int WhiteIndex = similaritiesWhite.IndexOf(similaritiesWhite.Max());
                int BlackIndex = similaritiesBlack.IndexOf(similaritiesBlack.Max());
                if (WhiteIndex == BlackIndex)
                {
                    return Path.GetFileNameWithoutExtension(FilePaths[WhiteIndex]);
                }
                else
                    return "";
            }
        }
    }
}
