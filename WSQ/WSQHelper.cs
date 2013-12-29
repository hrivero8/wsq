using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wsqm
{
    internal class WSQHelper : WSQConstants
    {
       
                      
        public class HuffCode
        {
            public int size;
            public int code;
        }
        public class HeaderFrm
        {
            public int black;
            public int white;
            public int width;
            public int height;
            public float mShift;
            public float rScale;
            public int wsqEncoder;
            public int software;
        }

        public class HuffmanTable
        {
            public int tableLen;
            public int bytesLeft;
            public int tableId;
            public int[] huffbits;
            public int[] huffvalues;
        }
        

        public static void buildWSQTrees(Token token, int width, int height)
        {
            /* Build a W-TREE structure for the image. */
            buildWTree(token, WSQHelper.W_TREELEN, width, height);
            /* Build a Q-TREE structure for the image. */
            buildQTree(token, WSQHelper.Q_TREELEN);
        }
        public static void buildWTree(Token token, int wtreelen, int width, int height)
        {
            int lenx, lenx2, leny, leny2;  /* starting lengths of sections of
                                              the image being split into subbands */
            token.wtree = new WavletTree[wtreelen];
            for (int i = 0; i < wtreelen; i++)
            {
                token.wtree[i] = new WavletTree();
                token.wtree[i].invrw = 0;
                token.wtree[i].invcl = 0;
            }

            token.wtree[2].invrw = 1;
            token.wtree[4].invrw = 1;
            token.wtree[7].invrw = 1;
            token.wtree[9].invrw = 1;
            token.wtree[11].invrw = 1;
            token.wtree[13].invrw = 1;
            token.wtree[16].invrw = 1;
            token.wtree[18].invrw = 1;
            token.wtree[3].invcl = 1;
            token.wtree[5].invcl = 1;
            token.wtree[8].invcl = 1;
            token.wtree[9].invcl = 1;
            token.wtree[12].invcl = 1;
            token.wtree[13].invcl = 1;
            token.wtree[17].invcl = 1;
            token.wtree[18].invcl = 1;

            wtree4(token, 0, 1, width, height, 0, 0, 1);

            if ((token.wtree[1].lenx % 2) == 0)
            {
                lenx = token.wtree[1].lenx / 2;
                lenx2 = lenx;
            }
            else
            {
                lenx = (token.wtree[1].lenx + 1) / 2;
                lenx2 = lenx - 1;
            }

            if ((token.wtree[1].leny % 2) == 0)
            {
                leny = token.wtree[1].leny / 2;
                leny2 = leny;
            }
            else
            {
                leny = (token.wtree[1].leny + 1) / 2;
                leny2 = leny - 1;
            }

            wtree4(token, 4, 6, lenx2, leny, lenx, 0, 0);
            wtree4(token, 5, 10, lenx, leny2, 0, leny, 0);
            wtree4(token, 14, 15, lenx, leny, 0, 0, 0);

            token.wtree[19].x = 0;
            token.wtree[19].y = 0;
            if ((token.wtree[15].lenx % 2) == 0)
                token.wtree[19].lenx = token.wtree[15].lenx / 2;
            else
                token.wtree[19].lenx = (token.wtree[15].lenx + 1) / 2;

            if ((token.wtree[15].leny % 2) == 0)
                token.wtree[19].leny = token.wtree[15].leny / 2;
            else
                token.wtree[19].leny = (token.wtree[15].leny + 1) / 2;
        }
        public static void wtree4(Token token, int start1, int start2, int lenx, int leny, int x, int y, int stop1)
        {
            int evenx, eveny;   /* Check length of subband for even or odd */
            int p1, p2;         /* w_tree locations for storing subband sizes and locations */

            p1 = start1;
            p2 = start2;

            evenx = lenx % 2;
            eveny = leny % 2;

            token.wtree[p1].x = x;
            token.wtree[p1].y = y;
            token.wtree[p1].lenx = lenx;
            token.wtree[p1].leny = leny;

            token.wtree[p2].x = x;
            token.wtree[p2 + 2].x = x;
            token.wtree[p2].y = y;
            token.wtree[p2 + 1].y = y;

            if (evenx == 0)
            {
                token.wtree[p2].lenx = lenx / 2;
                token.wtree[p2 + 1].lenx = token.wtree[p2].lenx;
            }
            else
            {
                if (p1 == 4)
                {
                    token.wtree[p2].lenx = (lenx - 1) / 2;
                    token.wtree[p2 + 1].lenx = token.wtree[p2].lenx + 1;
                }
                else
                {
                    token.wtree[p2].lenx = (lenx + 1) / 2;
                    token.wtree[p2 + 1].lenx = token.wtree[p2].lenx - 1;
                }
            }
            token.wtree[p2 + 1].x = token.wtree[p2].lenx + x;
            if (stop1 == 0)
            {
                token.wtree[p2 + 3].lenx = token.wtree[p2 + 1].lenx;
                token.wtree[p2 + 3].x = token.wtree[p2 + 1].x;
            }
            token.wtree[p2 + 2].lenx = token.wtree[p2].lenx;


            if (eveny == 0)
            {
                token.wtree[p2].leny = leny / 2;
                token.wtree[p2 + 2].leny = token.wtree[p2].leny;
            }
            else
            {
                if (p1 == 5)
                {
                    token.wtree[p2].leny = (leny - 1) / 2;
                    token.wtree[p2 + 2].leny = token.wtree[p2].leny + 1;
                }
                else
                {
                    token.wtree[p2].leny = (leny + 1) / 2;
                    token.wtree[p2 + 2].leny = token.wtree[p2].leny - 1;
                }
            }
            token.wtree[p2 + 2].y = token.wtree[p2].leny + y;
            if (stop1 == 0)
            {
                token.wtree[p2 + 3].leny = token.wtree[p2 + 2].leny;
                token.wtree[p2 + 3].y = token.wtree[p2 + 2].y;
            }
            token.wtree[p2 + 1].leny = token.wtree[p2].leny;
        }
        public static void buildQTree(Token token, int qtreelen)
        {
            token.qtree = new QuantTree[qtreelen];
            for (int i = 0; i < token.qtree.Length; i++)
            {
                token.qtree[i] = new QuantTree();
            }

            qtree16(token, 3, token.wtree[14].lenx, token.wtree[14].leny, token.wtree[14].x, token.wtree[14].y, 0, 0);
            qtree16(token, 19, token.wtree[4].lenx, token.wtree[4].leny, token.wtree[4].x, token.wtree[4].y, 0, 1);
            qtree16(token, 48, token.wtree[0].lenx, token.wtree[0].leny, token.wtree[0].x, token.wtree[0].y, 0, 0);
            qtree16(token, 35, token.wtree[5].lenx, token.wtree[5].leny, token.wtree[5].x, token.wtree[5].y, 1, 0);
            qtree4(token, 0, token.wtree[19].lenx, token.wtree[19].leny, token.wtree[19].x, token.wtree[19].y);
        }
        public static void qtree4(Token token, int start, int lenx, int leny, int x, int y)
        {
            int evenx, eveny;    /* Check length of subband for even or odd */
            int p;               /* indicates subband information being stored */

            p = start;
            evenx = lenx % 2;
            eveny = leny % 2;


            token.qtree[p].x = x;
            token.qtree[p + 2].x = x;
            token.qtree[p].y = y;
            token.qtree[p + 1].y = y;
            if (evenx == 0)
            {
                token.qtree[p].lenx = lenx / 2;
                token.qtree[p + 1].lenx = token.qtree[p].lenx;
                token.qtree[p + 2].lenx = token.qtree[p].lenx;
                token.qtree[p + 3].lenx = token.qtree[p].lenx;
            }
            else
            {
                token.qtree[p].lenx = (lenx + 1) / 2;
                token.qtree[p + 1].lenx = token.qtree[p].lenx - 1;
                token.qtree[p + 2].lenx = token.qtree[p].lenx;
                token.qtree[p + 3].lenx = token.qtree[p + 1].lenx;
            }
            token.qtree[p + 1].x = x + token.qtree[p].lenx;
            token.qtree[p + 3].x = token.qtree[p + 1].x;
            if (eveny == 0)
            {
                token.qtree[p].leny = leny / 2;
                token.qtree[p + 1].leny = token.qtree[p].leny;
                token.qtree[p + 2].leny = token.qtree[p].leny;
                token.qtree[p + 3].leny = token.qtree[p].leny;
            }
            else
            {
                token.qtree[p].leny = (leny + 1) / 2;
                token.qtree[p + 1].leny = token.qtree[p].leny;
                token.qtree[p + 2].leny = token.qtree[p].leny - 1;
                token.qtree[p + 3].leny = token.qtree[p + 2].leny;
            }
            token.qtree[p + 2].y = y + token.qtree[p].leny;
            token.qtree[p + 3].y = token.qtree[p + 2].y;
        }
        public static void qtree16(Token token, int start, int lenx, int leny, int x, int y, int rw, int cl)
        {
            int tempx, temp2x;   /* temporary x values */
            int tempy, temp2y;   /* temporary y values */
            int evenx, eveny;    /* Check length of subband for even or odd */
            int p;               /* indicates subband information being stored */

            p = start;
            evenx = lenx % 2;
            eveny = leny % 2;

            if (evenx == 0)
            {
                tempx = lenx / 2;
                temp2x = tempx;
            }
            else
            {
                if (cl != 0)
                {
                    temp2x = (lenx + 1) / 2;
                    tempx = temp2x - 1;
                }
                else
                {
                    tempx = (lenx + 1) / 2;
                    temp2x = tempx - 1;
                }
            }

            if (eveny == 0)
            {
                tempy = leny / 2;
                temp2y = tempy;
            }
            else
            {
                if (rw != 0)
                {
                    temp2y = (leny + 1) / 2;
                    tempy = temp2y - 1;
                }
                else
                {
                    tempy = (leny + 1) / 2;
                    temp2y = tempy - 1;
                }
            }

            evenx = tempx % 2;
            eveny = tempy % 2;

            token.qtree[p].x = x;
            token.qtree[p + 2].x = x;
            token.qtree[p].y = y;
            token.qtree[p + 1].y = y;
            if (evenx == 0)
            {
                token.qtree[p].lenx = tempx / 2;
                token.qtree[p + 1].lenx = token.qtree[p].lenx;
                token.qtree[p + 2].lenx = token.qtree[p].lenx;
                token.qtree[p + 3].lenx = token.qtree[p].lenx;
            }
            else
            {
                token.qtree[p].lenx = (tempx + 1) / 2;
                token.qtree[p + 1].lenx = token.qtree[p].lenx - 1;
                token.qtree[p + 2].lenx = token.qtree[p].lenx;
                token.qtree[p + 3].lenx = token.qtree[p + 1].lenx;
            }
            token.qtree[p + 1].x = x + token.qtree[p].lenx;
            token.qtree[p + 3].x = token.qtree[p + 1].x;
            if (eveny == 0)
            {
                token.qtree[p].leny = tempy / 2;
                token.qtree[p + 1].leny = token.qtree[p].leny;
                token.qtree[p + 2].leny = token.qtree[p].leny;
                token.qtree[p + 3].leny = token.qtree[p].leny;
            }
            else
            {
                token.qtree[p].leny = (tempy + 1) / 2;
                token.qtree[p + 1].leny = token.qtree[p].leny;
                token.qtree[p + 2].leny = token.qtree[p].leny - 1;
                token.qtree[p + 3].leny = token.qtree[p + 2].leny;
            }
            token.qtree[p + 2].y = y + token.qtree[p].leny;
            token.qtree[p + 3].y = token.qtree[p + 2].y;

            evenx = temp2x % 2;

            token.qtree[p + 4].x = x + tempx;
            token.qtree[p + 6].x = token.qtree[p + 4].x;
            token.qtree[p + 4].y = y;
            token.qtree[p + 5].y = y;
            token.qtree[p + 6].y = token.qtree[p + 2].y;
            token.qtree[p + 7].y = token.qtree[p + 2].y;
            token.qtree[p + 4].leny = token.qtree[p].leny;
            token.qtree[p + 5].leny = token.qtree[p].leny;
            token.qtree[p + 6].leny = token.qtree[p + 2].leny;
            token.qtree[p + 7].leny = token.qtree[p + 2].leny;
            if (evenx == 0)
            {
                token.qtree[p + 4].lenx = temp2x / 2;
                token.qtree[p + 5].lenx = token.qtree[p + 4].lenx;
                token.qtree[p + 6].lenx = token.qtree[p + 4].lenx;
                token.qtree[p + 7].lenx = token.qtree[p + 4].lenx;
            }
            else
            {
                token.qtree[p + 5].lenx = (temp2x + 1) / 2;
                token.qtree[p + 4].lenx = token.qtree[p + 5].lenx - 1;
                token.qtree[p + 6].lenx = token.qtree[p + 4].lenx;
                token.qtree[p + 7].lenx = token.qtree[p + 5].lenx;
            }
            token.qtree[p + 5].x = token.qtree[p + 4].x + token.qtree[p + 4].lenx;
            token.qtree[p + 7].x = token.qtree[p + 5].x;


            eveny = temp2y % 2;

            token.qtree[p + 8].x = x;
            token.qtree[p + 9].x = token.qtree[p + 1].x;
            token.qtree[p + 10].x = x;
            token.qtree[p + 11].x = token.qtree[p + 1].x;
            token.qtree[p + 8].y = y + tempy;
            token.qtree[p + 9].y = token.qtree[p + 8].y;
            token.qtree[p + 8].lenx = token.qtree[p].lenx;
            token.qtree[p + 9].lenx = token.qtree[p + 1].lenx;
            token.qtree[p + 10].lenx = token.qtree[p].lenx;
            token.qtree[p + 11].lenx = token.qtree[p + 1].lenx;
            if (eveny == 0)
            {
                token.qtree[p + 8].leny = temp2y / 2;
                token.qtree[p + 9].leny = token.qtree[p + 8].leny;
                token.qtree[p + 10].leny = token.qtree[p + 8].leny;
                token.qtree[p + 11].leny = token.qtree[p + 8].leny;
            }
            else
            {
                token.qtree[p + 10].leny = (temp2y + 1) / 2;
                token.qtree[p + 11].leny = token.qtree[p + 10].leny;
                token.qtree[p + 8].leny = token.qtree[p + 10].leny - 1;
                token.qtree[p + 9].leny = token.qtree[p + 8].leny;
            }
            token.qtree[p + 10].y = token.qtree[p + 8].y + token.qtree[p + 8].leny;
            token.qtree[p + 11].y = token.qtree[p + 10].y;


            token.qtree[p + 12].x = token.qtree[p + 4].x;
            token.qtree[p + 13].x = token.qtree[p + 5].x;
            token.qtree[p + 14].x = token.qtree[p + 4].x;
            token.qtree[p + 15].x = token.qtree[p + 5].x;
            token.qtree[p + 12].y = token.qtree[p + 8].y;
            token.qtree[p + 13].y = token.qtree[p + 8].y;
            token.qtree[p + 14].y = token.qtree[p + 10].y;
            token.qtree[p + 15].y = token.qtree[p + 10].y;
            token.qtree[p + 12].lenx = token.qtree[p + 4].lenx;
            token.qtree[p + 13].lenx = token.qtree[p + 5].lenx;
            token.qtree[p + 14].lenx = token.qtree[p + 4].lenx;
            token.qtree[p + 15].lenx = token.qtree[p + 5].lenx;
            token.qtree[p + 12].leny = token.qtree[p + 8].leny;
            token.qtree[p + 13].leny = token.qtree[p + 8].leny;
            token.qtree[p + 14].leny = token.qtree[p + 10].leny;
            token.qtree[p + 15].leny = token.qtree[p + 10].leny;
        }
    }//public class WSQHelper

    internal class QuantTree
    {
        public int x;    /* UL corner of block */
        public int y;
        public int lenx;  /* block size */
        public int leny;  /* block size */
    }
    internal class Quantization
    {
        public float q; /* quantization level */
        public float cr; /* compression ratio */
        public float r; /* compression bitrate */
        public float[] qbss_t = new float[WSQConstants.MAX_SUBBANDS];
        public float[] qbss = new float[WSQConstants.MAX_SUBBANDS];
        public float[] qzbs = new float[WSQConstants.MAX_SUBBANDS];
        public float[] var = new float[WSQConstants.MAX_SUBBANDS];
    }
    internal class WavletTree
    {
        public int x;
        public int y;
        public int lenx;
        public int leny;
        public int invrw;
        public int invcl;
    }
    internal class Table_DQT
    {
        public const int MAX_SUBBANDS = 64;
        public float binCenter;
        public float[] qBin = new float[MAX_SUBBANDS];
        public float[] zBin = new float[MAX_SUBBANDS];
        public char dqtDef;
    }
    internal class TableDTT
    {
        private static float[] HI_FILT_EVEN_8X8_1 = {
			0.03226944131446922f,
			-0.05261415011924844f,
			-0.18870142780632693f,
			0.60328894481393847f,
			-0.60328894481393847f,
			0.18870142780632693f,
			0.05261415011924844f,
			-0.03226944131446922f };

        private static float[] LO_FILT_EVEN_8X8_1 =  {
            0.07565691101399093f,
            -0.12335584105275092f,
            -0.09789296778409587f,
            0.85269867900940344f,
            0.85269867900940344f,
            -0.09789296778409587f,
            -0.12335584105275092f,
            0.07565691101399093f };

        private static float[] HI_FILT_NOT_EVEN_8X8_1 =  {
            0.06453888262893845f,
            -0.04068941760955844f,
            -0.41809227322221221f,
            0.78848561640566439f,
            -0.41809227322221221f,
            -0.04068941760955844f,
            0.06453888262893845f };

        private static float[] LO_FILT_NOT_EVEN_8X8_1 =  {
             0.03782845550699546f,
            -0.02384946501938000f,
            -0.11062440441842342f,
            0.37740285561265380f,
            0.85269867900940344f,
            0.37740285561265380f,
            -0.11062440441842342f,
            -0.02384946501938000f,
            0.03782845550699546f };


        public float[] lofilt = LO_FILT_NOT_EVEN_8X8_1;
        public float[] hifilt = HI_FILT_NOT_EVEN_8X8_1;
        public int losz;
        public int hisz;
        public int lodef;
        public int hidef;
    }
    internal class TableDHT
    {
        private const int MAX_HUFFBITS = 16; /*DO NOT CHANGE THIS CONSTANT!! */
        private const int MAX_HUFFCOUNTS_WSQ = 256; /* Length of code table: change as needed */

        public byte tabdef;
        public int[] huffbits = new int[MAX_HUFFBITS];
        public int[] huffvalues = new int[MAX_HUFFCOUNTS_WSQ + 1];
    }
    internal class Token
    {
        public TableDHT[] tableDHT = new TableDHT[WSQConstants.MAX_DHT_TABLES];
        public TableDTT tableDTT = new TableDTT();
        public Table_DQT tableDQT = new Table_DQT();

        public WavletTree[] wtree;
        public QuantTree[] qtree;

        public Quantization quant_vals = new Quantization();
        public List<String> comments = new List<String>();

        public Token()
        {
            /* Init DHT Tables to 0. */
            for (int i = 0; i < WSQConstants.MAX_DHT_TABLES; i++)
            {
                tableDHT[i] = new TableDHT();
                tableDHT[i].tabdef = 0;
            }
        }
    }
    internal class Ref<T>
    {
        private List<T> valor;
        private T[] buffer;

        private void ObtenerBuffer()
        {
            buffer = new T[valor.Count];
            for (int i = 0; i < valor.Count; i++)
            {
                buffer[i] = valor[i];
            }


        }


        public Ref()
        {
            //this=null;
            valor = null;
        }

        public Ref(T val)
        {
            //this.value = value;
            valor = new List<T>();
            valor.Add(val);
        }
        public T value
        {
            get { return valor[valor.Count - 1]; }
            set
            {
                if (valor == null)
                    valor = new List<T>();

                T val = value;
                valor.Add(val);
            }
        }
        public T[] valueT
        {
            get
            {
                ObtenerBuffer();
                return buffer;
            }
            set
            {
                valor = new List<T>();

                for (int i = 0; i < value.Length; i++)
                {
                    valor.Add(value[i]);

                }

            }
        }
        public void write(T[] val)
        {
            if (valor == null)
                valor = new List<T>();

            foreach (T b in val)
                valor.Add(b);
        }
    }
    internal class DataOutput
    {
        //private byte[] buffer;
        //public int pointer;
        private List<byte> LBuffer;
        private String _RutaDestino;

        public String RutaDestino
        {
            get { return _RutaDestino; }
            set {_RutaDestino = value;}
        } 

        public byte[] ObtenerBuffer()
        {
            byte[] buffer = new byte[LBuffer.Count];

            for (int i = 0; i < LBuffer.Count; i++)
            {
                buffer[i] = LBuffer[i];
            }

            return buffer;
        }

        public DataOutput()
        {
            LBuffer = new List<byte>();
        }
        public void write(byte val)
        {
            LBuffer.Add(val);
        }
        public void write(byte[] val)
        {
            foreach (byte b in val)
                LBuffer.Add(b);
        }
        public void write(int val)
        {
            byte aux = (byte)(0xff & val);
            LBuffer.Add(aux);


            //byte[] aux = new byte[sizeof(int)];
            //aux[0] = 0;
            //aux[1] = 0;
            //aux[2] = 0;
            //aux[3] = (byte)(0xff & val);



            //foreach (byte b in aux)
            //    LBuffer.Add(b);



        }
        public void writeShort(int val)
        {
            //Writes two bytes to the output stream to represent the value 
            //of the argument. The byte values to be written, 
            //in the order shown, are:

            byte[] aux = new byte[2];
            aux[0] = (byte)(0xff & (val >> 8));
            aux[1] = (byte)(0xff & val);

            //aux[0] = (byte)(-1);
            //aux[1] = (byte)(-96);

            //    out.write((v >>> 8) & 0xFF);
            //out.write((v >>> 0) & 0xFF);

            foreach (byte b in aux)
                LBuffer.Add(b);


        }
        public void writeByte(int val)
        {

            //Writes to the output stream the eight low- order bits of 
            //the argument v. The 24 high-order bits of v are ignored. 
            //(This means that writeByte does exactly the same thing as 
            //write for an integer argument.) The byte written by this 
            //method may be read by the readByte method of interface DataInput, 
            //which will then return a byte equal to (byte)v.
            byte aux = (byte)(0xff & val);
            LBuffer.Add(aux);
        }
        public void writeInt(int val)
        {
            byte[] aux = new byte[sizeof(int)];
            aux[0] = (byte)(0xff & (val >> 24));
            aux[1] = (byte)(0xff & (val >> 16));
            aux[2] = (byte)(0xff & (val >> 8));
            aux[3] = (byte)(0xff & val);
            //aux = BitConverter.GetBytes(val);

            foreach (byte b in aux)
                LBuffer.Add(b);
        }

    }

    internal class DataInput
    {
        private byte[] _buffer;
        private int pointer;
        public DataInput(byte[] info)
        {
            _buffer = info;
            pointer = 0;
        }
        public byte[] buffer
        {
            get { return _buffer; }
           
        }
        public int readUnsignedShort()
        {
            int byte1 = _buffer[pointer++];
            int byte2 = _buffer[pointer++];
            
            return (0xff & byte1) << 8 | (0xff & byte2);
        }
        public int readUnsignedByte()
        {
            byte byte1 = buffer[pointer++];

            return 0xff & byte1;
        }
        public long readInt()
        {
            byte byte1 = buffer[pointer++];
            byte byte2 = buffer[pointer++];
            byte byte3 = buffer[pointer++];
            byte byte4 = buffer[pointer++];

            return (0xffL & byte1) << 24 | (0xffL & byte2) << 16 | (0xffL & byte3) << 8 | (0xffL & byte4);
        }
        public byte[] readBytes(int size)
        {
            byte[] bytes = new byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = buffer[pointer++];
            }

            return bytes;
        }
    }
    internal class IntRef
    {
        public int value;


        public IntRef()
        {
        }

        public IntRef(int value)
        {
            this.value = value;
        }
    }
    internal class TokenD
    {
        public TableDHT[] tableDHT;
        public TableDTT tableDTT;
        public Table_DQT tableDQT;

        public WavletTree[] wtree;
        public QuantTree[] qtree;
        public Quantization quant_vals;
        public List<String> comments;

        public byte[] buffer;
        public int pointer;

        public TokenD()
        {

            initialize();
        }

        public TokenD(byte[] buffer)
        {
            this.buffer = buffer;
            this.pointer = 0;
            initialize();
        }

        private void initialize()
        {
            tableDTT = new TableDTT();
            tableDQT = new Table_DQT();
            quant_vals = new Quantization();
            comments = new List<String>();

            /* Init DHT Tables to 0. */
            tableDHT = new TableDHT[WSQConstants.MAX_DHT_TABLES];
            for (int i = 0; i < WSQConstants.MAX_DHT_TABLES; i++)
            {
                tableDHT[i] = new TableDHT();
                tableDHT[i].tabdef = 0;
            }
        }

        public long readInt()
        {
            byte byte1 = buffer[pointer++];
            byte byte2 = buffer[pointer++];
            byte byte3 = buffer[pointer++];
            byte byte4 = buffer[pointer++];

            return (0xffL & byte1) << 24 | (0xffL & byte2) << 16 | (0xffL & byte3) << 8 | (0xffL & byte4);
        }

        public int readShort()
        {
            int byte1 = buffer[pointer++];
            int byte2 = buffer[pointer++];

            return (0xff & byte1) << 8 | (0xff & byte2);
        }

        public int readByte()
        {
            byte byte1 = buffer[pointer++];

            return 0xff & byte1;
        }

        public byte[] readBytes(int size)
        {
            byte[] bytes = new byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = buffer[pointer++];
            }

            return bytes;
        }
    }
   
}
