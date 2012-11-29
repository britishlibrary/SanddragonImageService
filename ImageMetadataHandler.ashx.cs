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
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Web;
using System.Web.Services;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace SanddragonImageService
{
    /// <summary>
    /// Summary description for ImageMetadataHandler
    /// </summary>
    public class ImageMetadataHandler : IHttpHandler
    {
        error err = null;

        public void ProcessRequest(HttpContext context)
        {
            //write your handler implementation here.
            try
            {
                context.Response.Clear();
                context.Response.ClearHeaders();
                

                string result = "";

                if (context.Request.Url.AbsoluteUri.Length > 1024)
                {
                    err = new error();
                    err.statusCode = System.Net.HttpStatusCode.RequestUriTooLong;
                    err.parameter = "unknown";
                    err.text = "Request > 1024 characters, too long";
                }
                else 
                    result = GetWidthAndHeight(context.Request["identifier"], context.Request["return"]);

                if (err != null)
                {
                    context.Response.ContentType = "text/xml";
                    context.Response.StatusCode = (int)err.statusCode;
                }
                else if (context.Request["return"].Equals("json"))
                {
                    context.Response.ContentType = "application/json";
                    context.Response.AddHeader("content-disposition", "attachment; filename=export.json");
                }
                else
                {
                    context.Response.ContentType = "application/xml";
                    context.Response.AddHeader("content-disposition", "attachment; filename=export.xml");
                }

                context.Response.AddHeader("Link", "<http://library.stanford.edu/iiif/image-api/compliance.html#level2>;rel=\"compliesTo\"");                
                context.Response.AddHeader("content-length", (result.Length).ToString());

                context.Response.Flush();
                context.Response.Write(result);

                HttpContext.Current.ApplicationInstance.CompleteRequest();
            }
            catch (Exception e)
            {
                context.Response.Write(e.Message);
            }
        }

        private string GetWidthAndHeight(string p_identifier, string p_returnType)
        {
            try
            {
                Extractor kdu = new Extractor();

                string xmlMetadata = kdu.GetMetadata(p_identifier.EndsWith(".jp2") ? p_identifier : p_identifier + ".jp2");

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xmlMetadata);

                Metadata data = new Metadata();
                data.identifier = p_identifier;
                data.width = int.Parse(doc.GetElementsByTagName("Width")[0].InnerText);
                data.height = int.Parse(doc.GetElementsByTagName("Height")[0].InnerText);

                scale_factors scaleFactors = new scale_factors();
                for (int i = 0; i < int.Parse(doc.GetElementsByTagName("Jp2levels")[0].InnerText); i++)
                {
                    scaleFactors.Add(Convert.ToInt32(Math.Pow(2, i)));
                }
                data.scale_factors = scaleFactors;

                formats formats = new formats();
                formats.Add("jpg");
                formats.Add("png");
                formats.Add("gif");

                data.formats = formats;

                qualities qualities = new qualities();
                qualities.Add("native");
                qualities.Add("grey");
                qualities.Add("bitonal");

                data.qualities = qualities;

                data.tile_width = int.Parse(doc.GetElementsByTagName("TileWidth")[0].InnerText);
                data.tile_height = int.Parse(doc.GetElementsByTagName("TileHeight")[0].InnerText);

                if (p_returnType.Equals("json"))
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    return serializer.Serialize(data);
                }
                else
                {
                    return getSerializedXML(data);
                }
            }
            catch (Exception e)
            {
                err = new error();

                try
                {
                    err.parameter = "identifier";

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(e.Message);

                    err.text = doc.GetElementsByTagName("Message")[0].InnerText;
                    err.statusCode = System.Net.HttpStatusCode.NotFound;
                }
                catch 
                {
                    err.statusCode = System.Net.HttpStatusCode.BadRequest;
                    err.parameter = "unknown";
                    err.text = e.Message;
                }
                return getSerializedXML(err);
            }
        }

        private string getSerializedXML(object obj)
        {
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://library.stanford.edu/iiif/image-api/ns/");

            XmlSerializer xs = new XmlSerializer(obj.GetType());
            using (MemoryStream memoryStream = new MemoryStream())
            {
                XmlWriterSettings settings = new XmlWriterSettings()
                {
                    Encoding = Encoding.ASCII
                };
                using (XmlWriter writer = XmlWriter.Create(memoryStream, settings))
                {
                    xs.Serialize(writer, obj, ns);
                }
                return Encoding.ASCII.GetString(memoryStream.ToArray());
            }
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}