using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wsqm
{
    internal class WSQConstants : NISTConstants
    {
        /*used to "mask out" n number of bits from data stream*/
        public static int[] BITMASK = { 0x00, 0x01, 0x03, 0x07, 0x0f, 0x1f, 0x3f, 0x7f, 0xff };

        public const int MAX_DHT_TABLES = 8;
        public const int MAX_HUFFBITS = 16;
        public const int MAX_HUFFCOUNTS_WSQ = 256;

        public const int MAX_HUFFCOEFF = 74;/* -73 .. +74 */
        public const int MAX_HUFFZRUN = 100;

        public const int MAX_HIFILT = 7;
        public const int MAX_LOFILT = 9;


        public const int W_TREELEN = 20;
        public const int Q_TREELEN = 64;

        /* WSQ Marker Definitions */
        public const int SOI_WSQ = 0xffa0;
        public const int EOI_WSQ = 0xffa1;
        public const int SOF_WSQ = 0xffa2;
        public const int SOB_WSQ = 0xffa3;
        public const int DTT_WSQ = 0xffa4;
        public const int DQT_WSQ = 0xffa5;
        public const int DHT_WSQ = 0xffa6;
        public const int DRT_WSQ = 0xffa7;
        public const int COM_WSQ = 0xffa8;

        public const int STRT_SUBBAND_2 = 19;
        public const int STRT_SUBBAND_3 = 52;
        public const int MAX_SUBBANDS = 64;
        public const int NUM_SUBBANDS = 60;
        public const int STRT_SUBBAND_DEL = NUM_SUBBANDS;
        public const int STRT_SIZE_REGION_2 = 4;
        public const int STRT_SIZE_REGION_3 = 51;

        public const int COEFF_CODE = 0;
        public const int RUN_CODE = 1;

        public const float VARIANCE_THRESH = 1.01f;

        /* Case for getting ANY marker. */
        public const int ANY_WSQ = 0xffff;
        public const int TBLS_N_SOF = 2;
        public const int TBLS_N_SOB = TBLS_N_SOF + 2;


    }
}
