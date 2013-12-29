using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

using System.Runtime.InteropServices;


using System.Windows;
//using System.Windows.Controls;

using System.Globalization;



namespace Wsqm
{
    public class WSQ : IDisposable
    {
        
        private Encoder _EncoderWSQ;
        private Decoder _DecoderWSQ;
        public WSQ() {
            _EncoderWSQ = new Encoder();
            _DecoderWSQ = new Decoder();
        }
        

        //Implement IDisposable.
        public void Dispose()
        {
            
            GC.SuppressFinalize(this);
            _EncoderWSQ = null;
            _DecoderWSQ = null;
        }

        
        // Use C# destructor syntax for finalization code.
        ~WSQ()
        {
            
            Dispose();
        }


        /// <summary>
        /// Encode image fingerprint to wsq file
        /// </summary>
        /// <param name="FileSource">File source (bmp,tiff)</param>
        /// <param name="FileDest">File wsq</param>
        /// <param name="comments">Comments in image</param>
        /// <param name="BitRate">Bit rate</param>
        public void EnconderFile(String FileSource, 
                                String FileDest, 
                                String[] comments, 
                                double BitRate)
        {
            try
            {
                switch (Path.GetExtension(FileSource).ToUpper())
                {
                    case ".BMP":
                    case ".TIF":
                        break;
                    default:
                        throw new ApplicationException("Error: FileSource extension no supported");

                }

                if (Path.GetExtension(FileDest).ToUpper().Replace(".", "") != "WSQ")
                {
                    throw new ApplicationException("Error: FileDest extension no supported");
                }




                Bitmap bm = null;
                System.Drawing.Image img = null;
                byte[] fileData;

                Wsqm.cBitmap bitmap;
                Wsqm.DataOutput data;
                BitmapSource bitmapSource;

                switch (Path.GetExtension(FileSource).ToUpper())
                {
                    case ".BMP":
                        img = System.Drawing.Image.FromFile(FileSource);
                        bm = new Bitmap(img);


                        Uri myUri = new Uri(FileSource, UriKind.RelativeOrAbsolute);
                        BmpBitmapDecoder decoder2 = new BmpBitmapDecoder(myUri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                        bitmapSource = decoder2.Frames[0];


                        fileData = GetBytesFromBitmapSource(bitmapSource);

                        bitmap = new Wsqm.cBitmap(fileData,
                                bm.Width,
                                bm.Height, 500, 8, 1);

                        data = new Wsqm.DataOutput();
                        data.RutaDestino = FileDest;

                        _EncoderWSQ.encode(data, bitmap, BitRate, comments);

                        
                        img.Dispose();

                        break;
                    case ".TIF":
                        img = System.Drawing.Image.FromFile(FileSource);
                        bm = new Bitmap(img);


                        Stream imageStreamSource = new FileStream(FileSource, FileMode.Open, FileAccess.Read, FileShare.Read);
                        TiffBitmapDecoder decoder = new TiffBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                        bitmapSource = decoder.Frames[0];


                        fileData = GetBytesFromBitmapSource(bitmapSource);

                        bitmap = new Wsqm.cBitmap(fileData,
                                bm.Width,
                                bm.Height, 500, 8, 1);

                        data = new Wsqm.DataOutput();
                        data.RutaDestino = FileDest;
                        _EncoderWSQ.encode(data, bitmap, BitRate, comments);


                        img.Dispose();
                        imageStreamSource.Dispose();
                        break;

                }
            }
            catch (Exception e)
            {
                throw (e);
            }
          
        }

        /// <summary>
        /// Decode wsq file to image fingerprint
        /// </summary>
        /// <param name="FileSource">File source wsq</param>
        /// <param name="FileDest">File (bmp,tiff)</param>
        public void DecoderFile(String FileSource, String FileDest)
        {
            try
            {
                
                if (Path.GetExtension(FileSource).ToUpper().Replace(".", "") != "WSQ")
                {
                    throw new ApplicationException("Error: FileSource extension no supported");
                }

                byte[] fileData=null;
               
                BitmapSource image;
                FileStream stream;
                FileStream fs;
                DataInput data;
                cBitmapWithMetadata arch;
                byte[] datos;
                int rawStride;
                PixelFormat pf;
                int tope;

                switch (Path.GetExtension(FileDest).ToUpper())
                {
                    case ".BMP":
                        fs = File.OpenRead(FileSource);
                        fileData = new byte[fs.Length];
                        fs.Read(fileData, 0, fileData.Length);


                        data = new DataInput(fileData);
                        
                        arch = _DecoderWSQ.decode(data);

                        
                        pf = PixelFormats.Gray8;

                        rawStride = (arch.getWidth() * pf.BitsPerPixel + 7) / 8;
                      

                        datos = new byte[rawStride * arch.getHeight()];


                        if (datos.Length > arch.getLength())
                            tope = arch.getLength();
                        else
                            tope = datos.Length;

                        for (int y = 0; y < tope; y++)
                            {
                                datos[y] = arch.getPixels()[y];
                            }
                            
                       

                        image = BitmapSource.Create(
                                arch.getWidth(),
                                arch.getHeight(),
                                96,
                                96,
                                pf,
                                null,
                                datos,
                                rawStride);


                        stream = new FileStream(FileDest, FileMode.Create);
                        BmpBitmapEncoder encoder = new BmpBitmapEncoder();                        
                        encoder.Frames.Add(BitmapFrame.Create(image));
                        encoder.Save(stream);


                        stream.Dispose();
                        fs.Dispose();
                        
                        break;

                    case ".TIF":
                         fs = File.OpenRead(FileSource);
                        fileData = new byte[fs.Length];
                        fs.Read(fileData, 0, fileData.Length);

                        data = new DataInput(fileData);                        
                        arch = _DecoderWSQ.decode(data);

                        
                        pf = PixelFormats.Gray8;

                        rawStride = (arch.getWidth() * pf.BitsPerPixel + 7) / 8;
                      

                        datos = new byte[rawStride * arch.getHeight()];


                        if (datos.Length > arch.getLength())
                            tope = arch.getLength();
                        else
                            tope = datos.Length;

                            for (int y = 0; y < tope; y++)
                            {
                                datos[y] = arch.getPixels()[y];
                            }


                        // Creates a new empty image with the pre-defined palette

                        image = BitmapSource.Create(
                               arch.getWidth(),
                               arch.getHeight(),
                               96,
                               96,
                               pf,
                               null,
                               datos,
                               rawStride);

                        stream = new FileStream(FileDest, FileMode.Create);
                        TiffBitmapEncoder encodert = new TiffBitmapEncoder();
                       
                        encodert.Compression = TiffCompressOption.None;
                        encodert.Frames.Add(BitmapFrame.Create(image));
                        encodert.Save(stream);
                     

                        stream.Dispose();
                        fs.Dispose();
                        
                        break;
                    default:
                        throw new ApplicationException("Error: FileDest extension no supported");
                        
                }             

            }
            catch (Exception e)
            {
                throw (e);
            }
        }

        private byte[] GetBytesFromBitmapSource(BitmapSource bmp)
        {
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = width * ((bmp.Format.BitsPerPixel + 7) / 8);

            byte[] pixels = new byte[height * stride];

            bmp.CopyPixels(pixels, stride, 0);

            return pixels;
        }
       
    }
}
