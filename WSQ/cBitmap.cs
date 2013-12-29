using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wsqm
{
    internal class cBitmap
    {
        private const long serialVersionUID = -8632563339133022850L;
	
	    private int width;
        private int height;
        private int ppi;
        private int depth;
        private int lossyflag;

        private byte[] pixels;
        private int length;

        public cBitmap(byte[] pixels, int width, int height, int ppi, int depth, int lossyflag) {
            this.pixels = pixels;
            this.length = pixels != null ? pixels.Length : 0;

            this.width = width;
            this.height = height;
            this.ppi = ppi;
            this.depth = depth;
            this.lossyflag = lossyflag;
        }


        public int getWidth() {
            return width;
        }

        public int getHeight() {
            return height;
        }

        public int getPpi() {
            return ppi;
        }

        public byte[] getPixels() {
            return pixels;
        }

        public int getLength() {
            return length;
        }

        public int getDepth() {
            return depth;
        }

        public int getLossyflag() {
            return lossyflag;
        }
    
        public String toString() {
            StringBuilder result = new StringBuilder();
    	    result.Append("Bitmap [");
            result.Append(width);
            result.Append(" x "); result.Append(height);
            result.Append(" x "); result.Append(depth); result.Append(", ");
            result.Append("ppi = "); result.Append(ppi); result.Append(", ");
            result.Append("lossy = "); result.Append(lossyflag);
            result.Append("]");
    	    return result.ToString();
        }
    }
}
