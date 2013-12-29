using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//******
using System.IO;



namespace Wsqm
{
    internal class Encoder : WSQConstants
    {
        public void encode(DataOutput dataOutput, cBitmap bitmap,
                                double bitRate, String[] comments)
        {
            try
            {
                encode(dataOutput, bitmap, bitRate, null, comments);
            }
            catch (IOException e)
            {
                throw (e);
            }

        }

        public void encode(DataOutput dataOutput, cBitmap _bitmap, double bitRate, Dictionary<String, String> metadata, String[] comments)
        {
            try
            {
                cBitmapWithMetadata bitmap;
                if (_bitmap.GetType() == typeof(cBitmapWithMetadata))
                {
                    bitmap = (cBitmapWithMetadata)_bitmap;
                }
                else
                {
                    bitmap = new cBitmapWithMetadata(_bitmap.getPixels(), _bitmap.getWidth(), _bitmap.getHeight(), _bitmap.getPpi(), _bitmap.getDepth(), _bitmap.getLossyflag());
                }
                if (metadata != null)
                {
                    foreach (KeyValuePair<String, String> entry in metadata)
                    {
                        bitmap.getMetadata().Add(entry.Key, entry.Value);
                    }
                }
                //if (metadata != null)
                //    bitmap.getMetadata().putAll(metadata);
                if (comments != null)
                    foreach (String s in comments)
                        if (s != null)
                            bitmap.getComments().Add(s);

                float[] fdata;	/* floating point pixel image  */
                int[] qdata;	/* quantized image pointer     */
                Ref<int> qsize = new Ref<int>(), qsize1 = new Ref<int>(), qsize2 = new Ref<int>(), qsize3 = new Ref<int>();  /* quantized block sizes */
                WSQHelper.HuffCode[] hufftable;
                //Ref<int[]> huffbits = new Ref<int[]>(), huffvalues = new Ref<int[]>(); /* huffman code parameters */
                Ref<int> huffbits = new Ref<int>(), huffvalues = new Ref<int>(); /* huffman code parameters */

                /* Convert image pixels to floating point. */
                Ref<float> m_shift = new Ref<float>(), r_scale = new Ref<float>();
                fdata = convertImageToFloat(bitmap.getPixels(), bitmap.getWidth(), bitmap.getHeight(), m_shift, r_scale);

                Token token = new Token();

                /* Build WSQ decomposition trees */
                WSQHelper.buildWSQTrees(token, bitmap.getWidth(), bitmap.getHeight());

                /* WSQ decompose the image */
                wsqDecompose(token, fdata, bitmap.getWidth(), bitmap.getHeight(), token.tableDTT.hifilt, MAX_HIFILT, token.tableDTT.lofilt, MAX_LOFILT);

                /* Set compression ratio and 'q' to zero. */
                token.quant_vals.cr = 0;
                token.quant_vals.q = 0.0f;

                /* Assign specified r-bitrate into quantization structure. */
                token.quant_vals.r = (float)bitRate;

                /* Compute subband variances. */
                variance(token, fdata, bitmap.getWidth(), bitmap.getHeight());

                /* Quantize the floating point pixmap. */

                qdata = quantize(token, qsize, fdata, bitmap.getWidth(), bitmap.getHeight());

                /* Compute quantized WSQ subband block sizes */
                quant_block_sizes(token, qsize1, qsize2, qsize3);

                if (qsize.value != qsize1.value + qsize2.value + qsize3.value)
                {
                    throw new System.InvalidOperationException("ERROR : wsq_encode_1 : problem w/quantization block sizes");
                }

                /* Add a Start Of Image (SOI) marker to the WSQ buffer. */
                dataOutput.writeShort(SOI_WSQ);

                putc_nistcom_wsq(dataOutput, bitmap, (float)bitRate, metadata, comments);

                /* Store the Wavelet filter taps to the WSQ buffer. */
                putc_transform_table(dataOutput, token.tableDTT.lofilt, MAX_LOFILT, token.tableDTT.hifilt, MAX_HIFILT);

                /* Store the quantization parameters to the WSQ buffer. */
                putc_quantization_table(dataOutput, token);

                /* Store a frame header to the WSQ buffer. */
                putc_frame_header_wsq(dataOutput, bitmap.getWidth(), bitmap.getHeight(), m_shift.value, r_scale.value);

                /* ENCODE Block 1 */

                /* Compute Huffman table for Block 1. */
                hufftable = gen_hufftable_wsq(token, huffbits, huffvalues, qdata, 0, new int[] { qsize1.value });



                /* Store Huffman table for Block 1 to WSQ buffer. */
                //putc_huffman_table(dataOutput, DHT_WSQ, 0, huffbits.value, huffvalues.value);
                putc_huffman_table(dataOutput, WSQConstants.DHT_WSQ, 0, huffbits.valueT, huffvalues.valueT);

                /* Store Block 1's header to WSQ buffer. */
                putc_block_header(dataOutput, 0);

                /* Compress Block 1 data. */
                compress_block(dataOutput, qdata, 0, qsize1.value, MAX_HUFFCOEFF, MAX_HUFFZRUN, hufftable);

                /* ENCODE Block 2 */

                /* Compute  Huffman table for Blocks 2 & 3. */
                hufftable = gen_hufftable_wsq(token, huffbits, huffvalues, qdata, qsize1.value, new int[] { qsize2.value, qsize3.value });


                /* Store Huffman table for Blocks 2 & 3 to WSQ buffer. */
                //putc_huffman_table(dataOutput, DHT_WSQ, 1, huffbits.value, huffvalues.value);
                putc_huffman_table(dataOutput, WSQConstants.DHT_WSQ, 1, huffbits.valueT, huffvalues.valueT);

                /* Store Block 2's header to WSQ buffer. */
                putc_block_header(dataOutput, 1);

                /* Compress Block 2 data. */
                compress_block(dataOutput, qdata, qsize1.value, qsize2.value, MAX_HUFFCOEFF, MAX_HUFFZRUN, hufftable);

                /* ENCODE Block 3 */

                /* Store Block 3's header to WSQ buffer. */
                putc_block_header(dataOutput, 1);

                /* Compress Block 3 data. */
                compress_block(dataOutput, qdata, qsize1.value + qsize2.value, qsize3.value, MAX_HUFFCOEFF, MAX_HUFFZRUN, hufftable);

                /* Add a End Of Image (EOI) marker to the WSQ buffer. */
                dataOutput.writeShort(EOI_WSQ);

                File.WriteAllBytes(dataOutput.RutaDestino, dataOutput.ObtenerBuffer());

            }
            catch (IOException e)
            {
                throw (e);
            }

        }

        /// <summary>
        /// Corresponds to conv_img_2_flt in wsq_encoder.c.
        /// Converts an image's unsigned character pixels to floating point values in the range +/- 128.0.
        /// </summary>
        /// <param name="data">input image, values should be regarded as unsigned</param>
        /// <param name="width">width of input image</param>
        /// <param name="height">height of input image</param>
        /// <param name="m_shift">output for shift, needs be non-null and length >= 1</param>
        /// <param name="r_scale">output for scale, needs to be non-null and length >= 1</param>
        /// <returns></returns>
        private float[] convertImageToFloat(byte[] data, int width, int height, Ref<float> m_shift, Ref<float> r_scale)
        {
            if (data == null) { throw new ArgumentException("Image cannot be null"); }
            int cnt;                     /* pixel cnt */
            int sum;                     /* sum of pixel values */
            int low, high;               /* low/high pixel values */
            float low_diff, high_diff;   /* new low/high pixels values shifting */

            float[] fip = new float[data.Length];

            sum = 0;
            low = 255;
            high = 0;
            for (cnt = 0; cnt < data.Length; cnt++)
            {
                if ((data[cnt] & 0xFF) > high)
                {
                    high = data[cnt] & 0xFF;
                }
                if ((data[cnt] & 0xFF) < low)
                {
                    low = data[cnt] & 0xFF;
                }
                sum += (data[cnt] & 0xFF);
            }

            float mean = (float)sum / (float)data.Length;
            m_shift.value = mean;

            low_diff = m_shift.value - low;
            high_diff = high - m_shift.value;

            if (low_diff >= high_diff)
            {
                r_scale.value = low_diff;
            }
            else
            {
                r_scale.value = high_diff;
            }

            r_scale.value /= (float)128.0;

            for (cnt = 0; cnt < data.Length; cnt++)
            {
                fip[cnt] = ((float)(data[cnt] & 0xFF) - m_shift.value) / r_scale.value;
            }
            return fip;
        }
        /// <summary>
        /// WSQ decompose the image.  NOTE: this routine modifies and returns
        /// the results in "fdata"  
        /// </summary>
        /// <param name="token"></param>
        /// <param name="fdata"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="hifilt"></param>
        /// <param name="hisz"></param>
        /// <param name="lofilt"></param>
        /// <param name="losz"></param>
        private void wsqDecompose(Token token, float[] fdata, int width, int height, float[] hifilt, int hisz, float[] lofilt, int losz)
        {
            int num_pix = width * height;
            /* Allocate temporary floating point pixmap. */
            float[] fdata1 = new float[num_pix];

            /* Compute the Wavelet image decomposition. */
            for (int node = 0; node < token.wtree.Length; node++)
            {
                int fdataBse = (token.wtree[node].y * width) + token.wtree[node].x;

                getLets(fdata1, fdata, 0, fdataBse, token.wtree[node].leny, token.wtree[node].lenx,
                        width, 1, hifilt, hisz, lofilt, losz, token.wtree[node].invrw);
                getLets(fdata, fdata1, fdataBse, 0, token.wtree[node].lenx, token.wtree[node].leny,
                        1, width, hifilt, hisz, lofilt, losz, token.wtree[node].invcl);
            }

        }

        private void getLets(float[] newdata,     /* image pointers for creating subband splits */
             float[] olddata,
             int newIndex,
             int oldIndex,
             int len1,       /* temporary length parameters */
             int len2,
             int pitch,      /* pitch gives next row_col to filter */
             int stride,    /*           stride gives next pixel to filter */
             float[] hi,
             int hsz,   /* NEW */
             float[] lo,      /* filter coefficients */
             int lsz,   /* NEW */
             int inv)        /* spectral inversion? */
        {
            if (newdata == null) { throw new ArgumentException("newdata == null"); }
            if (olddata == null) { throw new ArgumentException("olddata == null"); }
            if (lo == null) { throw new ArgumentException("lo == null"); }

            int lopass, hipass;	/* pointers of where to put lopass
                                   and hipass filter outputs */
            int p0, p1;		/* pointers to image pixels used */
            int pix, rw_cl;		/* pixel counter and row/column counter */
            int i, da_ev;		/* even or odd row/column of pixels */
            int fi_ev;
            int loc, hoc, nstr, pstr;
            int llen, hlen;
            int lpxstr, lspxstr;
            int lpx, lspx;
            int hpxstr, hspxstr;
            int hpx, hspx;
            int olle, ohle;
            int olre, ohre;
            int lle, lle2;
            int lre, lre2;
            int hle, hle2;
            int hre, hre2;

            da_ev = len2 % 2;
            fi_ev = lsz % 2;

            if (fi_ev != 0)
            {
                loc = (lsz - 1) / 2;
                hoc = (hsz - 1) / 2 - 1;
                olle = 0;
                ohle = 0;
                olre = 0;
                ohre = 0;
            }
            else
            {
                loc = lsz / 2 - 2;
                hoc = hsz / 2 - 2;
                olle = 1;
                ohle = 1;
                olre = 1;
                ohre = 1;

                if (loc == -1)
                {
                    loc = 0;
                    olle = 0;
                }
                if (hoc == -1)
                {
                    hoc = 0;
                    ohle = 0;
                }

                for (i = 0; i < hsz; i++)
                {
                    hi[i] *= -1.0f;
                }
            }

            pstr = stride;
            nstr = -pstr;

            if (da_ev != 0)
            {
                llen = (len2 + 1) / 2;
                hlen = llen - 1;
            }
            else
            {
                llen = len2 / 2;
                hlen = llen;
            }

            for (rw_cl = 0; rw_cl < len1; rw_cl++)
            {
                if (inv != 0)
                {
                    hipass = newIndex + rw_cl * pitch;
                    lopass = hipass + hlen * stride;
                }
                else
                {
                    lopass = newIndex + rw_cl * pitch;
                    hipass = lopass + llen * stride;
                }

                p0 = oldIndex + rw_cl * pitch;
                p1 = p0 + (len2 - 1) * stride;

                lspx = p0 + (loc * stride);
                lspxstr = nstr;
                lle2 = olle;
                lre2 = olre;
                hspx = p0 + (hoc * stride);
                hspxstr = nstr;
                hle2 = ohle;
                hre2 = ohre;
                for (pix = 0; pix < hlen; pix++)
                {
                    lpxstr = lspxstr;
                    lpx = lspx;
                    lle = lle2;
                    lre = lre2;
                    newdata[lopass] = olddata[lpx] * lo[0];
                    for (i = 1; i < lsz; i++)
                    {
                        if (lpx == p0)
                        {
                            if (lle != 0)
                            {
                                lpxstr = 0;
                                lle = 0;
                            }
                            else
                                lpxstr = pstr;
                        }
                        if (lpx == p1)
                        {
                            if (lre != 0)
                            {
                                lpxstr = 0;
                                lre = 0;
                            }
                            else
                            {
                                lpxstr = nstr;
                            }
                        }
                        lpx += lpxstr;
                        newdata[lopass] += olddata[lpx] * lo[i];
                    }
                    lopass += stride;

                    hpxstr = hspxstr;
                    hpx = hspx;
                    hle = hle2;
                    hre = hre2;
                    newdata[hipass] = olddata[hpx] * hi[0];
                    for (i = 1; i < hsz; i++)
                    {
                        if (hpx == p0)
                        {
                            if (hle != 0)
                            {
                                hpxstr = 0;
                                hle = 0;
                            }
                            else
                            {
                                hpxstr = pstr;
                            }
                        }
                        if (hpx == p1)
                        {
                            if (hre != 0)
                            {
                                hpxstr = 0;
                                hre = 0;
                            }
                            else
                            {
                                hpxstr = nstr;
                            }
                        }
                        hpx += hpxstr;
                        newdata[hipass] += olddata[hpx] * hi[i];
                    }
                    hipass += stride;

                    for (i = 0; i < 2; i++)
                    {
                        if (lspx == p0)
                        {
                            if (lle2 != 0)
                            {
                                lspxstr = 0;
                                lle2 = 0;
                            }
                            else
                            {
                                lspxstr = pstr;
                            }
                        }
                        lspx += lspxstr;
                        if (hspx == p0)
                        {
                            if (hle2 != 0)
                            {
                                hspxstr = 0;
                                hle2 = 0;
                            }
                            else
                            {
                                hspxstr = pstr;
                            }
                        }
                        hspx += hspxstr;
                    }
                }
                if (da_ev != 0)
                {
                    lpxstr = lspxstr;
                    lpx = lspx;
                    lle = lle2;
                    lre = lre2;
                    newdata[lopass] = olddata[lpx] * lo[0];
                    for (i = 1; i < lsz; i++)
                    {
                        if (lpx == p0)
                        {
                            if (lle != 0)
                            {
                                lpxstr = 0;
                                lle = 0;
                            }
                            else
                            {
                                lpxstr = pstr;
                            }
                        }
                        if (lpx == p1)
                        {
                            if (lre != 0)
                            {
                                lpxstr = 0;
                                lre = 0;
                            }
                            else
                            {
                                lpxstr = nstr;
                            }
                        }
                        lpx += lpxstr;
                        newdata[lopass] += olddata[lpx] * lo[i];
                    }
                    lopass += stride;
                }
            }
            if (fi_ev == 0)
            {
                for (i = 0; i < hsz; i++)
                {
                    hi[i] *= -1.0f;
                }
            }
        }

        /// <summary>
        /// This routine calculates the variances of the subbands.
        /// </summary>
        /// <param name="token">contains quant_vals quantization parameters and quantization "tree" and treelen.NOTE: This routine will write to var field inside quant_vals </param>
        /// <param name="fip">image pointer</param>
        /// <param name="width">image width</param>
        /// <param name="height">image height</param>
        private void variance(Token token, float[] fip, int width, int height)
        {
            int fp;                 /* temp image pointer */
            int lenx = 0, leny = 0; /* dimensions of area to calculate variance */
            int skipx, skipy;       /* pixels to skip to get to area for variance calculation */
            int row, col;           /* dimension counters */
            float ssq;              /* sum of squares */
            float sum2;             /* variance calculation parameter */
            float sum_pix;          /* sum of pixels */
            float vsum;             /* variance sum for subbands 0-3 */

            vsum = 0;
            for (int cvr = 0; cvr < 4; cvr++)
            {
                fp = ((token.qtree[cvr].y) * width) + token.qtree[cvr].x;
                ssq = 0.0f;
                sum_pix = 0.0f;

                skipx = token.qtree[cvr].lenx / 8;
                skipy = (9 * token.qtree[cvr].leny) / 32;

                lenx = (3 * token.qtree[cvr].lenx) / 4;
                leny = (7 * token.qtree[cvr].leny) / 16;

                fp += (skipy * width) + skipx;
                for (row = 0; row < leny; row++, fp += (width - lenx))
                {
                    for (col = 0; col < lenx; col++)
                    {
                        sum_pix += fip[fp];
                        ssq += fip[fp] * fip[fp];
                        fp++;
                    }
                }
                sum2 = (sum_pix * sum_pix) / (lenx * leny);
                token.quant_vals.var[cvr] = (float)((ssq - sum2) / ((lenx * leny) - 1.0f));
                vsum += token.quant_vals.var[cvr];
            }

            //This part is needed to comply with WSQ 3.1
            if (vsum < 20000.0)
            {
                for (int cvr = 0; cvr < WSQConstants.NUM_SUBBANDS; cvr++)
                {
                    fp = (token.qtree[cvr].y * width) + token.qtree[cvr].x;
                    ssq = 0;
                    sum_pix = 0;

                    lenx = token.qtree[cvr].lenx;
                    leny = token.qtree[cvr].leny;

                    for (row = 0; row < leny; row++, fp += (width - lenx))
                    {
                        for (col = 0; col < lenx; col++)
                        {
                            sum_pix += fip[fp];
                            ssq += fip[fp] * fip[fp];
                            fp++;
                        }
                    }
                    sum2 = (sum_pix * sum_pix) / (lenx * leny);
                    token.quant_vals.var[cvr] = (float)((ssq - sum2) / ((lenx * leny) - 1.0));
                }
            }
            else
            {
                for (int cvr = 4; cvr < WSQConstants.NUM_SUBBANDS; cvr++)
                {
                    fp = (token.qtree[cvr].y * width) + token.qtree[cvr].x;
                    ssq = 0;
                    sum_pix = 0;

                    skipx = token.qtree[cvr].lenx / 8;
                    skipy = (9 * token.qtree[cvr].leny) / 32;

                    lenx = (3 * token.qtree[cvr].lenx) / 4;
                    leny = (7 * token.qtree[cvr].leny) / 16;

                    fp += (skipy * width) + skipx;
                    for (row = 0; row < leny; row++, fp += (width - lenx))
                    {
                        for (col = 0; col < lenx; col++)
                        {
                            sum_pix += fip[fp];
                            ssq += fip[fp] * fip[fp];
                            fp++;
                        }
                    }
                    sum2 = (sum_pix * sum_pix) / (lenx * leny);
                    token.quant_vals.var[cvr] = (float)((ssq - sum2) / ((lenx * leny) - 1.0));
                }
            }

        }

        /// <summary>
        /// This routine quantizes the wavelet subbands.
        /// </summary>
        /// <param name="token">contains quantization parameters, quantization tree, size of quantization tree</param>
        /// <param name="qsize">output size</param>
        /// <param name="fip">floating point image pointer</param>
        /// <param name="width">image width</param>
        /// <param name="height">image height</param>
        /// <returns>quantized image</returns>
        private int[] quantize(Token token, Ref<int> qsize, float[] fip, int width, int height)
        {
            int row, col;          /* temp image characteristic parameters */
            float zbin;            /* zero bin size */
            float[] A = new float[WSQConstants.NUM_SUBBANDS]; /* subband "weights" for quantization */
            float[] m = new float[WSQConstants.NUM_SUBBANDS]; /* subband size to image size ratios */
            /* (reciprocal of FBI spec for 'm')  */
            float m1, m2, m3;      /* reciprocal constants for 'm' */
            float[] sigma = new float[WSQConstants.NUM_SUBBANDS]; /* square root of subband variances */
            int[] K0 = new int[WSQConstants.NUM_SUBBANDS];  /* initial list of subbands w/variance >= thresh */
            int[] K1 = new int[WSQConstants.NUM_SUBBANDS];  /* working list of subbands */
            int K, nK;           /* pointers to sets of subbands */
            bool[] NP = new bool[WSQConstants.NUM_SUBBANDS];  /* current subbounds with nonpositive bit rates. */
            int K0len;             /* number of subbands in K0 */
            int Klen, nKlen;       /* number of subbands in other subband lists */
            int NPlen;             /* number of subbands flagged in NP */
            float S;               /* current frac of subbands w/positive bit rate */
            float q;               /* current proportionality constant */
            float P;               /* product of 'q/Q' ratios */

            /* Set up 'A' table. */
            for (int cnt = 0; cnt < WSQConstants.STRT_SUBBAND_3; cnt++)
            {
                A[cnt] = 1.0f;
            }
            A[WSQConstants.STRT_SUBBAND_3 /*52*/] = 1.32f;
            A[WSQConstants.STRT_SUBBAND_3 + 1 /*53*/] = 1.08f;
            A[WSQConstants.STRT_SUBBAND_3 + 2 /*54*/] = 1.42f;
            A[WSQConstants.STRT_SUBBAND_3 + 3 /*55*/] = 1.08f;
            A[WSQConstants.STRT_SUBBAND_3 + 4 /*56*/] = 1.32f;
            A[WSQConstants.STRT_SUBBAND_3 + 5 /*57*/] = 1.42f;
            A[WSQConstants.STRT_SUBBAND_3 + 6 /*58*/] = 1.08f;
            A[WSQConstants.STRT_SUBBAND_3 + 7 /*59*/] = 1.08f;

            for (int cnt = 0; cnt < WSQConstants.MAX_SUBBANDS; cnt++)
            {
                token.quant_vals.qbss[cnt] = 0.0f;
                token.quant_vals.qzbs[cnt] = 0.0f;
            }

            /* Set up 'Q1' (prime) table. */
            for (int cnt = 0; cnt < WSQConstants.NUM_SUBBANDS; cnt++)
            {
                if (token.quant_vals.var[cnt] < WSQConstants.VARIANCE_THRESH)
                {
                    token.quant_vals.qbss[cnt] = 0.0f;
                }
                else
                {
                    /* NOTE: q has been taken out of the denominator in the next */
                    /*       2 formulas from the original code. */
                    if (cnt < WSQConstants.STRT_SIZE_REGION_2 /*4*/)
                    {
                        token.quant_vals.qbss[cnt] = 1.0f;
                    }
                    else
                    {
                        token.quant_vals.qbss[cnt] = 10.0f / (A[cnt] * (float)Math.Log(token.quant_vals.var[cnt]));
                    }
                }
            }

            /* Set up output buffer. */
            int[] sip = new int[width * height];
            int sptr = 0;

            /* Set up 'm' table (these values are the reciprocal of 'm' in the FBI spec). */
            m1 = 1.0f / 1024.0f;
            m2 = 1.0f / 256.0f;
            m3 = 1.0f / 16.0f;
            for (int cnt = 0; cnt < WSQConstants.STRT_SIZE_REGION_2; cnt++)
            {
                m[cnt] = m1;
            }
            for (int cnt = WSQConstants.STRT_SIZE_REGION_2; cnt < WSQConstants.STRT_SIZE_REGION_3; cnt++)
            {
                m[cnt] = m2;
            }
            for (int cnt = WSQConstants.STRT_SIZE_REGION_3; cnt < WSQConstants.NUM_SUBBANDS; cnt++)
            {
                m[cnt] = m3;
            }

            /* Initialize 'K0' and 'K1' lists. */
            K0len = 0;
            for (int cnt = 0; cnt < WSQConstants.NUM_SUBBANDS; cnt++)
            {
                if (token.quant_vals.var[cnt] >= WSQConstants.VARIANCE_THRESH)
                {
                    K0[K0len] = cnt;
                    K1[K0len++] = cnt;
                    /* Compute square root of subband variance. */
                    sigma[cnt] = (float)Math.Sqrt(token.quant_vals.var[cnt]);
                }
            }
            K = 0;
            Klen = K0len;

            while (true)
            {
                /* Compute new 'S' */
                S = 0.0f;
                for (int i = 0; i < Klen; i++)
                {
                    /* Remember 'm' is the reciprocal of spec. */
                    S += m[K1[K + i]];
                }

                /* Compute product 'P' */
                P = 1.0f;
                for (int i = 0; i < Klen; i++)
                {
                    /* Remember 'm' is the reciprocal of spec. */
                    P *= Convert.ToSingle(Math.Pow(Convert.ToDouble(sigma[K1[K + i]] / token.quant_vals.qbss[K1[K + i]]), Convert.ToDouble(m[K1[K + i]])));
                }

                /* Compute new 'q' */
                q = ((float)Math.Pow(2, ((token.quant_vals.r / S) - 1.0f)) / 2.5f) / (float)Math.Pow(P, (1.0f / S));

                /* Flag subbands with non-positive bitrate. */
                NP = new bool[WSQConstants.NUM_SUBBANDS];
                NPlen = 0;
                for (int i = 0; i < Klen; i++)
                {
                    if ((token.quant_vals.qbss[K1[K + i]] / q) >= (5.0 * sigma[K1[K + i]]))
                    {
                        NP[K1[K + i]] = true;
                        NPlen++;
                    }
                }

                /* If list of subbands with non-positive bitrate is empty ... */
                if (NPlen == 0)
                {
                    /* Then we are done, so break from while loop. */
                    break;
                }

                /* Assign new subband set to previous set K minus subbands in set NP. */
                nK = 0;
                nKlen = 0;
                for (int i = 0; i < Klen; i++)
                {
                    if (!NP[K1[K + i]])
                    {
                        K1[nK + nKlen++] = K1[K + i];
                    }
                }

                /* Assign new set as K. */
                K = nK;
                Klen = nKlen;
            }

            /* Flag subbands that are in set 'K0' (the very first set). */
            nK = 0;

            //Arrays.fill(K1, nK, WSQConstants.NUM_SUBBANDS, 0); // was: memset(nK, 0, NUM_SUBBANDS * sizeof(int));
            Fill(K1, nK, WSQConstants.NUM_SUBBANDS, 0); // was: memset(nK, 0, NUM_SUBBANDS * sizeof(int));
            for (int i = 0; i < K0len; i++)
            {
                K1[nK + K0[i]] = 1; /* MO: was = TRUE */
            }
            /* Set 'Q' values. */
            for (int cnt = 0; cnt < WSQConstants.NUM_SUBBANDS; cnt++)
            {
                if (K1[nK + cnt] != 0)
                {
                    token.quant_vals.qbss[cnt] /= q;
                }
                else
                {
                    token.quant_vals.qbss[cnt] = 0.0f;
                }
                token.quant_vals.qzbs[cnt] = 1.2f * token.quant_vals.qbss[cnt];
            }

            /* Now ready to compute and store bin widths for subbands. */
            for (int cnt = 0; cnt < WSQConstants.NUM_SUBBANDS; cnt++)
            {
                int fptr = (token.qtree[cnt].y * width) + token.qtree[cnt].x;

                if (token.quant_vals.qbss[cnt] != 0.0f)
                {

                    zbin = token.quant_vals.qzbs[cnt] / 2.0f;

                    for (row = 0; row < token.qtree[cnt].leny; row++, fptr += width - token.qtree[cnt].lenx)
                    {
                        for (col = 0; col < token.qtree[cnt].lenx; col++)
                        {
                            if (-zbin <= fip[fptr] && fip[fptr] <= zbin)
                            {
                                sip[sptr] = 0;
                            }
                            else if (fip[fptr] > 0.0f)
                            {
                                sip[sptr] = (int)(((fip[fptr] - zbin) / token.quant_vals.qbss[cnt]) + 1.0f);
                            }
                            else
                            {
                                sip[sptr] = (int)(((fip[fptr] + zbin) / token.quant_vals.qbss[cnt]) - 1.0f);
                            }
                            sptr++;
                            fptr++;
                        }
                    }
                }
            }

            qsize.value = sptr;

            return sip;

        }
        private void Fill<T>(T[] array, int fromIndex, int toIndex, T value)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }
            if (fromIndex < 0 || fromIndex > toIndex)
            {
                throw new ArgumentOutOfRangeException("fromIndex");
            }
            if (toIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException("toIndex");
            }
            for (var i = fromIndex; i < toIndex; i++)
            {
                array[i] = value;
            }
        }

        /// <summary>
        /// Compute quantized WSQ subband block sizes.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="oqsize1"></param>
        /// <param name="oqsize2"></param>
        /// <param name="oqsize3"></param>
        private void quant_block_sizes(Token token, Ref<int> oqsize1, Ref<int> oqsize2, Ref<int> oqsize3)
        {
            int qsize1, qsize2, qsize3;
            int node;

            /* Compute temporary sizes of 3 WSQ subband blocks. */
            qsize1 = token.wtree[14].lenx * token.wtree[14].leny;
            qsize2 = (token.wtree[5].leny * token.wtree[1].lenx) +
                    (token.wtree[4].lenx * token.wtree[4].leny);
            qsize3 = (token.wtree[2].lenx * token.wtree[2].leny) +
                    (token.wtree[3].lenx * token.wtree[3].leny);

            /* Adjust size of quantized WSQ subband blocks. */
            for (node = 0; node < WSQConstants.STRT_SUBBAND_2; node++)
            {
                if (token.quant_vals.qbss[node] == 0.0f)
                {
                    qsize1 -= (token.qtree[node].lenx * token.qtree[node].leny);
                }
            }

            for (node = WSQConstants.STRT_SUBBAND_2; node < WSQConstants.STRT_SUBBAND_3; node++)
            {
                if (token.quant_vals.qbss[node] == 0.0f)
                {
                    qsize2 -= (token.qtree[node].lenx * token.qtree[node].leny);
                }
            }

            for (node = WSQConstants.STRT_SUBBAND_3; node < WSQConstants.STRT_SUBBAND_DEL; node++)
            {
                if (token.quant_vals.qbss[node] == 0.0f)
                {
                    qsize3 -= (token.qtree[node].lenx * token.qtree[node].leny);
                }
            }

            oqsize1.value = qsize1;
            oqsize2.value = qsize2;
            oqsize3.value = qsize3;


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataOutput"></param>
        /// <param name="marker"></param>
        /// <param name="tableId"></param>
        /// <param name="huffbits"></param>
        /// <param name="huffvalues"></param>
        private void putc_huffman_table(DataOutput dataOutput, int marker, int tableId, int[] huffbits, int[] huffvalues)
        {
            /* DHT */
            dataOutput.writeShort(marker);

            /* "value(2) + table id(1) + bits(16)" */
            int table_len = 3 + WSQConstants.MAX_HUFFBITS;
            int values_offset = table_len;
            for (int i = 0; i < WSQConstants.MAX_HUFFBITS; i++)
            {
                table_len += huffbits[i];   /* values size */
            }

            /* Table Len */
            dataOutput.writeShort(table_len & 0xFFFF);

            /* Table ID */
            dataOutput.writeByte(tableId & 0xFF);

            /* Huffbits (MAX_HUFFBITS) */
            for (int i = 0; i < WSQConstants.MAX_HUFFBITS; i++)
            {
                dataOutput.writeByte(huffbits[i] & 0xFF);
            }

            /* Huffvalues (MAX_HUFFCOUNTS) */
            for (int i = 0; i < table_len - values_offset; i++)
            {
                dataOutput.writeByte(huffvalues[i] & 0xFF);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataOutput"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="m_shift">image shifting parameter</param>
        /// <param name="r_scale">image scaling parameter</param>
        private void putc_frame_header_wsq(DataOutput dataOutput, int width, int height, float m_shift, float r_scale)
        {
            try
            {
                float flt_tmp;         /* temp variable */
                int scale_ex;         /* exponent scaling parameter */

                int shrt_dat;       /* temp variable */
                dataOutput.writeShort(WSQConstants.SOF_WSQ); /* +2 = 2 */

                /* size of frame header */
                dataOutput.writeShort(17);

                /* black pixel */
                dataOutput.writeByte(0); /* +1 = 3 */

                /* white pixel */
                dataOutput.writeByte(255); /* +1 = 4 */

                dataOutput.writeShort(height); /* +2 = 5 */
                dataOutput.writeShort(width); /* +2 = 7 */

                flt_tmp = m_shift;
                scale_ex = 0;
                if (flt_tmp != 0.0)
                {
                    while (flt_tmp < 65535)
                    {
                        scale_ex += 1;
                        flt_tmp *= 10;
                    }
                    scale_ex -= 1;
                    shrt_dat = (int)Math.Round(flt_tmp / 10.0f);
                }
                else
                {
                    shrt_dat = 0;
                }
                dataOutput.writeByte(scale_ex & 0xFF); /* +1 = 9 */
                dataOutput.writeShort(shrt_dat); /* +2 = 11 */

                flt_tmp = r_scale;
                scale_ex = 0;
                if (flt_tmp != 0.0)
                {
                    while (flt_tmp < 65535)
                    {
                        scale_ex += 1;
                        flt_tmp *= 10;
                    }
                    scale_ex -= 1;
                    shrt_dat = (int)Math.Round(flt_tmp / 10.0f);
                }
                else
                {
                    shrt_dat = 0;
                }
                dataOutput.writeByte(scale_ex); /* +1 = 12 */
                dataOutput.writeShort(shrt_dat); /* +2 = 13 */

                dataOutput.writeByte(0); /* +1 = 15 */
                dataOutput.writeShort(0); /* +2 = 17 */
            }
            catch (Exception e)
            {
                throw (e);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataOutput"></param>
        /// <param name="lofilt"></param>
        /// <param name="losz"></param>
        /// <param name="hifilt"></param>
        /// <param name="hisz"></param>
        private void putc_transform_table(DataOutput dataOutput, float[] lofilt, int losz, float[] hifilt, int hisz)
        {
            long coef;				/* filter coefficient indicator */
            long int_dat;			/* temp variable */
            long int_dat_aux;

            //int coef;				/* filter coefficient indicator */
            //int int_dat;			/* temp variable */
            double dbl_tmp, dbl_aux;			/* temp variable */
            int scale_ex, sign;	/* exponent scaling and sign parameters */

            /* FIXME how big can hisz,losz get */
            if (losz < 0 || losz > Int32.MaxValue / 2)
            {
                throw new InvalidOperationException("Writing transform table: losz out of range");
            }
            if (hisz < 0 || hisz > Int32.MaxValue / 2)
            {
                throw new InvalidOperationException("Writing transform table: hisz out of range");
            }
            dataOutput.writeShort(WSQConstants.DTT_WSQ);

            /* table size */
            dataOutput.writeShort(58);

            /* number analysis lowpass coefficients */
            dataOutput.writeByte(losz);

            /* number analysis highpass coefficients */
            dataOutput.writeByte(hisz);
            int_dat_aux = 0;
            for (coef = (losz >> 1); (long)(coef & 0xFFFFFFFFL) < (long)losz; coef++)
            {
                dbl_tmp = lofilt[(int)(coef & 0xFFFFFFFFL)];
                if (dbl_tmp >= 0.0)
                {
                    sign = 0;
                }
                else
                {
                    sign = 1;
                    dbl_tmp *= -1.0;
                }
                scale_ex = 0;
                if (dbl_tmp == 0.0)
                {
                    int_dat = 0;
                }
                else if (dbl_tmp < 4294967295.0)
                {
                    while (dbl_tmp < 4294967295.0)
                    {
                        scale_ex += 1;
                        dbl_tmp *= 10.0;
                    }
                    scale_ex -= 1;

                    //TODO: La siguiente linea queda reemplazada por
                    //el bloque "nueva implementacion"

                    //int_dat = (int)Math.Round(dbl_tmp / 10.0);


                    //nueva implementacion
                    dbl_aux = Math.Round(dbl_tmp / 10.0, MidpointRounding.AwayFromZero);
                    int_dat_aux = (long)dbl_aux;
                    int_dat = (int)int_dat_aux;
                }
                else
                {
                    dbl_tmp = lofilt[(int)(coef & 0xFFFFFFFFL)];
                    throw new InvalidOperationException("ERROR: putc_transform_table : lofilt[%d] to high at %f"/* , coef, dbl_tmp */);
                }

                dataOutput.writeByte(sign & 0xFF);
                dataOutput.writeByte(scale_ex & 0xFF);
                dataOutput.writeInt((int)(int_dat & 0xFFFFFFFFL));

            }

            for (coef = (hisz >> 1); (int)(coef & 0xFFFFFFFFL) < (long)hisz; coef++)
            {
                dbl_tmp = hifilt[(int)(coef & 0xFFFFFFFFL)];
                if (dbl_tmp >= 0.0)
                {
                    sign = 0;
                }
                else
                {
                    sign = 1;
                    dbl_tmp *= -1.0;
                }
                scale_ex = 0;
                if (dbl_tmp == 0.0)
                {
                    int_dat = 0;
                }
                else if (dbl_tmp < 4294967295.0)
                {
                    while (dbl_tmp < 4294967295.0)
                    {
                        scale_ex += 1;
                        dbl_tmp *= 10.0;
                    }
                    scale_ex -= 1;
                    //TODO: La siguiente linea queda reemplazada por
                    //el bloque "nueva implementacion"

                    //int_dat = (int)Math.Round(dbl_tmp / 10.0);

                    //nueva implementacion
                    dbl_aux = Math.Round(dbl_tmp / 10.0, MidpointRounding.AwayFromZero);
                    int_dat_aux = (long)dbl_aux;
                    int_dat = (int)int_dat_aux;
                }
                else
                {
                    dbl_tmp = hifilt[(int)(coef & 0xFFFFFFFFL)];
                    throw new InvalidOperationException("ERROR: putc_transform_table : hifilt[" + coef + "] to high at " + dbl_tmp + "");
                }
                dataOutput.writeByte(sign & 0xFF);
                dataOutput.writeByte(scale_ex & 0xFF);
                dataOutput.writeInt((int)(int_dat & 0xFFFFFFFFL));
            }
        }

        /// <summary>
        ///  Stores quantization table in the output buffer.
        /// </summary>
        /// <param name="dataOutput"></param>
        /// <param name="token"></param>
        private void putc_quantization_table(DataOutput dataOutput, Token token)
        {
            try
            {
                int scale_ex, scale_ex2; /* exponent scaling parameters */
                int shrt_dat, shrt_dat2;  /* temp variables */
                float flt_tmp;            /* temp variable */
                double dbl_aux;
                long int_data_aux;

                dataOutput.writeShort(WSQConstants.DQT_WSQ);

                /* table size */
                dataOutput.writeShort(389);

                /* exponent scaling value */
                dataOutput.writeByte(2);

                /* quantizer bin center parameter */
                dataOutput.writeShort(44);

                for (int sub = 0; sub < 64; sub++)
                {
                    if (sub >= 0 && sub < 60)
                    {
                        if (token.quant_vals.qbss[sub] != 0.0f)
                        {
                            flt_tmp = token.quant_vals.qbss[sub];
                            scale_ex = 0;
                            if (flt_tmp < 65535)
                            {
                                while (flt_tmp < 65535)
                                {
                                    scale_ex += 1;
                                    flt_tmp *= 10;
                                }
                                scale_ex -= 1;
                                //TODO: La siguiente linea queda reemplazada por
                                //el bloque "nueva implementacion"

                                //shrt_dat = (int)Math.Round(flt_tmp / 10.0);

                                //nueva implementacion                              
                                dbl_aux = Math.Round(flt_tmp / 10.0, MidpointRounding.AwayFromZero);
                                int_data_aux = (long)dbl_aux;
                                shrt_dat = (int)int_data_aux;
                            }
                            else
                            {
                                flt_tmp = token.quant_vals.qbss[sub];
                                throw new InvalidOperationException("ERROR : putc_quantization_table : Q[%d] to high at %f"); //, sub, flt_tmp);
                            }

                            flt_tmp = token.quant_vals.qzbs[sub];
                            scale_ex2 = 0;
                            if (flt_tmp < 65535)
                            {
                                while (flt_tmp < 65535)
                                {
                                    scale_ex2 += 1;
                                    flt_tmp *= 10;
                                }
                                scale_ex2 -= 1;
                                //TODO: La siguiente linea queda reemplazada por
                                //el bloque "nueva implementacion"

                                //shrt_dat2 = (int)Math.Round(flt_tmp / 10.0);


                                //nueva implementacion
                                dbl_aux = Math.Round(flt_tmp / 10.0, MidpointRounding.AwayFromZero);
                                int_data_aux = (long)dbl_aux;
                                shrt_dat2 = (int)int_data_aux;
                            }
                            else
                            {
                                flt_tmp = token.quant_vals.qzbs[sub];
                                throw new ArgumentException("ERROR : putc_quantization_table : Z[%d] to high at %f"); //, sub, flt_tmp);
                            }
                        }
                        else
                        {
                            scale_ex = 0;
                            scale_ex2 = 0;
                            shrt_dat = 0;
                            shrt_dat2 = 0;
                        }
                    }
                    else
                    {
                        scale_ex = 0;
                        scale_ex2 = 0;
                        shrt_dat = 0;
                        shrt_dat2 = 0;
                    }

                    dataOutput.writeByte(scale_ex & 0xFF);
                    dataOutput.writeShort(shrt_dat & 0xFFFF);
                    dataOutput.writeByte(scale_ex2 & 0xFF);
                    dataOutput.writeShort(shrt_dat2 & 0xFFFF);
                }
            }
            catch (IOException e)
            {
                throw (e);
            }

        }

        /// <summary>
        /// Stores block header to the output buffer.
        /// </summary>
        /// <param name="dataOutput">token</param>
        /// <param name="table">huffman table indicator</param>
        private void putc_block_header(DataOutput dataOutput, int table)
        {
            try
            {
                dataOutput.writeShort(WSQConstants.SOB_WSQ);

                /* block header size */
                dataOutput.writeShort(3);

                dataOutput.writeByte(table & 0xFF);
            }
            catch (IOException e)
            {
                throw (e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataOutput"></param>
        /// <param name="bitmap"></param>
        /// <param name="r_bitrate"></param>
        /// <param name="metadata"></param>
        /// <param name="comments"></param>
        private void putc_nistcom_wsq(DataOutput dataOutput,
                                            cBitmap bitmap,
                                            float r_bitrate,
                                            Dictionary<String, String> metadata,
                                            String[] comments)
        {
            Dictionary<String, String> nistcom = new Dictionary<String, String>();
            //These attributes will be filled later
            nistcom.Add(NISTConstants.NCM_HEADER, "---");
            nistcom.Add(NISTConstants.NCM_PIX_WIDTH, "---");
            nistcom.Add(NISTConstants.NCM_PIX_HEIGHT, "---");
            nistcom.Add(NISTConstants.NCM_PIX_DEPTH, "---");
            nistcom.Add(NISTConstants.NCM_PPI, "---");
            nistcom.Add(NISTConstants.NCM_LOSSY, "---");
            nistcom.Add(NISTConstants.NCM_COLORSPACE, "---");
            nistcom.Add(NISTConstants.NCM_COMPRESSION, "---");
            nistcom.Add(NISTConstants.NCM_WSQ_RATE, "---");

            if (metadata != null)
            {
                foreach (KeyValuePair<String, String> entry in metadata)
                {
                    nistcom.Add(entry.Key, entry.Value);
                }
            }


            //TODO:VALIDAR SI Convert.ToString(bitmap.HorizontalResolution*bitmap.VerticalResolution) ES CORRECTO
            nistcom[NISTConstants.NCM_HEADER] = nistcom.Count.ToString();
            nistcom[NISTConstants.NCM_PIX_WIDTH] = bitmap.getWidth().ToString();
            nistcom[NISTConstants.NCM_PIX_HEIGHT] = bitmap.getHeight().ToString();
            //nistcom[NISTConstants.NCM_PPI] = Convert.ToString(bitmap.HorizontalResolution * bitmap.VerticalResolution);
            nistcom[NISTConstants.NCM_PPI] = Convert.ToString(500);
            nistcom[NISTConstants.NCM_PIX_DEPTH] = "8"; //WSQ has always 8 bpp
            nistcom[NISTConstants.NCM_LOSSY] = "1"; //WSQ is always lossy
            nistcom[NISTConstants.NCM_COLORSPACE] = "GRAY";
            nistcom[NISTConstants.NCM_COMPRESSION] = "WSQ";
            nistcom[NISTConstants.NCM_WSQ_RATE] = r_bitrate.ToString();
            //nistcom.Add(NISTConstants.NCM_HEADER     , nistcom.Count.ToString());
            //nistcom.Add(NISTConstants.NCM_PIX_WIDTH  , bitmap.Width.ToString());
            //nistcom.Add(NISTConstants.NCM_PIX_HEIGHT , bitmap.Height.ToString());
            //nistcom.Add(NISTConstants.NCM_PPI        , Convert.ToString(bitmap.HorizontalResolution*bitmap.VerticalResolution));
            //nistcom.Add(NISTConstants.NCM_PIX_DEPTH  , "8"); //WSQ has always 8 bpp
            //nistcom.Add(NISTConstants.NCM_LOSSY      , "1"); //WSQ is always lossy
            //nistcom.Add(NISTConstants.NCM_COLORSPACE , "GRAY");
            //nistcom.Add(NISTConstants.NCM_COMPRESSION, "WSQ");
            //nistcom.Add(NISTConstants.NCM_WSQ_RATE   , r_bitrate.ToString());

            putc_comment(dataOutput, WSQConstants.COM_WSQ, fetToString(nistcom));
            if (comments != null)
                foreach (String s in comments)
                    if (s != null)
                        putc_comment(dataOutput, WSQConstants.COM_WSQ, s);

        }

        /// <summary>
        /// Puts comment field in output buffer.
        /// MO, from tableio.c in ffpis_img
        /// </summary>
        /// <param name="dataOutput"></param>
        /// <param name="marker"></param>
        /// <param name="comment"></param>
        private void putc_comment(DataOutput dataOutput, int marker, String comment)
        {
            try
            {
                dataOutput.writeShort(marker);

                /* comment size */
                int hdr_size = 2 + comment.Length;
                dataOutput.writeShort(hdr_size & 0xFFFF);

                System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();


                //sbyte[] sbytes = Array.ConvertAll(encoding.GetBytes(comment), q => Convert.ToSByte(q));

                dataOutput.write(encoding.GetBytes(comment)); // FIXME: should be UTF-8. Check FBI spec.
                //dataOutput.write(sbytes); // FIXME: should be UTF-8. Check FBI spec.
            }
            catch (Exception e)
            {
                throw (e);
            }

        }

        /// <summary>
        /// Generate a Huffman code table for a quantized data block.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="ohuffbits">should contain one byte[] reference</param>
        /// <param name="ohuffvalues">should contain one byte[] reference</param>
        /// <param name="sip"></param>
        /// <param name="offset"></param>
        /// <param name="block_sizes"></param>
        /// <returns></returns>
        private WSQHelper.HuffCode[] gen_hufftable_wsq(Token token, Ref<int> ohuffbits, Ref<int> ohuffvalues, int[] sip, int offset, int[] block_sizes)
        {
            int[] codesize;       /* code sizes to use */
            Ref<int> last_size = new Ref<int>();       /* last huffvalue */
            int[] huffbits;     /* huffbits values */
            int[] huffvalues;   /* huffvalues */
            int[] huffcounts;     /* counts for each huffman category */
            int[] huffcounts2;    /* counts for each huffman category */
            WSQHelper.HuffCode[] hufftable1, hufftable2;  /* hufftables */

            huffcounts = count_block(WSQConstants.MAX_HUFFCOUNTS_WSQ, sip, offset, block_sizes[0], WSQConstants.MAX_HUFFCOEFF, WSQConstants.MAX_HUFFZRUN);

            for (int i = 1; i < block_sizes.Length; i++)
            {
                huffcounts2 = count_block(WSQConstants.MAX_HUFFCOUNTS_WSQ, sip, offset + block_sizes[i - 1], block_sizes[i], WSQConstants.MAX_HUFFCOEFF, WSQConstants.MAX_HUFFZRUN);

                for (int j = 0; j < WSQConstants.MAX_HUFFCOUNTS_WSQ; j++)
                {
                    huffcounts[j] += huffcounts2[j];
                }
            }

            codesize = find_huff_sizes(huffcounts, WSQConstants.MAX_HUFFCOUNTS_WSQ);

            /* tells if codesize is greater than MAX_HUFFBITS */
            Ref<bool> adjust = new Ref<bool>();

            huffbits = find_num_huff_sizes(adjust, codesize, WSQConstants.MAX_HUFFCOUNTS_WSQ);

            if (adjust.value)
            {
                sort_huffbits(huffbits);
            }

            huffvalues = sort_code_sizes(codesize, WSQConstants.MAX_HUFFCOUNTS_WSQ);

            hufftable1 = build_huffsizes(last_size, huffbits, WSQConstants.MAX_HUFFCOUNTS_WSQ);

            build_huffcodes(hufftable1);
            check_huffcodes_wsq(hufftable1, last_size.value);

            hufftable2 = build_huffcode_table(hufftable1, last_size.value, huffvalues, WSQConstants.MAX_HUFFCOUNTS_WSQ);

            //ohuffbits.value = huffbits;
            //ohuffvalues.value = huffvalues;
            //ohuffbits.write(huffbits);
            //ohuffvalues.write(huffvalues);

            ohuffbits.valueT = huffbits;
            ohuffvalues.valueT = huffvalues;
            return hufftable2;

        }


        /// <summary>
        /// This routine counts the number of occurences of each category
        /// in the huffman coding tables. 
        /// </summary>
        /// <param name="max_huffcounts">maximum number of counts</param>
        /// <param name="sip">quantized data</param>
        /// <param name="sip_offset">offset into sip</param>
        /// <param name="sip_siz">size of block being compressed</param>
        /// <param name="MaxCoeff">maximum values for coefficients</param>
        /// <param name="MaxZRun">maximum zero runs</param>
        /// <returns></returns>
        private int[] count_block(
            // int **ocounts,     /* output count for each huffman catetory */
                int max_huffcounts,
                int[] sip,
                int sip_offset,
                int sip_siz,
                int MaxCoeff,
                int MaxZRun)
        {
            int[] counts;         /* count for each huffman category */
            int LoMaxCoeff;        /* lower (negative) MaxCoeff limit */
            int pix;             /* temp pixel pointer */
            int rcnt = 0, state;  /* zero run count and if current pixel
                                         is in a zero run or just a coefficient */
            int cnt;               /* pixel counter */

            if (MaxCoeff < 0 || MaxCoeff > 0xffff)
            {
                throw new InvalidOperationException("ERROR : compress_block : MaxCoeff out of range.");
            }
            if (MaxZRun < 0 || MaxZRun > 0xffff)
            {
                throw new InvalidOperationException("ERROR : compress_block : MaxZRun out of range.");
            }
            /* Ininitalize vector of counts to 0. */
            counts = new int[max_huffcounts + 1];
            /* Set last count to 1. */
            counts[max_huffcounts] = 1;

            LoMaxCoeff = 1 - MaxCoeff;
            state = WSQConstants.COEFF_CODE;
            for (cnt = sip_offset; cnt < sip_siz; cnt++)
            {
                pix = sip[cnt];
                switch (state)
                {

                    case WSQConstants.COEFF_CODE:   /* for runs of zeros */
                        if (pix == 0)
                        {
                            state = WSQConstants.RUN_CODE;
                            rcnt = 1;
                            break;
                        }
                        if (pix > MaxCoeff)
                        {
                            if (pix > 255)
                                counts[103]++; /* 16bit pos esc */
                            else
                                counts[101]++; /* 8bit pos esc */
                        }
                        else if (pix < LoMaxCoeff)
                        {
                            if (pix < -255)
                                counts[104]++; /* 16bit neg esc */
                            else
                                counts[102]++; /* 8bit neg esc */
                        }
                        else
                            counts[pix + 180]++; /* within table */
                        break;

                    case WSQConstants.RUN_CODE:  /* get length of zero run */
                        if (pix == 0 && rcnt < 0xFFFF)
                        {
                            ++rcnt;
                            break;
                        }
                        /* limit rcnt to avoid EOF problem in bitio.c */
                        if (rcnt <= MaxZRun)
                        {
                            counts[rcnt]++;  /** log zero run length **/
                        }
                        else if (rcnt <= 0xFF)
                        {
                            counts[105]++;
                        }
                        else if (rcnt <= 0xFFFF)
                        {
                            counts[106]++; /* 16bit zrun esc */
                        }
                        else
                        {
                            throw new InvalidOperationException("ERROR: count_block : Zrun to long in count block.");
                        }

                        if (pix != 0)
                        {
                            if (pix > MaxCoeff)
                            { /** log current pix **/
                                if (pix > 255)
                                {
                                    counts[103]++; /* 16bit pos esc */
                                }
                                else
                                {
                                    counts[101]++; /* 8bit pos esc */
                                }
                            }
                            else if (pix < LoMaxCoeff)
                            {
                                if (pix < -255)
                                {
                                    counts[104]++; /* 16bit neg esc */
                                }
                                else
                                {
                                    counts[102]++; /* 8bit neg esc */
                                }
                            }
                            else
                            {
                                counts[pix + 180]++; /* within table */
                            }
                            state = WSQConstants.COEFF_CODE;
                        }
                        else
                        {
                            rcnt = 1;
                            state = WSQConstants.RUN_CODE;
                        }
                        break;
                }
            }
            if (state == WSQConstants.RUN_CODE)
            { /** log zero run length **/
                if (rcnt <= MaxZRun)
                {
                    counts[rcnt]++;
                }
                else if (rcnt <= 0xFF)
                {
                    counts[105]++;
                }
                else if (rcnt <= 0xFFFF)
                {
                    counts[106]++; /* 16bit zrun esc */
                }
                else
                {
                    throw new InvalidOperationException("ERROR: count_block : Zrun to long in count block.");
                }
            }

            return counts;

        }

        /// <summary>
        /// Routine to find number of codes of each size.
        /// </summary>
        /// <param name="adjust">should be boolean array of size 1</param>
        /// <param name="codesize"></param>
        /// <param name="max_huffcounts"></param>
        /// <returns></returns>
        private int[] find_num_huff_sizes(Ref<bool> adjust, int[] codesize, int max_huffcounts)
        {
            adjust.value = false;

            /* Allocate 2X desired number of bits due to possible codesize. */
            int[] bits = new int[2 * WSQConstants.MAX_HUFFBITS];

            for (int i = 0; i < max_huffcounts; i++)
            {
                if (codesize[i] != 0)
                {
                    bits[codesize[i] - 1]++;
                }
                if (codesize[i] > WSQConstants.MAX_HUFFBITS)
                {
                    adjust.value = true;
                }
            }
            return bits;

        }

        /// <summary>
        /// routine to sort the huffman code sizes
        /// </summary>
        /// <param name="codesize"></param>
        /// <param name="max_huffcounts"></param>
        /// <returns></returns>
        private int[] sort_code_sizes(int[] codesize, int max_huffcounts)
        {
            /*defines order of huffman codelengths in relation to the code sizes*/
            int[] values = new int[max_huffcounts + 1];
            int i2 = 0;
            for (int i = 1; i <= (WSQConstants.MAX_HUFFBITS << 1); i++)
            {
                for (int i3 = 0; i3 < max_huffcounts; i3++)
                {
                    if (codesize[i3] == i)
                    {
                        values[i2] = i3;
                        i2++;
                    }
                }
            }
            return values;

        }

        /// <summary>
        /// This routine defines the huffman code sizes for each difference category
        /// </summary>
        /// <param name="temp_size"></param>
        /// <param name="huffbits"></param>
        /// <param name="max_huffcounts"></param>
        /// <returns></returns>
        private WSQHelper.HuffCode[] build_huffsizes(Ref<int> temp_size, int[] huffbits, int max_huffcounts)
        {
            int number_of_codes = 1;     /*the number codes for a given code size*/

            /* table of huffman codes and sizes */
            WSQHelper.HuffCode[] huffcode_table = new WSQHelper.HuffCode[max_huffcounts + 1];
            for (int i = 0; i < huffcode_table.Length; i++) { huffcode_table[i] = new WSQHelper.HuffCode(); }

            temp_size.value = 0;

            for (int code_size = 1; code_size <= WSQConstants.MAX_HUFFBITS; code_size++)
            {
                while (number_of_codes <= huffbits[code_size - 1])
                {
                    huffcode_table[temp_size.value].size = code_size;
                    (temp_size.value)++;
                    number_of_codes++;
                }
                number_of_codes = 1;
            }
            huffcode_table[temp_size.value].size = 0;
            return huffcode_table;

        }

        /// <summary>
        /// Routine to optimize code sizes by frequency of difference values.
        /// </summary>
        /// <param name="freq">should be array of length 1</param>
        /// <param name="max_huffcounts">TODO</param>
        /// <returns></returns>
        private int[] find_huff_sizes(int[] freq, int max_huffcounts)
        {
            int value1;          /* smallest and next smallest frequency*/
            int value2;          /* of difference occurrence in the largest difference category*/

            /*codesizes for each category*/
            int[] codesize = new int[max_huffcounts + 1];

            /*pointer used to generate codesizes*/
            int[] others = new int[max_huffcounts + 1];

            for (int i = 0; i <= max_huffcounts; i++)
            {
                others[i] = -1;
            }

            while (true)
            {

                int[] values = find_least_freq(freq, max_huffcounts);
                value1 = values[0];
                value2 = values[1];

                if (value2 == -1)
                {
                    break;
                }

                freq[value1] += freq[value2];
                freq[value2] = 0;

                codesize[value1]++;
                while (others[value1] != -1)
                {
                    value1 = others[value1];
                    codesize[value1]++;
                }
                others[value1] = value2;
                codesize[value2]++;

                while (others[value2] != -1)
                {
                    value2 = others[value2];
                    codesize[value2]++;
                }
            }

            return codesize;

        }

        /// <summary>
        /// Routine to find the largest difference with the least frequency value
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="max_huffcounts"></param>
        /// <returns>array with value1, value2</returns>
        private int[] find_least_freq(int[] freq, int max_huffcounts)
        {
            int code_temp;       /*store code*/
            int value_temp;      /*store size*/
            int code2 = Int32.MaxValue;   /*next smallest frequency in largest diff category*/
            int code1 = Int32.MaxValue;   /*smallest frequency in largest difference category*/
            int set = 1;         /*flag first two non-zero frequency values*/

            int value1 = -1;
            int value2 = -1;

            for (int i = 0; i <= max_huffcounts; i++)
            {
                if (freq[i] == 0)
                {
                    continue;
                }
                if (set == 1)
                {
                    code1 = freq[i];
                    value1 = i;
                    set++;
                    continue;
                }
                if (set == 2)
                {
                    code2 = freq[i];
                    value2 = i;
                    set++;
                }
                code_temp = freq[i];
                value_temp = i;
                if (code1 < code_temp && code2 < code_temp)
                {
                    continue;
                }
                if ((code_temp < code1) || (code_temp == code1 && value_temp > value1))
                {
                    code2 = code1;
                    value2 = value1;
                    code1 = code_temp;
                    value1 = value_temp;
                    continue;
                }
                if ((code_temp < code2) || (code_temp == code2 && value_temp > value2))
                {
                    code2 = code_temp;
                    value2 = value_temp;
                }
            }
            return new int[] { value1, value2 };

        }

        /// <summary>
        /// routine to insure that no huffman code size is greater than 16
        /// </summary>
        /// <param name="bits"></param>
        private void sort_huffbits(int[] bits)
        {
            int i, j;
            int l1, l2, l3;

            l3 = WSQConstants.MAX_HUFFBITS << 1;       /* 32 */
            l1 = l3 - 1;                /* 31 */
            l2 = WSQConstants.MAX_HUFFBITS - 1;      /* 15 */

            int[] tbits = new int[l3];

            for (i = 0; i < WSQConstants.MAX_HUFFBITS << 1; i++)
            {
                tbits[i] = bits[i];
            }

            for (i = l1; i > l2; i--)
            {
                while (tbits[i] > 0)
                {
                    j = i - 2;
                    while (tbits[j] == 0)
                    {
                        j--;
                    }
                    tbits[i] -= 2;
                    tbits[i - 1] += 1;
                    tbits[j + 1] += 2;
                    tbits[j] -= 1;
                }
                tbits[i] = 0;
            }

            while (tbits[i] == 0)
            {
                i--;
            }

            tbits[i] -= 1;

            for (i = 0; i < WSQConstants.MAX_HUFFBITS << 1; i++)
            {
                bits[i] = (byte)tbits[i];
            }

            for (i = WSQConstants.MAX_HUFFBITS; i < l3; i++)
            {
                if (bits[i] > 0)
                {
                    throw new InvalidOperationException("ERROR : sort_huffbits : Code length of %d is greater than 16.");
                }
            }

        }

        /// <summary>
        /// This routine defines the huffman codes needed for each difference category
        /// </summary>
        /// <param name="huffcode_table"></param>
        private void build_huffcodes(WSQHelper.HuffCode[] huffcode_table)
        {
            int pointer = 0;/*pointer to code word information*/
            int temp_code = 0;/*used to construct code word*/

            int temp_size = huffcode_table[0].size;/*used to construct code size*/

            do
            {
                do
                {
                    huffcode_table[pointer].code = temp_code;
                    temp_code++;
                    pointer++;
                } while (huffcode_table[pointer].size == temp_size);

                if (huffcode_table[pointer].size == 0)
                {
                    return;
                }

                do
                {
                    temp_code <<= 1;
                    temp_size++;
                } while (huffcode_table[pointer].size != temp_size);
            } while (huffcode_table[pointer].size == temp_size);

        }
        private void check_huffcodes_wsq(WSQHelper.HuffCode[] hufftable, int last_size)
        {
            Boolean all_ones;

            for (int i = 0; i < last_size; i++)
            {
                all_ones = true;
                for (int k = 0; (k < hufftable[i].size) && all_ones; k++)
                {
                    all_ones = (all_ones && (((hufftable[i].code >> k) & 0x0001) != 0));
                }
                if (all_ones)
                {
                    throw new InvalidOperationException("WARNING: A code in the hufftable contains an "
                            + "all 1's code. This image may still be "
                            + "decodable. It is not compliant with "
                            + "the WSQ specification.");
                }
            }

        }
        /// <summary>
        /// routine to sort huffman codes and sizes
        /// </summary>
        /// <param name="in_huffcode_table"></param>
        /// <param name="last_size"></param>
        /// <param name="values"></param>
        /// <param name="max_huffcounts"></param>
        /// <returns></returns>
        private WSQHelper.HuffCode[] build_huffcode_table(WSQHelper.HuffCode[] in_huffcode_table,
                int last_size, int[] values, int max_huffcounts)
        {
            WSQHelper.HuffCode[] new_huffcode_table = new WSQHelper.HuffCode[max_huffcounts + 1];
            for (int i = 0; i < new_huffcode_table.Length; i++) { new_huffcode_table[i] = new WSQHelper.HuffCode(); }

            for (int size = 0; size < last_size; size++)
            {
                new_huffcode_table[values[size]].code = in_huffcode_table[size].code;
                new_huffcode_table[values[size]].size = in_huffcode_table[size].size;
            }

            return new_huffcode_table;

        }

        /// <summary>
        /// Routine "codes" the quantized image using the huffman tables.
        /// </summary>
        /// <param name="dataOutput"></param>
        /// <param name="sip">quantized image</param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="MaxCoeff">Maximum values for coefficients</param>
        /// <param name="MaxZRun">Maximum zero runs</param>
        /// <param name="codes"></param>
        private void compress_block(DataOutput dataOutput,
                int[] sip,          /*  */
                int offset,
                int length,
                int MaxCoeff,  /*   */
                int MaxZRun,   /*  */
                WSQHelper.HuffCode[] codes)
        {
            try
            {
                int LoMaxCoeff;        /* lower (negative) MaxCoeff limit */
                int pix;             /* temp pixel pointer */
                int rcnt = 0, state;  /* zero run count and if current pixel
                                         is in a zero run or just a coefficient */
                int cnt;               /* pixel counter */

                if (MaxCoeff < 0 || MaxCoeff > 0xffff)
                {
                    throw new InvalidOperationException("ERROR : compress_block : MaxCoeff out of range.");
                }
                if (MaxZRun < 0 || MaxZRun > 0xffff)
                {
                    throw new InvalidOperationException("ERROR : compress_block : MaxZRun out of range.");
                }
                LoMaxCoeff = 1 - MaxCoeff;

                Ref<int> outbit = new Ref<int>(7);
                Ref<int> bytes = new Ref<int>(0);
                Ref<int> bits = new Ref<int>(0);

                state = WSQConstants.COEFF_CODE;
                for (cnt = offset; cnt < length; cnt++)
                {
                    pix = sip[cnt];

                    //if (cnt == 6480)
                    //{
                    //    int ii;
                    //    ii = 1;
                    //}

                    switch (state)
                    {

                        case WSQConstants.COEFF_CODE:
                            if (pix == 0)
                            {
                                state = WSQConstants.RUN_CODE;
                                rcnt = 1;
                                break;
                            }
                            if (pix > MaxCoeff)
                            {
                                if (pix > 255)
                                {
                                    /* 16bit pos esc */
                                    write_bits(dataOutput, codes[103].size, codes[103].code, outbit, bits, bytes);
                                    write_bits(dataOutput, 16, pix, outbit, bits, bytes);
                                }
                                else
                                {
                                    /* 8bit pos esc */
                                    write_bits(dataOutput, codes[101].size, codes[101].code, outbit, bits, bytes);
                                    write_bits(dataOutput, 8, pix, outbit, bits, bytes);
                                }
                            }
                            else if (pix < LoMaxCoeff)
                            {
                                if (pix < -255)
                                {
                                    /* 16bit neg esc */
                                    write_bits(dataOutput, codes[104].size, codes[104].code, outbit, bits, bytes);
                                    write_bits(dataOutput, 16, -(pix), outbit, bits, bytes);
                                }
                                else
                                {
                                    /* 8bit neg esc */
                                    write_bits(dataOutput, codes[102].size, codes[102].code, outbit, bits, bytes);
                                    write_bits(dataOutput, 8, -(pix), outbit, bits, bytes);
                                }
                            }
                            else
                            {
                                /* within table */
                                write_bits(dataOutput, codes[pix + 180].size, codes[pix + 180].code, outbit, bits, bytes);
                            }
                            break;

                        case WSQConstants.RUN_CODE:
                            if (pix == 0 && rcnt < 0xFFFF)
                            {
                                ++rcnt;
                                break;
                            }
                            if (rcnt <= (int)MaxZRun)
                            {
                                /* log zero run length */
                                write_bits(dataOutput, codes[rcnt].size, codes[rcnt].code, outbit, bits, bytes);
                            }
                            else if (rcnt <= 0xFF)
                            {
                                /* 8bit zrun esc */
                                write_bits(dataOutput, codes[105].size, codes[105].code, outbit, bits, bytes);
                                write_bits(dataOutput, 8, rcnt, outbit, bits, bytes);
                            }
                            else if (rcnt <= 0xFFFF)
                            {
                                /* 16bit zrun esc */
                                write_bits(dataOutput, codes[106].size, codes[106].code, outbit, bits, bytes);
                                write_bits(dataOutput, 16, rcnt, outbit, bits, bytes);
                            }
                            else
                            {
                                throw new InvalidOperationException("ERROR : compress_block : zrun too large.");
                            }

                            if (pix != 0)
                            {
                                if (pix > MaxCoeff)
                                {
                                    /** log current pix **/
                                    if (pix > 255)
                                    {
                                        /* 16bit pos esc */
                                        write_bits(dataOutput, codes[103].size, codes[103].code, outbit, bits, bytes);
                                        write_bits(dataOutput, 16, pix, outbit, bits, bytes);
                                    }
                                    else
                                    {
                                        /* 8bit pos esc */
                                        write_bits(dataOutput, codes[101].size, codes[101].code, outbit, bits, bytes);
                                        write_bits(dataOutput, 8, pix, outbit, bits, bytes);
                                    }
                                }
                                else if (pix < LoMaxCoeff)
                                {
                                    if (pix < -255)
                                    {
                                        /* 16bit neg esc */
                                        write_bits(dataOutput, codes[104].size, codes[104].code, outbit, bits, bytes);
                                        write_bits(dataOutput, 16, -pix, outbit, bits, bytes);
                                    }
                                    else
                                    {
                                        /* 8bit neg esc */
                                        write_bits(dataOutput, codes[102].size, codes[102].code, outbit, bits, bytes);
                                        write_bits(dataOutput, 8, -pix, outbit, bits, bytes);
                                    }
                                }
                                else
                                {
                                    /* within table */
                                    write_bits(dataOutput, codes[pix + 180].size, codes[pix + 180].code, outbit, bits, bytes);
                                }
                                state = WSQConstants.COEFF_CODE;
                            }
                            else
                            {
                                rcnt = 1;
                                state = WSQConstants.RUN_CODE;
                            }
                            break;
                    }
                }
                if (state == WSQConstants.RUN_CODE)
                {
                    if (rcnt <= MaxZRun)
                    {
                        write_bits(dataOutput, codes[rcnt].size, codes[rcnt].code, outbit, bits, bytes);
                    }
                    else if (rcnt <= 0xFF)
                    {
                        write_bits(dataOutput, codes[105].size, codes[105].code, outbit, bits, bytes);
                        write_bits(dataOutput, 8, rcnt, outbit, bits, bytes);
                    }
                    else if (rcnt <= 0xFFFF)
                    {
                        write_bits(dataOutput, codes[106].size, codes[106].code, outbit, bits, bytes);
                        write_bits(dataOutput, 16, rcnt, outbit, bits, bytes);
                    }
                    else
                    {
                        throw new Exception("ERROR : compress_block : zrun2 too large.");
                    }
                }

                flush_bits(dataOutput, outbit, bits, bytes);
            }
            catch (IOException e)
            {
                throw (e);
            }

        }

        private String fetToString(Dictionary<String, String> fet)
        {
            try
            {
                StringBuilder result = new StringBuilder();

                foreach (KeyValuePair<String, String> entry in fet)
                {
                    if (entry.Key == null) continue;
                    if (entry.Value == null) continue;
                    String key = System.Web.HttpUtility.UrlEncode(entry.Key, Encoding.UTF8);
                    String value = System.Web.HttpUtility.UrlEncode(entry.Value, Encoding.UTF8);

                    result.Append(key);
                    result.Append(" ");
                    result.Append(value);
                    result.Append("\n");
                }


                return result.ToString();
            }
            catch (Exception e)
            {
                throw new SystemException(e.Message);
            }

        }

        /// <summary>
        /// Routine to write "compressed" bits to output buffer.
        /// </summary>
        /// <param name="outbuf"></param>
        /// <param name="size">numbers bits of code to write into buffer</param>
        /// <param name="code">info to write into buffer</param>
        /// <param name="outbit">current bit location in out buffer byte</param>
        /// <param name="bits">byte to write to output buffer</param>
        /// <param name="bytes"></param>
        private void write_bits(
                DataOutput outbuf,
                int size,
                int code,
                Ref<int> outbit,
                Ref<int> bits,
                Ref<int> bytes)
        {
            try
            {
                int num;
                num = size;

                for (--num; num >= 0; num--)
                {
                    bits.value <<= 1;
                    bits.value |= (((code >> num) & 0x0001)) & 0xFF;

                    if (--(outbit.value) < 0)
                    {
                        outbuf.write(bits.value);
                        if ((bits.value & 0xFF) == 0xFF)
                        {
                            outbuf.write(0);
                            bytes.value++;
                        }
                        bytes.value++;
                        outbit.value = 7;
                        bits.value = 0;
                    }
                }
            }
            catch (IOException e)
            {
                throw (e);
            }

        }

        /// <summary>
        /// Routine to "flush" left over bits in last
        /// byte after compressing a block. 
        /// </summary>
        /// <param name="outbuf">output data buffer</param>
        /// <param name="outbit">current bit location in out buffer byte</param>
        /// <param name="bits">byte to write to output buffer</param>
        /// <param name="bytes">count of number bytes written to the buffer</param>
        private void flush_bits(
                DataOutput outbuf,
                Ref<int> outbit,
                Ref<int> bits,
                Ref<int> bytes)
        {
            try
            {

                int cnt; /* temp counter */

                if (outbit.value != 7)
                {
                    for (cnt = outbit.value; cnt >= 0; cnt--)
                    {
                        bits.value <<= 1;
                        bits.value |= 0x01;
                    }
                    outbuf.write(bits.value);
                    if (bits.value == 0xFF)
                    {
                        bits.value = 0;
                        outbuf.write(0);
                        bytes.value++;
                    }
                    bytes.value++;
                    outbit.value = 7;
                    bits.value = 0;
                }
            }
            catch (IOException e)
            {
                throw (e);
            }

        }
    }
}
