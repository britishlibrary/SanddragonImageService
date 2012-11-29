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
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.ClientServices;
using System.Web.Services;
using System.Xml;
using System.Xml.Serialization;

namespace SanddragonImageService
{
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    public class ImageHandler : IHttpAsyncHandler
    {
        /// <summary>
        /// You will need to configure this handler in the web.config file of your 
        /// web and register it with IIS before being able to use it. For more information
        /// see the following link: http://go.microsoft.com/?linkid=8101007
        /// </summary>
        #region IHttpHandler Members

        private ImageAsynchResult asynch;
        private HttpContext _context;
        private Exception _ex;
        private Bitmap _bitmap;
        private delegate void GetImageDelegate(IAsyncResult state);
        GetImageDelegate getImage;

        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            //write your handler implementation here.
        }

        public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, Object extraData)
        {
            _context = context;

            ImageRequestState state = new ImageRequestState();

            string quality = "";
            string format = "jpg";
            int dotPos = context.Request["quality_format"].IndexOf("."); 
            
            if (dotPos != -1)
            {
                quality = context.Request["quality_format"].Substring(0, dotPos);
                format = context.Request["quality_format"].Substring(dotPos + 1);
            }
            else
            {
                quality = context.Request["quality_format"];
            }

            if (string.IsNullOrEmpty(context.Request["identifier"]) || string.IsNullOrEmpty(context.Request["size"]) ||
                string.IsNullOrEmpty(context.Request["region"]) || string.IsNullOrEmpty(quality) ||
                string.IsNullOrEmpty(context.Request["rotation"]))
            {
                state.err = new error();

                if (string.IsNullOrEmpty(context.Request["identifier"]))
                    state.err.parameter = ", identifier";
                if (string.IsNullOrEmpty(context.Request["size"]))
                    state.err.parameter += ", size";
                if (string.IsNullOrEmpty(context.Request["region"]))
                    state.err.parameter += ", region";
                if (string.IsNullOrEmpty(context.Request["rotation"]))
                    state.err.parameter += ", rotation";
                if (string.IsNullOrEmpty(quality))
                    state.err.parameter += ", quality";
                if (string.IsNullOrEmpty(context.Request["format"]))
                    state.err.parameter += ", format";

                state.err.parameter = state.err.parameter.Substring(2);
                state.err.text = "missing " + state.err.parameter;

                state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
            }

            string size = context.Request["size"];
            if (state.err == null && !string.IsNullOrEmpty(size))
            {
                if (size.EndsWith(","))
                {
                    state.sizeType = "resize";
                    state.width = size.Replace(",", "");

                    if (!string.IsNullOrEmpty(state.width) && int.Parse(state.width) < 1)
                    {
                        state.err = new error();
                        state.err.parameter = "size";
                        state.err.text = "Invalid size specified";
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                    }
                }
                else if (size.StartsWith(","))
                {
                    state.sizeType = "resize";
                    state.height = size.Replace(",", "");

                    if (!string.IsNullOrEmpty(state.height) && int.Parse(state.height) < 1)
                    {
                        state.err = new error();
                        state.err.parameter = "size";
                        state.err.text = "Invalid size specified";
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                    }
                }
                else if (size.StartsWith("pct:"))
                {
                    state.sizeType = "proportion";
                    state.size = (double.Parse(size.Replace("pct:", "")) / 100).ToString();

                    if (!string.IsNullOrEmpty(state.size) && double.Parse(state.size) <= 0)
                    {
                        state.err = new error();
                        state.err.parameter = "size";
                        state.err.text = "Invalid size specified";
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                    }
                }
                else if (size.StartsWith("!"))
                {
                    Regex r = new Regex(@"!(?<w>\d+),(?<h>\d+)");
                    Match m = r.Match(size);

                    state.sizeType = "best";
                    state.width = m.Groups["w"].Value;
                    state.height = m.Groups["h"].Value;

                    if ((!string.IsNullOrEmpty(state.width) && int.Parse(state.width) < 1) ||
                        (!string.IsNullOrEmpty(state.height) && int.Parse(state.height) < 1))
                    {
                        state.err = new error();
                        state.err.parameter = "size";
                        state.err.text = "Invalid size specified";
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                    }
                }
                else
                {
                    Regex r = new Regex(@"(?<w>\d+),(?<h>\d+)");
                    Match m = r.Match(size);

                    state.sizeType = "resize";
                    state.width = m.Groups["w"].Value;
                    state.height = m.Groups["h"].Value;

                    if ((!string.IsNullOrEmpty(state.width) && int.Parse(state.width) < 1) ||
                        (!string.IsNullOrEmpty(state.height) && int.Parse(state.height) < 1))
                    {
                        state.err = new error();
                        state.err.parameter = "size";
                        state.err.text = "Invalid size specified";
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                    }
                }

                if (string.IsNullOrEmpty(state.sizeType))
                {
                    state.err = new error();
                    state.err.parameter = "size";
                    state.err.text = "Invalid size specified";
                    state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                }
            }

            string region = context.Request["region"];
            if (state.err == null && !string.IsNullOrEmpty(region))
            {
                if (context.Request["region"].Equals("full"))
                {
                    state.region = "all";
                }
                else if (region.StartsWith("pct:"))
                {
                    Regex r = new Regex(@"pct:(?<x>(\d+|\d+\.\d+)),(?<y>(\d+|\d+\.\d+)),(?<w>(\d+|\d+\.\d+)),(?<h>(\d+|\d+\.\d+))");
                    Match m = r.Match(region);
                    double y = Convert.ToDouble(m.Groups["y"].Value) / 100;
                    double x = Convert.ToDouble(m.Groups["x"].Value) / 100;
                    double h = Convert.ToDouble(m.Groups["h"].Value) / 100;
                    double w = Convert.ToDouble(m.Groups["w"].Value) / 100;

                    state.region = "%" + x + "," + y + "," + w + "," + h;

                    state.regionHeight = h.ToString();
                    state.regionWidth = w.ToString();

                    if ((!string.IsNullOrEmpty(state.regionWidth) && Convert.ToDouble(state.regionWidth) <= 0) ||
                        (!string.IsNullOrEmpty(state.regionHeight) && Convert.ToDouble(state.regionHeight) <= 0))
                    {
                        state.err = new error();
                        state.err.parameter = "region";
                        state.err.text = "Invalid region specified";
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                    }
                }
                else
                {
                    Regex r = new Regex(@"(?<x>\d+),(?<y>\d+),(?<w>\d+),(?<h>\d+)");
                    Match m = r.Match(region);

                    state.region = m.Groups["x"].Value + "," + m.Groups["y"].Value + "," + m.Groups["w"].Value + "," + m.Groups["h"].Value;
                    state.regionHeight = m.Groups["h"].Value;
                    state.regionWidth = m.Groups["w"].Value;

                    if (state.sizeType.Equals("proportion"))
                    {
                        if (!string.IsNullOrEmpty(state.regionWidth) && Convert.ToInt32(state.regionWidth) * Convert.ToDouble(state.size) < 1)
                        {
                            state.err = new error();
                            state.err.parameter = "region";
                            state.err.text = "Invalid region specified";
                            state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                        }
                        else if (!string.IsNullOrEmpty(state.regionHeight) && Convert.ToInt32(state.regionHeight) * Convert.ToDouble(state.size) < 1)
                        {
                            state.err = new error();
                            state.err.parameter = "region";
                            state.err.text = "Invalid region specified";
                            state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                        }
                    }
                    if ((!string.IsNullOrEmpty(state.regionWidth) && int.Parse(state.regionWidth) < 1) ||
                        (!string.IsNullOrEmpty(state.regionHeight) && int.Parse(state.regionHeight) < 1))
                    {
                        state.err = new error();
                        state.err.parameter = "region";
                        state.err.text = "Invalid region specified";
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                    }
                }

                if (string.IsNullOrEmpty(state.region.Replace(",", "")))
                {
                    state.err = new error();
                    state.err.parameter = "region";
                    state.err.text = "Invalid region specified";
                    state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                }
            }

            if (state.err == null && !string.IsNullOrEmpty(quality))
            {
                switch (quality)
                {
                    case "grey": quality = "grey"; break;
                    case "bitonal": quality = "bitonal"; break;
                    case "color": quality = "color"; break;
                    case "native": quality = "native"; break;
                    default:
                        state.err = new error();
                        state.err.parameter = "quality";
                        state.err.text = "only native, color, grey, bitonal supported";
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                        break;
                }
            }

            if (state.err == null && !string.IsNullOrEmpty(format))
            {
                switch (format)
                {
                    case "jpg": format = "jpg"; break;
                    case "png": format = "png"; break;
                    case "gif": format = "gif"; break;
                    case "jp2":
                    case "pdf":
                    default:
                        state.err = new error();
                        state.err.parameter = "format";
                        state.err.text = format + " not supported";
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                        break;     // no format matched
                }
            }

            state.identifier = context.Request["identifier"];
            state.rotation = context.Request["rotation"];
            state.quality = quality;
            state.format = format;

            asynch = new ImageAsynchResult(cb, context, state);

            getImage = new GetImageDelegate(GetImage);
            getImage.BeginInvoke(asynch, null, state);

            return asynch;
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            _context.Response.AddHeader("Link", "<http://library.stanford.edu/iiif/image-api/compliance.html#level2>;rel=\"compliesTo\"");
            if (_ex != null)
            {
                // If an exception was thrown, rethrow it
                throw _ex;
            }
            else
            {
                ImageRequestState state = (ImageRequestState)result.AsyncState;
                if (state.err != null)
                {
                    string xmlError = "";
                    XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                    ns.Add("", "http://library.stanford.edu/iiif/image-api/ns/");

                    XmlSerializer xs = new XmlSerializer(state.err.GetType());
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        XmlWriterSettings settings = new XmlWriterSettings()
                        {
                            Encoding = Encoding.ASCII
                        };
                        using (XmlWriter writer = XmlWriter.Create(memoryStream, settings))
                        {
                            xs.Serialize(writer, state.err, ns);
                        }
                        xmlError = Encoding.ASCII.GetString(memoryStream.ToArray());
                    }

                    _context.Response.StatusCode = (int)state.err.statusCode;
                    _context.Response.ContentType = "application/xml";
                    _context.Response.Write(xmlError);
                }
                else
                {
                    string contentType = "image/jpeg";
                    ImageFormat imageFormat = ImageFormat.Jpeg;

                    switch (state.format)
                    {
                        case "png": imageFormat = ImageFormat.Png; contentType = "image/png"; break;
                        case "gif": imageFormat = ImageFormat.Gif; contentType = "image/gif"; break;
                    }
                    
                    _context.Response.ContentType = contentType;

                    if (state.quality.Equals("native"))
                    {
                        _bitmap.Save(_context.Response.OutputStream, imageFormat);
                    }
                    else
                    {
                        ImageCodecInfo codec = GetEncoderInfo(contentType);

                        System.Drawing.Imaging.Encoder encoder = System.Drawing.Imaging.Encoder.ColorDepth;
                        EncoderParameters encoderParameters = new EncoderParameters(1);
                        if (state.quality.Equals("color"))
                        {
                            encoderParameters.Param[0] = new EncoderParameter(encoder, 24L);
                        }
                        else if (state.quality.Equals("grey"))
                        {
                            encoderParameters.Param[0] = new EncoderParameter(encoder, 8L);
                        }
                        else    // must be bitonal
                        {
                            encoderParameters.Param[0] = new EncoderParameter(encoder, 1L);
                        }                        
                        
                        _bitmap.Save(_context.Response.OutputStream, codec, encoderParameters);
                    }
                    _bitmap.Dispose();
                }
            }
        }

        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        object lockobj = new object();

        private void GetImage(IAsyncResult p_res)
        {
            ImageRequestState state = (ImageRequestState)p_res.AsyncState;
            if (state.err != null) 
            {
                asynch.CompleteCall();
            }
            else {
                lock (lockobj)
                {
                    try
                    {
                        Extractor kdu = new Extractor();
                        State kdu_state = new State();

                        kdu_state.File = state.identifier.EndsWith(".jp2") ? state.identifier : state.identifier + ".jp2";
                        kdu_state.SizeType = state.sizeType;
                        kdu_state.Size = string.IsNullOrEmpty(state.size) ? 0 : float.Parse(state.size);
                        kdu_state.Width = string.IsNullOrEmpty(state.width) ? 0 : int.Parse(state.width);
                        kdu_state.Height = string.IsNullOrEmpty(state.height) ? 0 : int.Parse(state.height);
                        kdu_state.Region = state.region;
                        kdu_state.Format = state.format;
                        kdu_state.Quality = state.quality;

                        Stream stream = kdu.GetImage(kdu_state);
                        Image img = Image.FromStream(stream);

                        if (state.sizeType.Equals("resize"))
                        {
                            int sizeWidth = img.Width;
                            int sizeHeight = img.Height;

                            if (!string.IsNullOrEmpty(state.width) && !string.IsNullOrEmpty(state.height))
                            {
                                sizeWidth = Convert.ToInt32(state.width);
                                sizeHeight = Convert.ToInt32(state.height);
                            }
                            else if (!string.IsNullOrEmpty(state.width))
                            {
                                sizeWidth = Convert.ToInt32(state.width);
                                sizeHeight = (int)(((float)sizeWidth / img.Width) * sizeHeight);
                            }
                            else if (!string.IsNullOrEmpty(state.height))
                            {
                                sizeHeight = Convert.ToInt32(state.height);
                                sizeWidth = (int)(((float)sizeHeight / img.Height) * sizeWidth);
                            }

                            img = ResizeImage(img, sizeWidth, sizeHeight);
                        }
                        else if (state.sizeType.Equals("best"))
                        {
                            int sizeWidth = Convert.ToInt32(state.width);
                            int sizeHeight = Convert.ToInt32(state.height);
                            double regionWidth = img.Width;
                            if (!string.IsNullOrEmpty(state.regionWidth))
                                regionWidth = Convert.ToInt32(state.regionWidth);
                            double regionHeight = img.Height;
                            if (!string.IsNullOrEmpty(state.regionHeight))
                                regionHeight = Convert.ToInt32(state.regionHeight);

                            double scalex = sizeWidth / regionWidth;
                            double scaley = sizeHeight / regionHeight;

                            if (scalex < scaley)
                                img = ResizeImage(img, (int)(regionWidth * scalex), (int)(regionHeight * scalex));
                            else
                                img = ResizeImage(img, (int)(regionWidth * scaley), (int)(regionHeight * scaley));
                        }
                        else if (state.sizeType.Equals("proportion"))
                        {
                            double regionWidth = 0;
                            double regionHeight = 0;

                            if (state.region.Equals("all"))
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.LoadXml(kdu.GetMetadata(state.identifier.EndsWith(".jp2") ? state.identifier : state.identifier + ".jp2"));

                                regionWidth = Convert.ToInt32(doc.GetElementsByTagName("Width")[0].InnerText);
                                regionHeight = Convert.ToInt32(doc.GetElementsByTagName("Height")[0].InnerText);
                            }
                            else if (state.region.StartsWith("%"))
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.LoadXml(kdu.GetMetadata(state.identifier.EndsWith(".jp2") ? state.identifier : state.identifier + ".jp2"));

                                regionWidth = Convert.ToDouble(state.regionWidth) * Convert.ToInt32(doc.GetElementsByTagName("Width")[0].InnerText);
                                regionHeight = Convert.ToDouble(state.regionHeight) * Convert.ToInt32(doc.GetElementsByTagName("Height")[0].InnerText);
                            }
                            else
                            {
                                regionWidth = Convert.ToInt32(state.regionWidth);
                                regionHeight = Convert.ToInt32(state.regionHeight);
                            }

                            regionWidth *= Convert.ToDouble(state.size);
                            regionHeight *= Convert.ToDouble(state.size);

                            if (regionWidth < 1 || regionHeight < 1)
                            {
                                state.err = new error();
                                state.err.parameter = "region / size";
                                state.err.text = "Invalid region /size specified";
                                state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                            }
                            else
                            {
                                img = ResizeImage(img, (int)regionWidth, (int)regionHeight);
                            }
                        }

                        if (state.err == null)
                        {
                            if (!string.IsNullOrEmpty(state.quality))
                            {
                                if (state.quality.Equals("grey"))
                                    img = MakeGrayscale(new Bitmap(img));
                                else if (state.quality.Equals("bitonal"))
                                    img = MakeBitonal(new Bitmap(img));
                            }

                            switch (state.rotation)
                            {
                                case "90": img.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                                case "180": img.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                                case "270": img.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
                            }

                            _bitmap = new Bitmap(img);
                        }
                    }
                    catch (IIIFException e)
                    {
                        state.err = new error();

                        try
                        {
                            XmlDocument doc = new XmlDocument();
                            doc.LoadXml(e.Message);

                            if (doc.GetElementsByTagName("Code")[0].InnerText.Contains("i3f_200"))
                            {
                                state.err.parameter = "identifier";
                                state.err.text = doc.GetElementsByTagName("Message")[0].InnerText;
                                state.err.statusCode = System.Net.HttpStatusCode.NotFound;
                            }
                            else if (doc.GetElementsByTagName("Code")[0].InnerText.Contains("i3f_400"))
                            {
                                state.err.parameter = "region";
                                state.err.text = doc.GetElementsByTagName("Message")[0].InnerText;
                                state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                            }
                            else
                            {
                                state.err.parameter = "unknown";
                                state.err.text = e.Message;
                                state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                            }
                        }
                        catch (Exception ex)
                        {
                            state.err.parameter = "unknown";
                            state.err.text = ex.Message;
                            state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                        }
                    }
                    catch (Exception e)
                    {
                        state.err = new error();
                        state.err.parameter = "unknown";
                        state.err.text = e.Message;
                        state.err.statusCode = System.Net.HttpStatusCode.BadRequest;
                    }
                    finally
                    {
                        asynch.CompleteCall();
                    }
                }
            }
        }

        private Image ResizeImage(Image img, int nWidth, int nHeight)
        {
            Bitmap bmp = new Bitmap(nWidth, nHeight);
            using (Graphics g = Graphics.FromImage((Image)bmp))
                g.DrawImage((Bitmap)img, 0, 0, nWidth, nHeight);

            return bmp;
        }

        private Bitmap MakeGrayscale(Bitmap original)
        {
            ColorMatrix colorMatrix = new ColorMatrix(
               new float[][] 
                {
                    new float[] {.3f, .3f, .3f, 0, 0},
                    new float[] {.59f, .59f, .59f, 0, 0},
                    new float[] {.11f, .11f, .11f, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {0, 0, 0, 0, 1}
                });

            return ChangeColour(original, colorMatrix , false);
        }

        private Bitmap MakeBitonal(Bitmap original)
        {
            ColorMatrix colorMatrix = new ColorMatrix( 
                            new float[][] {
                                new float[] {0.5f, 0.5f, 0.5f, 0, 0},
                                new float[] {0.5f, 0.5f, 0.5f, 0, 0},
                                new float[] {0.5f, 0.5f, 0.5f, 0, 0},
                                new float[] {0, 0, 0, 1, 0},
                                new float[] {0, 0, 0, 0, 1}  
                            });

            return ChangeColour(original, colorMatrix, true);
        }

        private Bitmap ChangeColour(Bitmap original, ColorMatrix colorMatrix, bool setThresholdForBitonal)
        {
            //create a blank bitmap the same size as original
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);

            //get a graphics object from the new image
            Graphics g = Graphics.FromImage(newBitmap);

            //create some image attributes
            ImageAttributes attributes = new ImageAttributes();

            //set the color matrix attribute
            attributes.SetColorMatrix(colorMatrix);

            // if the image is required is bitonal set a threshold
            if (setThresholdForBitonal)
                attributes.SetThreshold(0.75f);

            //draw the original image on the new image
            //using the grayscale color matrix
            g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
               0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);

            //dispose the Graphics object
            g.Dispose();

            return newBitmap;
        }

        private MemoryStream getImageInFormat(Stream p_stream, string p_format)
        {
            return getImageInFormat(p_stream, null, p_format);
        }

        private MemoryStream getImageInFormat(Image p_img, string p_format)
        {
            return getImageInFormat(null, p_img, p_format);
        }

        private MemoryStream getImageInFormat(Stream p_stream, Image p_img, string p_format)
        {
            MemoryStream ms = new MemoryStream();
            Image img = null;

            if (p_stream != null)
                img = Image.FromStream(p_stream, true);
            else
                img = p_img;

            System.Drawing.Imaging.ImageFormat imageFormat = System.Drawing.Imaging.ImageFormat.Jpeg;

            if (p_format.Equals("png"))
            {
                imageFormat = System.Drawing.Imaging.ImageFormat.Png;
            }
            else if (p_format.Equals("gif"))
            {
                imageFormat = System.Drawing.Imaging.ImageFormat.Gif;
            }

            img.Save(ms, imageFormat);
            ms.Position = 0;
            return ms;
        }

        private Bitmap DeserializeImage(string base64Data)
        {
            MemoryStream stream = new MemoryStream(Convert.FromBase64String(base64Data));
            Bitmap bitmap = new Bitmap(stream);
            return bitmap;
        }

        #endregion
    }

    // The RequestState class passes data across async calls.
    public class ImageRequestState
    {
        public string identifier;
        public string region;
        public string regionWidth;
        public string regionHeight;
        public string size;
        public string width;
        public string height;
        public string sizeType;
        public string rotation;
        public string format;
        public string quality;

        public error err;
    }

    class ImageAsynchResult : IAsyncResult
    {
        private bool _completed;
        private object _state;
        private AsyncCallback _callback;
        private HttpContext _context;
        private object _lock = new object();
        private ManualResetEvent _event;

        bool IAsyncResult.IsCompleted { get { return _completed; } }
        WaitHandle IAsyncResult.AsyncWaitHandle { get { return null; } }
        Object IAsyncResult.AsyncState { get { return _state; } }
        bool IAsyncResult.CompletedSynchronously { get { return false; } }

        public ImageAsynchResult(AsyncCallback callback, HttpContext context, object state)
        {
            _callback = callback;
            _context = context;
            _state = state;
            _completed = false;
        }

        public void StartAsyncWork()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(StartAsyncTask), null);
        }

        private void StartAsyncTask(Object workItemState)
        {
            _completed = true;
            _callback(this);
        }

        public object AsyncState { get { return _state; } }

        public bool CompletedSynchronously { get { return false; } }

        public bool IsCompleted { get { return _completed; } }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                lock (_lock)
                {
                    if (_event == null)
                        _event = new ManualResetEvent(IsCompleted);
                    return _event;
                }
            }
        }

        public void CompleteCall(object p_state)
        {
            lock (_lock)
            {
                _state = p_state;
                _completed = true;
                if (_event != null) _event.Set();
            }

            if (_callback != null) _callback(this);
        }

        public void CompleteCall()
        {
            lock (_lock)
            {
                _completed = true;
                if (_event != null) _event.Set();
            }

            if (_callback != null) _callback(this);
        }
    }
}