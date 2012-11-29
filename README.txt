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

//
// Overview
//
For an overview of this project please visit http://sanddragon.bl.uk

//
// IIIF Server notes
//
This SanddragonImageService was developed to conform with the following API http://lib.stanford.edu/iiif/
This project was developed using the Kakadu Software kdu_expand.exe component and is intended to be a lightweight JPEG 2000 image service.

//
// SanddragonImageService Installation
//

1. Ensure you have IIS 7 and .Net Framework 4 installed on your server.
2. Ensure you have Web Deploy module installed on IIS otherwise download it from here - http://www.iis.net/download/WebDeploy
3. Ensure you have URL rewrite module installed on IIS otherwise download it from here - http://www.iis.net/download/urlrewrite
4. Build the SanddragonImageService application using Visual Studio 2010 and then build the deployment package, the default zip location will be obj\Debug\Package\SanddragonImageService.zip.
5. Use the Web Deploy feature in IIS 7 to import the SanddragonImageService application onto your web server.
6. Download the Kakadu demo from the Kakadu Software website - http://www.kakadusoftware.com/executables/KDU71_Demo_Apps_for_Win32_120625.msi
7. Install the Kakadu demo and ensure that it works by using a command prompt to navigate to the installation folder and running kdu_expand.exe
8. Update the SanddragonImageService web.config KakaduInstall parameter to point to the location of the Kakadu installation.
9. Update the SanddragonImageService web.config JP2CachePath parameter to point to the location of your JP2 files.

//
// Example Usage
//

For image metadata information - 
http://[ServerName]/[SanddragonImageService site]/Metadata/[jp2 filename]/info.xml

To get a full colour image at 10% of size -
http://[ServerName]/[SanddragonImageService site]/image/[jp2 filename]/full/pct:10/0/color

Further usage examples can be found at the IIIF link - http://library.stanford.edu/iiif/image-api/

If you do not have any JPEG2000 images available example JPEG2000s can be downloaded from here - http://sanddragon.bl.uk/JP2/