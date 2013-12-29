using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Wsqm
{
//    IllegalStateException--->System.InvalidOperationException
//IllegalArgumentException--->ArgumentException 
//IOException---> IOException
    //RuntimeException--->SystemException 
    internal class Decoder : WSQConstants
    {
        //public cBitmapWithMetadata decode(DataInput info)
        //{
        //    try
        //    {
        //        //if (info.GetType()==typeof(DataInput)) {
        //        //    return decode((DataInput)info);
        //        //} else {
        //        //    return decode(info);
        //        //}
        //        return decode(info);
        //    }
        //    catch (IOException e)
        //    {
        //        throw (e);
        //    }

        //}
        public cBitmapWithMetadata decode(DataInput dataInput)
        {
            try
            {
                Token token = new Token();

                /* Read the SOI marker. */
                getCMarkerWSQ(dataInput, SOI_WSQ);

                /* Read in supporting tables up to the SOF marker. */
                int marker = getCMarkerWSQ(dataInput, TBLS_N_SOF);
                while (marker != SOF_WSQ)
                {
                    getCTableWSQ(dataInput, token, marker);
                    marker = getCMarkerWSQ(dataInput, TBLS_N_SOF);
                }

                /* Read in the Frame Header. */
                WSQHelper.HeaderFrm frmHeaderWSQ = getCFrameHeaderWSQ(dataInput);
                int width = frmHeaderWSQ.width;
                int height = frmHeaderWSQ.height;

                /* Build WSQ decomposition trees. */
                WSQHelper.buildWSQTrees(token, width, height);

                /* Decode the Huffman encoded buffer blocks. */
                int[] qdata = huffmanDecodeDataMem(dataInput, token, width * height);

                /* Decode the quantize wavelet subband buffer. */
                float[] fdata = unquantize(token, qdata, width, height);

                wsqReconstruct(token, fdata, width, height);

                /* Convert floating point pixels to unsigned char pixels. */
                byte[] cdata = convertImageToByte(fdata, width, height, frmHeaderWSQ.mShift, frmHeaderWSQ.rScale);


                Dictionary<String, String> nistcom = new Dictionary<String, String>();
                List<String> comments = new List<String>();
                KeyValuePair<String, String> val;
                foreach (String comment in token.comments)
                    try
                    {
                        foreach (KeyValuePair<String, String> entry in nistcom)
                        {
                            val=stringToFet(comment);
                            nistcom.Add(val.Key,val.Value);
                           // nistcom.Add((stringToFet(comment));
                        }
                        //nistcom.putAll(stringToFet(comment));
                    }
                    catch (Exception e)
                    {
                        comments.Add(comment);
                    }
                nistcom.Remove(NCM_HEADER);
                nistcom.Add(NCM_PIX_WIDTH,width.ToString());
                nistcom.Add(NCM_PIX_HEIGHT, height.ToString());
                nistcom.Add(NCM_PIX_DEPTH, "8");
                nistcom.Add(NCM_LOSSY, "1");
                nistcom.Add(NCM_COLORSPACE, "GRAY");
                nistcom.Add(NCM_COMPRESSION, "WSQ");
                //nistcom.remove(NCM_HEADER);
                //nistcom.put(NCM_PIX_WIDTH, Integer.toString(width));
                //nistcom.put(NCM_PIX_HEIGHT, Integer.toString(height));
                //nistcom.put(NCM_PIX_DEPTH, "8");
                //nistcom.put(NCM_LOSSY, "1");
                //nistcom.put(NCM_COLORSPACE, "GRAY");
                //nistcom.put(NCM_COMPRESSION, "WSQ");
                bool ppiOk = false;
                try
                {                    
                    if (Convert.ToInt32(nistcom[NCM_PPI]) > 0)
                        ppiOk = true;
                    //if (Integer.parseInt(nistcom.get(NCM_PPI)) > 0)
                    //    ppiOk = true;
                }
                catch (Exception t) { }
                if (!ppiOk)
                    nistcom.Add(NCM_PPI, "-1");
                    //nistcom.put(NCM_PPI, "-1");
                return new cBitmapWithMetadata(cdata, width, height,Convert.ToInt32(nistcom[NCM_PPI]), 8, 1, nistcom, comments.ToArray());
                //return new cBitmapWithMetadata(cdata, width, height, Integer.parseInt(nistcom.get(NCM_PPI)), 8, 1, nistcom, comments.toArray(new String[0]));
            }
            catch (IOException e)
            {
                throw (e);
            }

        }
        private int getCMarkerWSQ(DataInput dataInput, int type)
        {
            try
            {
                int marker = dataInput.readUnsignedShort();

                switch (type)
                {
                    case SOI_WSQ:
                        if (marker != SOI_WSQ)
                        {
                            throw new SystemException("ERROR : getCMarkerWSQ : No SOI marker : " + marker);
                        }

                        return marker;

                    case TBLS_N_SOF:
                        if (marker != DTT_WSQ
                        && marker != DQT_WSQ
                        && marker != DHT_WSQ
                        && marker != SOF_WSQ
                        && marker != COM_WSQ)
                        {
                            throw new SystemException("ERROR : getc_marker_wsq : No SOF, Table, or comment markers : " + marker);
                        }

                        return marker;

                    case TBLS_N_SOB:
                        if (marker != DTT_WSQ
                        && marker != DQT_WSQ
                        && marker != DHT_WSQ
                        && marker != SOB_WSQ
                        && marker != COM_WSQ)
                        {
                            throw new SystemException("ERROR : getc_marker_wsq : No SOB, Table, or comment markers : " +
                                    marker);
                        }
                        return marker;
                    case ANY_WSQ:
                        if ((marker & 0xff00) != 0xff00)
                        {
                            throw new SystemException("ERROR : getc_marker_wsq : no marker found : " + marker);
                        }

                        /* Added by MDG on 03-07-05 */
                        if ((marker < SOI_WSQ) || (marker > COM_WSQ))
                        {
                            throw new SystemException("ERROR : getc_marker_wsq : not a valid marker : " + marker);
                        }

                        return marker;
                    default:
                        throw new SystemException("ERROR : getc_marker_wsq : Invalid marker : " + marker);
                }
            }
            catch (IOException e)
            {
                throw (e);
            }

        }

        private void getCTableWSQ(DataInput DataInput, Token token, int marker)
        {
            try
            {
                switch (marker)
                {
                    case DTT_WSQ:
                        getCTransformTable(DataInput, token);
                        return;
                    case DQT_WSQ:
                        getCQuantizationTable(DataInput, token);
                        return;
                    case DHT_WSQ:
                        getCHuffmanTableWSQ(DataInput, token);
                        return;
                    case COM_WSQ:
                        token.comments.Add(getCComment(DataInput, token));
                        return;
                    default:
                        throw new SystemException("ERROR: getCTableWSQ : Invalid table defined : " +BitConverter.ToString(BitConverter.GetBytes(marker)));
                }
            }
            catch (IOException e)
            {
                throw (e);
            }

        }

        private KeyValuePair<String, String> stringToFet(String comment)
        {
            try
            {
                if (!comment.StartsWith(NCM_HEADER))
                    throw new ArgumentException("Not a NISTCOM header");

                System.IO.StringReader inn = new System.IO.StringReader(comment);

                
                //Scanner inn = new Scanner(comment);
                KeyValuePair<String, String> result=new KeyValuePair<String, String>();

                String line;

                while ((line = inn.ReadLine()) != null)
                {
                    int split = line.IndexOf(" "); 
                    if (split < 0)
                    {
                        Console.WriteLine("Illegal NISTCOM header: Missing separator on line '" + line + "'");
                        continue;
                    }

                    String key = System.Web.HttpUtility.UrlDecode(line.Substring(0, split), Encoding.UTF8);
                    String value = System.Web.HttpUtility.UrlDecode(line.Substring(split + 1), Encoding.UTF8);
                    result = new KeyValuePair<String, String>(key,value);
                }
                //while (inn.hasNextLine())
                //{
                //    String line = inn.nextLine();
                //    int split = line.IndexOf(" ");
                //    if (split < 0)
                //    {
                //        Console.WriteLine("Illegal NISTCOM header: Missing separator on line '" + line + "'");
                //        continue;
                //    }
                    
                //    //String key = URLDecoder.decode(line.substring(0, split), "UTF-8");
                //    //String value = URLDecoder.decode(line.substring(split + 1), "UTF-8");

                //    String key = System.Web.HttpUtility.UrlDecode(line.Substring(0, split), Encoding.UTF8);
                //    String value = System.Web.HttpUtility.UrlDecode(line.Substring(split + 1), Encoding.UTF8);
                //    result.Add(key, value);
                //    //result.put(key, value);

                //}
                return result;
            }
            catch (Exception e)
            {
                throw new SystemException(e.Message);
            }
        }

        private String getCComment(DataInput dataInput, Token token){
            String resul=null;
            try{
                int size = dataInput.readUnsignedShort() - 2;
                byte[] t = dataInput.readBytes(size);
                resul= ASCIIEncoding.ASCII.GetString(t, 0, t.Length);
                //int size = dataInput.readUnsignedShort() - 2;
                //sbyte[] bytes = new sbyte[size];
                //dataInput.readFully(bytes);
                //resul= new String(bytes, "UTF-8");
            }catch(IOException e){
                throw(e);
            }
            return resul;
		   
	    }
        private void getCTransformTable(DataInput dataInput, Token token){
            try{
             
		        // read header Size;
		        dataInput.readUnsignedShort();

		        token.tableDTT.hisz = dataInput.readUnsignedByte();
		        token.tableDTT.losz = dataInput.readUnsignedByte();

		        token.tableDTT.hifilt = new float[token.tableDTT.hisz];
		        token.tableDTT.lofilt = new float[token.tableDTT.losz];

		        int aSize;
		        if (token.tableDTT.hisz % 2 != 0) {
			        aSize = (token.tableDTT.hisz + 1) / 2;
		        } else {
			        aSize = token.tableDTT.hisz / 2;
		        }

		        float[] aLofilt = new float[aSize];

		        aSize--;
		        for (int cnt = 0; cnt <= aSize; cnt++) {
			        int sign = dataInput.readUnsignedByte();
			        int scale = dataInput.readUnsignedByte();
			        long shrtDat = dataInput.readInt() & 0xFFFFFFFFL;

			        aLofilt[cnt] = (float) shrtDat;

			        while (scale > 0) {
				        aLofilt[cnt] /= 10.0f;
				        scale--;
			        }

			        if (sign != 0) {
				        aLofilt[cnt] *= -1.0f;
			        }

			        if (token.tableDTT.hisz % 2 != 0) {
				        token.tableDTT.hifilt[cnt + aSize] = intSign(cnt) * aLofilt[cnt];
				        if (cnt > 0) {
					        token.tableDTT.hifilt[aSize - cnt] = token.tableDTT.hifilt[cnt + aSize];
				        }
			        } else {
				        token.tableDTT.hifilt[cnt + aSize + 1] = intSign(cnt) * aLofilt[cnt];
				        token.tableDTT.hifilt[aSize - cnt] = -1 * token.tableDTT.hifilt[cnt + aSize + 1];
			        }
		        }

		        if (token.tableDTT.losz % 2 != 0) {
			        aSize = (token.tableDTT.losz + 1) / 2;
		        } else {
			        aSize = token.tableDTT.losz / 2;
		        }

		        float[] aHifilt = new float[aSize];

		        aSize--;
		        for (int cnt = 0; cnt <= aSize; cnt++) {
			        int sign = dataInput.readUnsignedByte();
			        int scale = dataInput.readUnsignedByte();
			        long shrtDat = dataInput.readInt() & 0xFFFFFFFFL;

			        aHifilt[cnt] = (float) shrtDat;

			        while (scale > 0) {
				        aHifilt[cnt] /= 10.0f;
				        scale--;
			        }

			        if (sign != 0) {
				        aHifilt[cnt] *= -1.0f;
			        }

			        if (token.tableDTT.losz % 2 != 0) {
				        token.tableDTT.lofilt[cnt + aSize] = intSign(cnt) * aHifilt[cnt];
				        if (cnt > 0) {
					        token.tableDTT.lofilt[aSize - cnt] = token.tableDTT.lofilt[cnt + aSize];
				        }
			        } else {
				        token.tableDTT.lofilt[cnt + aSize + 1] = intSign(cnt + 1) * aHifilt[cnt];
				        token.tableDTT.lofilt[aSize - cnt] = token.tableDTT.lofilt[cnt + aSize + 1];
			        }
		        }

		        token.tableDTT.lodef = 1;
		        token.tableDTT.hidef = 1;
            }
            catch (IOException e)
            {
                throw (e);
            }
	    }
        public void getCQuantizationTable(DataInput dataInput, Token token){
            try{
            
		        dataInput.readUnsignedShort(); /* header size */
		        int scale = dataInput.readUnsignedByte(); /* scaling parameter */
		        int shrtDat = dataInput.readUnsignedShort(); /* counter and temp short buffer */

		        token.tableDQT.binCenter = (float) shrtDat;
		        while (scale > 0) {
			        token.tableDQT.binCenter /= 10.0f;
			        scale--;
		        }

		        for (int cnt = 0; cnt < Table_DQT.MAX_SUBBANDS; cnt++) {
			        scale = dataInput.readUnsignedByte();
			        shrtDat = dataInput.readUnsignedShort();
			        token.tableDQT.qBin[cnt] = (float) shrtDat;
			        while (scale > 0) {
				        token.tableDQT.qBin[cnt] /= 10.0f;
				        scale--;
			        }

			        scale = dataInput.readUnsignedByte();
			        shrtDat = dataInput.readUnsignedShort();
			        token.tableDQT.zBin[cnt] = (float) shrtDat;
			        while (scale > 0) {
				        token.tableDQT.zBin[cnt] /= 10.0f;
				        scale--;
			        }
		        }

		        token.tableDQT.dqtDef = (char)1;
            }
            catch (IOException e)
            {
                throw (e);
            }
	    }
        public void getCHuffmanTableWSQ(DataInput DataInput, Token token){
            try{
                /* First time, read table len. */
                WSQHelper.HuffmanTable firstHuffmanTable = getCHuffmanTable(DataInput, token, MAX_HUFFCOUNTS_WSQ, 0, true);

                /* Store table into global structure list. */
                int tableId = firstHuffmanTable.tableId;
                token.tableDHT[tableId].huffbits = (int[])firstHuffmanTable.huffbits.Clone();
                token.tableDHT[tableId].huffvalues = (int[])firstHuffmanTable.huffvalues.Clone();
                token.tableDHT[tableId].tabdef = 1;

                int bytesLeft = firstHuffmanTable.bytesLeft;
                while (bytesLeft != 0)
                {
                    /* Read next table without rading table len. */
                    WSQHelper.HuffmanTable huffmantable = getCHuffmanTable(DataInput, token, MAX_HUFFCOUNTS_WSQ, bytesLeft, false);

                    /* If table is already defined ... */
                    tableId = huffmantable.tableId;
                    if (token.tableDHT[tableId].tabdef != 0)
                    {
                        throw new SystemException("ERROR : getCHuffmanTableWSQ : huffman table already defined.");
                    }

                    /* Store table into global structure list. */
                    token.tableDHT[tableId].huffbits = (int[])huffmantable.huffbits.Clone();
                    token.tableDHT[tableId].huffvalues = (int[])huffmantable.huffvalues.Clone();
                    token.tableDHT[tableId].tabdef = 1;
                    bytesLeft = huffmantable.bytesLeft;
                }
            }catch (IOException e)
            {
                throw (e);
            }
		    
	    }
        private WSQHelper.HuffmanTable getCHuffmanTable(DataInput dataInput, Token token, int maxHuffcounts, int bytesLeft, bool readTableLen){
            try{
                WSQHelper.HuffmanTable huffmanTable = new WSQHelper.HuffmanTable();

                /* table_len */
                if (readTableLen)
                {
                    huffmanTable.tableLen = dataInput.readUnsignedShort();
                    huffmanTable.bytesLeft = huffmanTable.tableLen - 2;
                    bytesLeft = huffmanTable.bytesLeft;
                }
                else
                {
                    huffmanTable.bytesLeft = bytesLeft;
                }

                /* If no bytes left ... */
                if (bytesLeft <= 0)
                {
                    throw new SystemException("ERROR : getCHuffmanTable : no huffman table bytes remaining");
                }

                /* Table ID */
                huffmanTable.tableId = dataInput.readUnsignedByte();
                huffmanTable.bytesLeft--;


                huffmanTable.huffbits = new int[MAX_HUFFBITS];
                int numHufvals = 0;
                /* L1 ... L16 */
                for (int i = 0; i < MAX_HUFFBITS; i++)
                {
                    huffmanTable.huffbits[i] = dataInput.readUnsignedByte();
                    numHufvals += huffmanTable.huffbits[i];
                }
                huffmanTable.bytesLeft -= MAX_HUFFBITS;

                if (numHufvals > maxHuffcounts + 1)
                {
                    throw new SystemException("ERROR : getCHuffmanTable : numHufvals is larger than MAX_HUFFCOUNTS");
                }

                /* Could allocate only the amount needed ... then we wouldn't */
                /* need to pass MAX_HUFFCOUNTS. */
                huffmanTable.huffvalues = new int[maxHuffcounts + 1];

                /* V1,1 ... V16,16 */
                for (int i = 0; i < numHufvals; i++)
                {
                    huffmanTable.huffvalues[i] = dataInput.readUnsignedByte();
                }
                huffmanTable.bytesLeft -= numHufvals;

                return huffmanTable;
            }catch (IOException e)
            {
                throw (e);
            }
            
	    }
        //************************************************
        private WSQHelper.HeaderFrm getCFrameHeaderWSQ(DataInput dataInput){
            try{
		        WSQHelper.HeaderFrm headerFrm = new WSQHelper.HeaderFrm();

		        /* int hdrSize = */ dataInput.readUnsignedShort(); /* header size */

		        headerFrm.black = dataInput.readUnsignedByte();
		        headerFrm.white = dataInput.readUnsignedByte();
		        headerFrm.height = dataInput.readUnsignedShort();
		        headerFrm.width = dataInput.readUnsignedShort();
		        int scale = dataInput.readUnsignedByte(); /* exponent scaling parameter */
		        int shrtDat = dataInput.readUnsignedShort(); /* buffer pointer */
		        headerFrm.mShift = (float) shrtDat;
		        while (scale > 0) {
			        headerFrm.mShift /= 10.0f;
			        scale--;
		        }

		        scale = dataInput.readUnsignedByte();
		        shrtDat = dataInput.readUnsignedShort();
		        headerFrm.rScale = (float) shrtDat;
		        while (scale > 0) {
			        headerFrm.rScale /= 10.0f;
			        scale--;
		        }

		        headerFrm.wsqEncoder = dataInput.readUnsignedByte();
		        headerFrm.software = dataInput.readUnsignedShort();

		        return headerFrm;
            }catch (IOException e)
            {
                throw (e);
            }
	    }

	    private int[] huffmanDecodeDataMem(DataInput DataInput, Token token, int size){
            try{
		        int[] qdata = new int[size];
		        int[] maxcode = new int[MAX_HUFFBITS + 1];
		        int[] mincode = new int[MAX_HUFFBITS + 1];
		        int[] valptr = new int[MAX_HUFFBITS + 1];

		        Ref<int> marker = new Ref<int>(getCMarkerWSQ(DataInput, TBLS_N_SOB));

		        Ref<int> bitCount = new Ref<int>(0); /* bit count for getc_nextbits_wsq routine */
		        Ref<int> nextByte = new Ref<int>(0); /*next byte of buffer*/
		        int hufftableId = 0; /* huffman table number */
		        int ip = 0;

		        while (marker.value != EOI_WSQ) {

			        if (marker.value != 0) {
				        while (marker.value != SOB_WSQ) {
					        getCTableWSQ(DataInput, token, marker.value);
					        marker.value = getCMarkerWSQ(DataInput, TBLS_N_SOB);
				        }
				        hufftableId = getCBlockHeader(DataInput); /* huffman table number */

				        if (token.tableDHT[hufftableId].tabdef != 1) {
					        throw new SystemException("ERROR : huffmanDecodeDataMem : huffman table undefined.");
				        }

				        /* the next two routines reconstruct the huffman tables */
				        WSQHelper.HuffCode[] hufftable = buildHuffsizes(token.tableDHT[hufftableId].huffbits, MAX_HUFFCOUNTS_WSQ);
				        buildHuffcodes(hufftable);

				        /* this routine builds a set of three tables used in decoding */
				        /* the compressed buffer*/
				        genDecodeTable(hufftable, maxcode, mincode, valptr, token.tableDHT[hufftableId].huffbits);

				        bitCount.value = 0;
				        marker.value = 0;
			        }

			        /* get next huffman category code from compressed input buffer stream */
			        int nodeptr = decodeDataMem(DataInput, mincode, maxcode, valptr, token.tableDHT[hufftableId].huffvalues, bitCount, marker, nextByte);
			        /* nodeptr  pointers for decoding */

			        if (nodeptr == -1) {
				        continue;
			        }

			        if (nodeptr > 0 && nodeptr <= 100) {
				        for (int n = 0; n < nodeptr; n++) {
					        qdata[ip++] = 0; /* z run */
				        }
			        } else if (nodeptr > 106 && nodeptr < 0xff) {
				        qdata[ip++] = nodeptr - 180;
			        } else if (nodeptr == 101) {
				        qdata[ip++] = getCNextbitsWSQ(DataInput,  marker, bitCount, 8, nextByte);
			        } else if (nodeptr == 102) {
				        qdata[ip++] = -getCNextbitsWSQ(DataInput, marker, bitCount, 8, nextByte);
			        } else if (nodeptr == 103) {
				        qdata[ip++] = getCNextbitsWSQ(DataInput, marker, bitCount, 16, nextByte);
			        } else if (nodeptr == 104) {
				        qdata[ip++] = -getCNextbitsWSQ(DataInput, marker, bitCount, 16, nextByte);
			        } else if (nodeptr == 105) {
				        int n = getCNextbitsWSQ(DataInput, marker, bitCount, 8, nextByte);
				        while (n-- > 0) {
					        qdata[ip++] = 0;
				        }
			        } else if (nodeptr == 106) {
				        int n = getCNextbitsWSQ(DataInput, marker, bitCount, 16, nextByte);
				        while (n-- > 0) {
					        qdata[ip++] = 0;
				        }
			        } else {
				        throw new SystemException("ERROR: huffman_decode_data_mem : Invalid code (" + nodeptr + ")");
			        }
		        }

		        return qdata;
            }catch (IOException e)
            {
                throw (e);
            }
	    }

	    private int getCBlockHeader(DataInput dataInput){
            try{
                dataInput.readUnsignedShort(); /* block header size */
		        return dataInput.readUnsignedByte();
 	        }
            catch (IOException e)
            {
                throw (e);
            }

		    
	    }

	    private WSQHelper.HuffCode[] buildHuffsizes(int[] huffbits, int maxHuffcounts) {
		    WSQHelper.HuffCode[] huffcodeTable;    /*table of huffman codes and sizes*/
		    int numberOfCodes = 1;     /*the number codes for a given code size*/

		    huffcodeTable = new WSQHelper.HuffCode[maxHuffcounts + 1];

		    int tempSize = 0;
		    for (int codeSize = 1; codeSize <= MAX_HUFFBITS; codeSize++) {
			    while (numberOfCodes <= huffbits[codeSize - 1]) {
				    huffcodeTable[tempSize] = new WSQHelper.HuffCode();
				    huffcodeTable[tempSize].size = codeSize;
				    tempSize++;
				    numberOfCodes++;
			    }
			    numberOfCodes = 1;
		    }

		    huffcodeTable[tempSize] = new WSQHelper.HuffCode();
		    huffcodeTable[tempSize].size = 0;

		    return huffcodeTable;
	    }

	    private void buildHuffcodes(WSQHelper.HuffCode[] huffcodeTable) {
		    short tempCode = 0;  /*used to construct code word*/
		    int pointer = 0;     /*pointer to code word information*/

		    int tempSize = huffcodeTable[0].size;
		    if (huffcodeTable[pointer].size == 0) {
			    return;
		    }

		    do {
			    do {
				    huffcodeTable[pointer].code = tempCode;
				    tempCode++;
				    pointer++;
			    } while (huffcodeTable[pointer].size == tempSize);

			    if (huffcodeTable[pointer].size == 0)
				    return;

			    do {
				    tempCode <<= 1;
				    tempSize++;
			    } while (huffcodeTable[pointer].size != tempSize);
		    } while (huffcodeTable[pointer].size == tempSize);
	    }

	    private void genDecodeTable(WSQHelper.HuffCode[] huffcodeTable, int[] maxcode, int[] mincode, int[] valptr, int[] huffbits) {
		    for (int i = 0; i <= MAX_HUFFBITS; i++) {
			    maxcode[i] = 0;
			    mincode[i] = 0;
			    valptr[i] = 0;
		    }

		    int i2 = 0;
		    for (int i = 1; i <= MAX_HUFFBITS; i++) {
			    if (huffbits[i - 1] == 0) {
				    maxcode[i] = -1;
				    continue;
			    }
			    valptr[i] = i2;
			    mincode[i] = huffcodeTable[i2].code;
			    i2 = i2 + huffbits[i - 1] - 1;
			    maxcode[i] = huffcodeTable[i2].code;
			    i2++;
		    }
	    }

	    private int decodeDataMem(DataInput DataInput, int[] mincode, int[] maxcode, int[] valptr, int[] huffvalues, Ref<int> bitCount, Ref<int> marker, Ref<int> nextByte){
            try{
		        short code = (short) getCNextbitsWSQ(DataInput, marker, bitCount, 1, nextByte);   /* becomes a huffman code word  (one bit at a time) */
		        if (marker.value != 0) {
			        return -1;
		        }

		        int inx;
		        for (inx = 1; code > maxcode[inx]; inx++) {
			        int tbits = getCNextbitsWSQ(DataInput, marker, bitCount, 1, nextByte);  /* becomes a huffman code word  (one bit at a time)*/
			        code = (short) ((code << 1) + tbits);

			        if (marker.value != 0) {
				        return -1;
			        }
		        }

		        int inx2 = valptr[inx] + code - mincode[inx];  /*increment variables*/
		        return huffvalues[inx2];
            }
            catch (IOException e)
            {
                throw (e);
            }
	    }

	    private int getCNextbitsWSQ(DataInput dataInput, Ref<int> marker, Ref<int> bitCount, int bitsReq, Ref<int> nextByte){
            try{
		        if (bitCount.value == 0) {
			        nextByte.value = dataInput.readUnsignedByte();

			        bitCount.value = 8;
			        if (nextByte.value == 0xFF) {
				        int code2 = dataInput.readUnsignedByte();  /*stuffed byte of buffer*/

				        if (code2 != 0x00 && bitsReq == 1) {
					        marker.value = (nextByte.value << 8) | code2;
					        return 1;
				        }
				        if (code2 != 0x00) {
					        throw new SystemException("ERROR: getCNextbitsWSQ : No stuffed zeros.");
				        }
			        }
		        }

		        int bits, tbits;  /*bits of current buffer byte requested*/
		        int bitsNeeded; /*additional bits required to finish request*/

		        if (bitsReq <= bitCount.value) {
			        bits = (nextByte.value >> (bitCount.value - bitsReq)) & (BITMASK[bitsReq]);
			        bitCount.value -= bitsReq;
			        nextByte.value &= BITMASK[bitCount.value];
		        } else {
			        bitsNeeded = bitsReq - bitCount.value; /*additional bits required to finish request*/
			        bits = nextByte.value << bitsNeeded;
			        bitCount.value = 0;
			        tbits = getCNextbitsWSQ(dataInput, marker, bitCount, bitsNeeded, nextByte);
			        bits |= tbits;
		        }

		        return bits;
            }
            catch (IOException e)
            {
                throw (e);
            }
	    }

	    private float[] unquantize(Token token, int[] sip, int width, int height) {
		    float[] fip = new float[width * height];  /* floating point image */

		    if (token.tableDQT.dqtDef != 1) {
			    throw new SystemException("ERROR: unquantize : quantization table parameters not defined!");
		    }

		    float binCenter = token.tableDQT.binCenter; /* quantizer bin center */

		    int sptr = 0;
		    for (int cnt = 0; cnt < NUM_SUBBANDS; cnt++) {
			    if (token.tableDQT.qBin[cnt] == 0.0) {
				    continue;
			    }

			    int fptr = (token.qtree[cnt].y * width) + token.qtree[cnt].x;

			    for (int row = 0; row < token.qtree[cnt].leny; row++, fptr += width - token.qtree[cnt].lenx) {
				    for (int col = 0; col < token.qtree[cnt].lenx; col++) {
					    if (sip[sptr] == 0) {
						    fip[fptr] = 0.0f;
					    } else if (sip[sptr] > 0) {
						    fip[fptr] = (token.tableDQT.qBin[cnt] * (sip[sptr] - binCenter)) + (token.tableDQT.zBin[cnt] / 2.0f);
					    } else if (sip[sptr] < 0) {
						    fip[fptr] = (token.tableDQT.qBin[cnt] * (sip[sptr] + binCenter)) - (token.tableDQT.zBin[cnt] / 2.0f);
					    } else {
						    throw new SystemException("ERROR : unquantize : invalid quantization pixel value");
					    }
					    fptr++;
					    sptr++;
				    }
			    }
		    }

		    return fip;
	    }

	    private void wsqReconstruct(Token token, float[] fdata, int width, int height) {
		    if (token.tableDTT.lodef != 1) {
			    throw new SystemException("ERROR: wsq_reconstruct : Lopass filter coefficients not defined");
		    }

		    if (token.tableDTT.hidef != 1) {
			    throw new SystemException("ERROR: wsq_reconstruct : Hipass filter coefficients not defined");
		    }

		    int numPix = width * height;
		    /* Allocate temporary floating point pixmap. */
		    float[] fdataTemp = new float[numPix];

		    /* Reconstruct floating point pixmap from wavelet subband buffer. */
		    for (int node = W_TREELEN - 1; node >= 0; node--) {
			    int fdataBse = (token.wtree[node].y * width) + token.wtree[node].x;
			    joinLets(fdataTemp, fdata, 0, fdataBse, token.wtree[node].lenx, token.wtree[node].leny,
					    1, width,
					    token.tableDTT.hifilt, token.tableDTT.hisz,
					    token.tableDTT.lofilt, token.tableDTT.losz,
					    token.wtree[node].invcl);
			    joinLets(fdata, fdataTemp, fdataBse, 0, token.wtree[node].leny, token.wtree[node].lenx,
					    width, 1,
					    token.tableDTT.hifilt, token.tableDTT.hisz,
					    token.tableDTT.lofilt, token.tableDTT.losz,
					    token.wtree[node].invrw);
		    }
	    }

	    private void joinLets(
			    float[] newdata,
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
			    int inv)        /* spectral inversion? */ {
		    int lp0, lp1;
		    int hp0, hp1;
		    int lopass, hipass;   /* lo/hi pass image pointers */
		    int limg, himg;
		    int pix, cl_rw;      /* pixel counter and column/row counter */
		    int i, da_ev;         /* if "scanline" is even or odd and */
		    int loc, hoc;
		    int hlen, llen;
		    int nstr, pstr;
		    int tap;
		    int fi_ev;
		    int olle, ohle, olre, ohre;
		    int lle, lle2, lre, lre2;
		    int hle, hle2, hre, hre2;
		    int lpx, lspx;
		    int lpxstr, lspxstr;
		    int lstap, lotap;
		    int hpx, hspx;
		    int hpxstr, hspxstr;
		    int hstap, hotap;
		    int asym, fhre = 0, ofhre;
		    float ssfac, osfac, sfac;

		    da_ev = len2 % 2;
		    fi_ev = lsz % 2;
		    pstr = stride;
		    nstr = -pstr;
		    if (da_ev != 0) {
			    llen = (len2 + 1) / 2;
			    hlen = llen - 1;
		    } else {
			    llen = len2 / 2;
			    hlen = llen;
		    }

		    if (fi_ev != 0) {
			    asym = 0;
			    ssfac = 1.0f;
			    ofhre = 0;
			    loc = (lsz - 1) / 4;
			    hoc = (hsz + 1) / 4 - 1;
			    lotap = ((lsz - 1) / 2) % 2;
			    hotap = ((hsz + 1) / 2) % 2;
			    if (da_ev != 0) {
				    olle = 0;
				    olre = 0;
				    ohle = 1;
				    ohre = 1;
			    } else {
				    olle = 0;
				    olre = 1;
				    ohle = 1;
				    ohre = 0;
			    }
		    } else {
			    asym = 1;
			    ssfac = -1.0f;
			    ofhre = 2;
			    loc = lsz / 4 - 1;
			    hoc = hsz / 4 - 1;
			    lotap = (lsz / 2) % 2;
			    hotap = (hsz / 2) % 2;
			    if (da_ev != 0) {
				    olle = 1;
				    olre = 0;
				    ohle = 1;
				    ohre = 1;
			    } else {
				    olle = 1;
				    olre = 1;
				    ohle = 1;
				    ohre = 1;
			    }

			    if (loc == -1) {
				    loc = 0;
				    olle = 0;
			    }
			    if (hoc == -1) {
				    hoc = 0;
				    ohle = 0;
			    }

			    for (i = 0; i < hsz; i++) {
				    hi[i] *= -1.0f;
			    }
		    }

		    for (cl_rw = 0; cl_rw < len1; cl_rw++) {
			    limg = newIndex + cl_rw * pitch;
			    himg = limg;
			    newdata[himg] = 0.0f;
			    newdata[himg + stride] = 0.0f;
			    if (inv != 0) {
				    hipass = oldIndex + cl_rw * pitch;
				    lopass = hipass + stride * hlen;
			    } else {
				    lopass = oldIndex + cl_rw * pitch;
				    hipass = lopass + stride * llen;
			    }

			    lp0 = lopass;
			    lp1 = lp0 + (llen - 1) * stride;
			    lspx = lp0 + (loc * stride);
			    lspxstr = nstr;
			    lstap = lotap;
			    lle2 = olle;
			    lre2 = olre;

			    hp0 = hipass;
			    hp1 = hp0 + (hlen - 1) * stride;
			    hspx = hp0 + (hoc * stride);
			    hspxstr = nstr;
			    hstap = hotap;
			    hle2 = ohle;
			    hre2 = ohre;
			    osfac = ssfac;

			    for (pix = 0; pix < hlen; pix++) {
				    for (tap = lstap; tap >= 0; tap--) {
					    lle = lle2;
					    lre = lre2;
					    lpx = lspx;
					    lpxstr = lspxstr;

					    newdata[limg] = olddata[lpx] * lo[tap];
					    for (i = tap + 2; i < lsz; i += 2) {
						    if (lpx == lp0) {
							    if (lle != 0) {
								    lpxstr = 0;
								    lle = 0;
							    } else {
								    lpxstr = pstr;
							    }
						    }
						    if (lpx == lp1) {
							    if (lre != 0) {
								    lpxstr = 0;
								    lre = 0;
							    } else {
								    lpxstr = nstr;
							    }
						    }
						    lpx += lpxstr;
						    newdata[limg] += olddata[lpx] * lo[i];
					    }
					    limg += stride;
				    }
				    if (lspx == lp0) {
					    if (lle2 != 0) {
						    lspxstr = 0;
						    lle2 = 0;
					    } else {
						    lspxstr = pstr;
					    }
				    }
				    lspx += lspxstr;
				    lstap = 1;

				    for (tap = hstap; tap >= 0; tap--) {
					    hle = hle2;
					    hre = hre2;
					    hpx = hspx;
					    hpxstr = hspxstr;
					    fhre = ofhre;
					    sfac = osfac;

					    for (i = tap; i < hsz; i += 2) {
						    if (hpx == hp0) {
							    if (hle != 0) {
								    hpxstr = 0;
								    hle = 0;
							    } else {
								    hpxstr = pstr;
								    sfac = 1.0f;
							    }
						    }
						    if (hpx == hp1) {
							    if (hre != 0) {
								    hpxstr = 0;
								    hre = 0;
								    if (asym != 0 && da_ev != 0) {
									    hre = 1;
									    fhre--;
									    sfac = (float) fhre;
									    if (sfac == 0.0)
										    hre = 0;
								    }
							    } else {
								    hpxstr = nstr;
								    if (asym != 0)
									    sfac = -1.0f;
							    }
						    }
						    newdata[himg] += olddata[hpx] * hi[i] * sfac;
						    hpx += hpxstr;
					    }
					    himg += stride;
				    }
				    if (hspx == hp0) {
					    if (hle2 != 0) {
						    hspxstr = 0;
						    hle2 = 0;
					    } else {
						    hspxstr = pstr;
						    osfac = 1.0f;
					    }
				    }
				    hspx += hspxstr;
				    hstap = 1;
			    }


			    if (da_ev != 0)
				    if (lotap != 0) {
					    lstap = 1;
				    } else {
					    lstap = 0;
				    }
			    else if (lotap != 0) {
				    lstap = 2;
			    } else {
				    lstap = 1;
			    }

			    for (tap = 1; tap >= lstap; tap--) {
				    lle = lle2;
				    lre = lre2;
				    lpx = lspx;
				    lpxstr = lspxstr;

				    newdata[limg] = olddata[lpx] * lo[tap];
				    for (i = tap + 2; i < lsz; i += 2) {
					    if (lpx == lp0) {
						    if (lle != 0) {
							    lpxstr = 0;
							    lle = 0;
						    } else {
							    lpxstr = pstr;
						    }
					    }
					    if (lpx == lp1) {
						    if (lre != 0) {
							    lpxstr = 0;
							    lre = 0;
						    } else {
							    lpxstr = nstr;
						    }
					    }
					    lpx += lpxstr;
					    newdata[limg] += olddata[lpx] * lo[i];
				    }
				    limg += stride;
			    }


			    if (da_ev != 0) {
				    if (hotap != 0) {
					    hstap = 1;
				    } else {
					    hstap = 0;
				    }

				    if (hsz == 2) {
					    hspx -= hspxstr;
					    fhre = 1;
				    }
			    } else if (hotap != 0)
				    hstap = 2;
			    else {
				    hstap = 1;
			    }

			    for (tap = 1; tap >= hstap; tap--) {
				    hle = hle2;
				    hre = hre2;
				    hpx = hspx;
				    hpxstr = hspxstr;
				    sfac = osfac;
				    if (hsz != 2)
					    fhre = ofhre;

				    for (i = tap; i < hsz; i += 2) {
					    if (hpx == hp0) {
						    if (hle != 0) {
							    hpxstr = 0;
							    hle = 0;
						    } else {
							    hpxstr = pstr;
							    sfac = 1.0f;
						    }
					    }
					    if (hpx == hp1) {
						    if (hre != 0) {
							    hpxstr = 0;
							    hre = 0;
							    if (asym != 0 && da_ev != 0) {
								    hre = 1;
								    fhre--;
								    sfac = (float) fhre;
								    if (sfac == 0.0)
									    hre = 0;
							    }
						    } else {
							    hpxstr = nstr;
							    if (asym != 0)
								    sfac = -1.0f;
						    }
					    }
					    newdata[himg] += olddata[hpx] * hi[i] * sfac;
					    hpx += hpxstr;
				    }
				    himg += stride;
			    }
		    }

		    if (fi_ev == 0)
			    for (i = 0; i < hsz; i++)
				    hi[i] *= -1.0f;
	    }

	    private int intSign(int power) { /* "sign" power */
		    int cnt;        /* counter */
		    int num = -1;   /* sign return value */

		    if (power == 0) {
			    return 1;
		    }

		    for (cnt = 1; cnt < power; cnt++) {
			    num *= -1;
		    }

		    return num;
	    }

	    private byte[] convertImageToByte(float[] img, int width, int height, float mShift, float rScale) {
		    byte[] data = new byte[width * height];

		    int idx = 0;
		    for (int r = 0; r < height; r++) {
			    for (int c = 0; c < width; c++) {
				    float pixel = (img[idx] * rScale) + mShift;
				    pixel += 0.5f;

				    if (pixel < 0.0) {
					    data[idx] = 0; /* neg pix poss after quantization */
				    } else if (pixel > 255.0) {
					    data[idx] = (byte) 255;
				    } else {
					    data[idx] = (byte) pixel;
				    }
				    idx++;
			    }
		    }

		    return data;
	    }
    }
}
