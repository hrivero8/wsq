using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Wsqm;

namespace wsqEncodeDecode
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Decode_Click(object sender, EventArgs e)
        {
            WSQ dec = new WSQ();
            String[] comentario = null;

            comentario = new String[2];
            comentario[0] = "humberto";
            comentario[1] = "humberto";

            dec.EnconderFile(@"D:\borrar\prueba2.bmp", @"D:\borrar\prueba2.wsq",
                                    comentario,
                                    0.75f);
        }

        private void Encode_Click(object sender, EventArgs e)
        {
            WSQ dec=new WSQ();

            dec.DecoderFile(@"D:\borrar\prueba1.wsq", @"D:\borrar\prueba1.tif");
        }
    }
}
