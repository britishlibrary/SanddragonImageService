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
using System.Linq;
using System.Web;
using System.Xml.Serialization;

namespace SanddragonImageService
{
    [XmlRoot(ElementName = "info", Namespace = "http://library.stanford.edu/iiif/image-api/ns/")]
    public class Metadata
    {
        public int height
        {
            get;
            set;
        }

        public int width
        {
            get;
            set;
        }

        public int tile_height
        {
            get;
            set;
        }

        public int tile_width
        {
            get;
            set;
        }

        public string identifier
        {
            get;
            set;
        }

        [XmlArrayItem(ElementName="scale_factor")]
        public scale_factors scale_factors
        {
            get;
            set;
        }

        [XmlArrayItem(ElementName = "quality")]
        public qualities qualities
        {
            get;
            set;
        }

        [XmlArrayItem(ElementName = "format")]
        public formats formats
        {
            get;
            set;
        }
    }

    public class scale_factors : List<int>
    { }

    public class formats : List<string>
    { }

    public class qualities : List<string>
    { }
}