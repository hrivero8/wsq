using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wsqm
{
    internal class NISTConstants
    {
        /* From nistcom.h */
        public const String NCM_EXT = "ncm";
        public const String NCM_HEADER = "NIST_COM";        /* mandatory */
        public const String NCM_PIX_WIDTH = "PIX_WIDTH";       /* mandatory */
        public const String NCM_PIX_HEIGHT = "PIX_HEIGHT";      /* mandatory */
        public const String NCM_PIX_DEPTH = "PIX_DEPTH";       /* 1,8,24 (mandatory)*/
        public const String NCM_PPI = "PPI";             /* -1 if unknown (mandatory)*/
        public const String NCM_COLORSPACE = "COLORSPACE";      /* RGB,YCbCr,GRAY */
        public const String NCM_N_CMPNTS = "NUM_COMPONENTS";  /* [1..4] (mandatory w/hv_factors)*/
        public const String NCM_HV_FCTRS = "HV_FACTORS";      /* H0,V0:H1,V1:...*/
        public const String NCM_INTRLV = "INTERLEAVE";      /* 0,1 (mandatory w/depth=24) */
        public const String NCM_COMPRESSION = "COMPRESSION";     /* NONE,JPEGB,JPEGL,WSQ */
        public const String NCM_JPEGB_QUAL = "JPEGB_QUALITY";   /* [20..95] */
        public const String NCM_JPEGL_PREDICT = "JPEGL_PREDICT"; /* [1..7] */
        public const String NCM_WSQ_RATE = "WSQ_BITRATE";     /* ex. .75,2.25 (-1.0 if unknown)*/
        public const String NCM_LOSSY = "LOSSY";           /* 0,1 */

        public const String NCM_HISTORY = "HISTORY";         /* ex. SD historical data */
        public const String NCM_FING_CLASS = "FING_CLASS";      /* ex. A,L,R,S,T,W */
        public const String NCM_SEX = "SEX";             /* m,f */
        public const String NCM_SCAN_TYPE = "SCAN_TYPE";       /* l,i */
        public const String NCM_FACE_POS = "FACE_POS";        /* f,p */
        public const String NCM_AGE = "AGE";
        public const String NCM_SD_ID = "SD_ID";           /* 4,9,10,14,18 */

    }
}
