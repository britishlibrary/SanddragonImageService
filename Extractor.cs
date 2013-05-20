/*
* Copyright (c) 2012, The British Library Board
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
* Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
* Neither the name of The British Library nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Drawing.Imaging;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace SanddragonImageService
{
    public class Extractor
    {
        Object lockObject = new object();

        public Extractor()
        { }

        public class Metadata
        {
            public int Width { set; get; }
            public int Height { set; get; }
            public int Jp2levels { set; get; }
            public int TileWidth { set; get; }
            public int TileHeight { set; get; }
        }

        [Serializable]
        public class Error
        {
            public string Code { set; get; }
            public string Message { set; get; }
        }

        private string getErrorXml(Error p_error)
        {
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");

            XmlSerializer xs = new XmlSerializer(p_error.GetType());
            using (MemoryStream memoryStream = new MemoryStream())
            {
                XmlWriterSettings settings = new XmlWriterSettings()
                {
                    Encoding = Encoding.ASCII
                };
                using (XmlWriter writer = XmlWriter.Create(memoryStream, settings))
                {
                    xs.Serialize(writer, p_error, ns);
                }
                return Encoding.ASCII.GetString(memoryStream.ToArray());
            }
        }

        public string GetMetadata(string p_filename)
        {
            string fname = ConfigurationManager.AppSettings["JP2CachePath"] + p_filename;

            if (!File.Exists(fname))
            {
                throw new Exception(getErrorXml(new Error { Code = "i3f_200", Message = p_filename + " does not exist" }));
            }

            return GetJP2MetadataString(fname);
        }

        private string GetJP2MetadataString(string fname)
        {
            Metadata data = GetJP2Metadata(fname);

            System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(data.GetType());
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            System.IO.StringWriter writer = new System.IO.StringWriter(sb);
            ser.Serialize(writer, data);

            return sb.ToString();
        }

        private Metadata GetJP2Metadata(string p_filename)
        {
            if (!File.Exists(p_filename + ".log"))
            {
                ProcessStartInfo start = new ProcessStartInfo();

                start.FileName = ConfigurationManager.AppSettings["ExpanderPath"];
                start.UseShellExecute = false;
                start.RedirectStandardOutput = false;
                start.RedirectStandardError = false;
                start.CreateNoWindow = true;

                start.Arguments = "-i \"" + ConfigurationManager.AppSettings["JP2CachePath"] + Path.GetFileName(p_filename) + "\" -record \"" + p_filename + ".log\"";

                Process proc = Process.Start(start);
                proc.WaitForExit();
            }

            byte[] cOutput = new byte[500];

            FileStream fs = File.OpenRead(p_filename + ".log");
            fs.Read(cOutput, 0, cOutput.Length);
            cOutput = Encoding.Convert(Encoding.GetEncoding("iso-8859-1"), Encoding.UTF8, cOutput);

            Metadata data = new Metadata();

            string output = Encoding.UTF8.GetString(cOutput, 0, cOutput.Length);
            Regex rSize = new Regex(@"Ssize={(?<y>\d+),(?<x>\d+)}");    // Ssize={2647,1748}
            Match mSize = rSize.Match(output);
            data.Width = Convert.ToInt32(mSize.Groups["x"].Value);
            data.Height = Convert.ToInt32(mSize.Groups["y"].Value);

            Regex rTileSize = new Regex(@"Stiles={(?<y>\d+),(?<x>\d+)}"); // Stiles={4096,2048}
            Match mTileSize = rTileSize.Match(output);
            data.TileWidth = Convert.ToInt32(mTileSize.Groups["x"].Value);
            data.TileHeight = Convert.ToInt32(mTileSize.Groups["y"].Value);

            Regex rLevels = new Regex(@"Clevels=(?<levels>\d+)");
            Match mLevels = rLevels.Match(output);
            data.Jp2levels = Convert.ToInt32(mLevels.Groups["levels"].Value) - 1;

            return data;
        }

        public Stream GetImage(State p_state)
        {
            string fname = ConfigurationManager.AppSettings["JP2CachePath"] + p_state.File;
            if (!File.Exists(fname))
            {
                throw new IIIFException(getErrorXml(new Error { Code = "i3f_200", Message = p_state.File + " does not exist" }));
            }

            try
            {
                return GetImageUsingKDUExpand(p_state);
            }
            catch (IIIFException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw e;
            }
        }


        private Stream GetImageUsingKDUExpand(State p_state)
        {
            try
            {
                string fname = ConfigurationManager.AppSettings["JP2CachePath"] + p_state.File;
                float starty = 0;
                float startx = 0;
                double tileheight = 1;
                double tilewidth = 1;

                string[] regions = null;
                float scale = 0;
                float scalex = 0;
                float scaley = 0;

                Metadata imageSize = GetJP2Metadata(fname);

                regions = p_state.Region.Split(new char[] { ',' });

                if (regions.Length == 4)
                {
                    if (!regions[0].StartsWith("%"))
                    {
                        tilewidth = int.Parse(regions[2]);
                        tileheight = int.Parse(regions[3]);
                    }
                    else
                    {
                        tilewidth = double.Parse(regions[2]) * imageSize.Width;
                        tileheight = double.Parse(regions[3]) * imageSize.Height;
                    }
                }
                // assume all region returned 
                else
                {
                    startx = 0;
                    starty = 0;
                }

                float wScale = (float)p_state.Width / imageSize.Width;
                float hScale = (float)p_state.Height / imageSize.Height;

                scale = scalex = scaley = 1;
                startx = 0;
                starty = 0;

                switch (p_state.SizeType)
                {
                    case "resize":
                        if (p_state.Region.Equals("all"))
                        {
                            scalex = wScale;
                            scaley = hScale;
                            if (p_state.SizeType.Equals("best"))
                            {
                                if (scalex < scaley)
                                {
                                    scale = scalex;
                                }
                                else
                                {
                                    scale = scaley;
                                }
                            }
                            else
                            {
                                if (scalex > scaley)
                                {
                                    scale = scalex;
                                }
                                else
                                {
                                    scale = scaley;
                                }
                            }
                            tilewidth = Convert.ToInt32(imageSize.Width * scale);
                            tileheight = Convert.ToInt32(imageSize.Height * scale);
                        }
                        else if (p_state.Width != 0 && p_state.Height != 0)
                        {
                            if (tilewidth > 0)
                                scalex = (float)(p_state.Width / tilewidth);
                            else
                                scalex = wScale;

                            if (tileheight > 0)
                                scaley = (float)(p_state.Height / tileheight);
                            else
                                scaley = hScale;

                            if (p_state.Width == p_state.Height)
                            {
                                scale = scalex; // both scales should be the same so shouldn't matter
                            }
                            else if (p_state.Width > p_state.Height)
                            {
                                scale = scalex;
                            }
                            else
                            {
                                scale = scaley;
                            }
                        }
                        else if (p_state.Width > p_state.Height)
                        {
                            if (tilewidth > 0)
                                scale = (float)(p_state.Width / tilewidth);
                            else
                                scale = wScale;

                            scaley = scalex = scale;
                        }
                        else
                        {
                            if (tileheight > 0)
                                scale = (float)(p_state.Height / tileheight);
                            else
                                scale = hScale;
                        }

                        // get all 
                        if (!p_state.SizeType.Equals("resize") && p_state.Region.Equals("all"))
                        {
                            if (p_state.Width != 0)
                                tilewidth = p_state.Width;

                            if (p_state.Height != 0)
                                tileheight = p_state.Height;
                        }

                        break;
                    case "best":
                        if (tilewidth > 0)
                            scalex = (float)(p_state.Width / tilewidth);
                        else
                            scalex = wScale;

                        if (tileheight > 0)
                            scaley = (float)(p_state.Height / tileheight);
                        else
                            scaley = hScale;

                        if (scalex < scaley)
                            scale = scalex;
                        else
                            scale = scaley;

                        // get all 
                        if (p_state.Region.Equals("all"))
                        {
                            tileheight = int.Parse(Math.Round(imageSize.Height * scale).ToString());
                            tilewidth = int.Parse(Math.Round(imageSize.Width * scale).ToString());
                        }
                        break;
                    case "proportion":
                        //scale = scalex = scaley = p_state.Size;
                        scale = p_state.Size;

                        // get all 
                        if (p_state.Region.Equals("all"))
                        {
                            tileheight = int.Parse(Math.Round(imageSize.Height * scale).ToString());
                            tilewidth = int.Parse(Math.Round(imageSize.Width * scale).ToString());
                        }

                        break;
                    default:
                        if (wScale < hScale)
                            scale = wScale;
                        else
                            scale = hScale;

                        scalex = scaley = scale;
                        break;
                }

                regions = p_state.Region.Split(new char[] { ',' });

                string region = "";
                if (regions.Length == 4)
                {
                    // for percentage regions
                    if (p_state.Region.StartsWith("%"))
                    {
                        startx = float.Parse(regions[0].Substring(1));
                        starty = float.Parse(regions[1]);

                        tilewidth = float.Parse(regions[2]);
                        tileheight = float.Parse(regions[3]);
                    }
                    else
                    {
                        startx = float.Parse(regions[0]) / imageSize.Width;
                        starty = float.Parse(regions[1]) / imageSize.Height;

                        tilewidth = float.Parse(regions[2]) / imageSize.Width;
                        tileheight = float.Parse(regions[3]) / imageSize.Height;
                    }

                    region = "{" + starty + "," + startx + "},{" + tileheight + "," + tilewidth + "}";
                }

                if (starty > 1 || startx > 1 || startx + tilewidth < 0 || starty + tileheight < 0)
                {
                    throw new IIIFException(getErrorXml(new Error { Code = "i3f_400", Message = "Invalid region specified" }));
                }

                // try to get the closest possible size to the one we want in order to reduce the tile size
                // when creating an image tile 
                int reduce = Convert.ToInt32(1 / scale);
                if (reduce > 0)
                    reduce = Convert.ToInt32(Math.Floor(Math.Log(reduce, 2.5)));
                p_state.Reduce = reduce;

                string outputFile = ConfigurationManager.AppSettings["JP2CachePath"] + p_state.Target.Replace(".jpg", ".bmp");

                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = ConfigurationManager.AppSettings["ExpanderPath"];
                start.UseShellExecute = false;
                start.RedirectStandardOutput = false;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;

                start.Arguments = "-resilient -quiet -i \"" + fname + "\" -o \"" +
                    outputFile +
                    "\" -reduce " + reduce +
                    (string.IsNullOrEmpty(region) ? "" : " -region " + region);

                Process proc = Process.Start(start);
                proc.WaitForExit();

                BufferedStream bs = new BufferedStream(new FileStream(outputFile, FileMode.Open));

                int length = (int)bs.Length;
                byte[] imageBits = new byte[length];

                // read the digital bits of the image into the byte array
                bs.Read(imageBits, 0, length);

                Image img = Image.FromStream(bs);

                Bitmap bmp = new Bitmap(img);

                MemoryStream stream = new MemoryStream();
                bmp.Save(stream, ImageFormat.Jpeg);

                // remove temporary bmp file
                img.Dispose();
                bs.Close();
                bs.Dispose();

                File.Delete(outputFile);

                return stream;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}