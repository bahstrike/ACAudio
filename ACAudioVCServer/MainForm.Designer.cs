namespace ACAudioVCServer
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.logLB = new System.Windows.Forms.ListBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.sampleRateCombo = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.bitDepthCombo = new System.Windows.Forms.ComboBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // logLB
            // 
            this.logLB.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.logLB.FormattingEnabled = true;
            this.logLB.Location = new System.Drawing.Point(12, 12);
            this.logLB.Name = "logLB";
            this.logLB.Size = new System.Drawing.Size(621, 498);
            this.logLB.TabIndex = 0;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 50;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.sampleRateCombo);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.bitDepthCombo);
            this.groupBox1.Location = new System.Drawing.Point(639, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(234, 220);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Stream Info";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 74);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(68, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Sample Rate";
            // 
            // sampleRateCombo
            // 
            this.sampleRateCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.sampleRateCombo.FormattingEnabled = true;
            this.sampleRateCombo.Items.AddRange(new object[] {
            "6000",
            "7333",
            "8000",
            "11025",
            "16000",
            "22050",
            "24000",
            "32000",
            "44100"});
            this.sampleRateCombo.Location = new System.Drawing.Point(9, 90);
            this.sampleRateCombo.Name = "sampleRateCombo";
            this.sampleRateCombo.Size = new System.Drawing.Size(121, 21);
            this.sampleRateCombo.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(122, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Bit-Depth / Compression";
            // 
            // bitDepthCombo
            // 
            this.bitDepthCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.bitDepthCombo.FormattingEnabled = true;
            this.bitDepthCombo.Items.AddRange(new object[] {
            "8-bit (full)",
            "16-bit (full)",
            "16-bit (µ-law)"});
            this.bitDepthCombo.Location = new System.Drawing.Point(9, 38);
            this.bitDepthCombo.Name = "bitDepthCombo";
            this.bitDepthCombo.Size = new System.Drawing.Size(121, 21);
            this.bitDepthCombo.TabIndex = 0;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(885, 513);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.logLB);
            this.Name = "MainForm";
            this.Text = "ACAudio VoiceChat Server";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox logLB;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox bitDepthCombo;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox sampleRateCombo;
    }
}

