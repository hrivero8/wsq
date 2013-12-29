namespace wsqEncodeDecode
{
    partial class Form1
    {
        /// <summary>
        /// Variable del diseñador requerida.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén utilizando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben eliminar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido del método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.Decode = new System.Windows.Forms.Button();
            this.Encode = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // Decode
            // 
            this.Decode.Location = new System.Drawing.Point(51, 39);
            this.Decode.Name = "Decode";
            this.Decode.Size = new System.Drawing.Size(75, 23);
            this.Decode.TabIndex = 0;
            this.Decode.Text = "Decode";
            this.Decode.UseVisualStyleBackColor = true;
            this.Decode.Click += new System.EventHandler(this.Decode_Click);
            // 
            // Encode
            // 
            this.Encode.Location = new System.Drawing.Point(51, 123);
            this.Encode.Name = "Encode";
            this.Encode.Size = new System.Drawing.Size(75, 23);
            this.Encode.TabIndex = 1;
            this.Encode.Text = "Encode";
            this.Encode.UseVisualStyleBackColor = true;
            this.Encode.Click += new System.EventHandler(this.Encode_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(248, 175);
            this.Controls.Add(this.Encode);
            this.Controls.Add(this.Decode);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button Decode;
        private System.Windows.Forms.Button Encode;
    }
}

