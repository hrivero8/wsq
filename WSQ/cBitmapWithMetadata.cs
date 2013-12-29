using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wsqm
{
    internal class cBitmapWithMetadata : cBitmap
    {
        private const long serialVersionUID = -4243273616650162026L;

        private Dictionary<String, String> metadata = new Dictionary<String, String>();
	    private List<String> comments = new List<String>();


        public cBitmapWithMetadata(byte[] pixels, int width, int height,
                                    int ppi, int depth, int lossyflag):this(pixels, width, height, ppi, depth, lossyflag, null)
        {
            
            
        }
	
	    public cBitmapWithMetadata(byte[] pixels, int width, int height, 
                                int ppi, int depth, int lossyflag, 
                                Dictionary<String,String> metadata, 
                                String[] comments=null):base(pixels, width, height, ppi, depth, lossyflag) {

            if (metadata != null)
            {
                foreach (KeyValuePair<String, String> entry in metadata)
                {
                    this.metadata.Add(entry.Key, entry.Value);
                }
            }
            //if (metadata != null)
            //    this.metadata.putAll(metadata);
		    if (comments != null)
                foreach(String s in comments)			    
				    if (s != null)
				    this.comments.Add(s);
	    }

        public Dictionary<String, String> getMetadata()
        {
		    return metadata;
	    }
	
	    public List<String> getComments() {
		    return comments;
	    }
    }
}
