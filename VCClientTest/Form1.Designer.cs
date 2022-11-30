namespace VCClientTest
{
    partial class Form1
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
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.serverLocal = new System.Windows.Forms.RadioButton();
            this.serverRemote = new System.Windows.Forms.RadioButton();
            this.remoteIP = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 50;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // listBox1
            // 
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point(12, 60);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(846, 160);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 223);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "label1";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Location = new System.Drawing.Point(12, 257);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(846, 187);
            this.pictureBox1.TabIndex = 2;
            this.pictureBox1.TabStop = false;
            // 
            // serverLocal
            // 
            this.serverLocal.AutoSize = true;
            this.serverLocal.Enabled = false;
            this.serverLocal.Location = new System.Drawing.Point(12, 12);
            this.serverLocal.Name = "serverLocal";
            this.serverLocal.Size = new System.Drawing.Size(130, 17);
            this.serverLocal.TabIndex = 3;
            this.serverLocal.TabStop = true;
            this.serverLocal.Text = "Local loopback server";
            this.serverLocal.UseVisualStyleBackColor = true;
            // 
            // serverRemote
            // 
            this.serverRemote.AutoSize = true;
            this.serverRemote.Enabled = false;
            this.serverRemote.Location = new System.Drawing.Point(12, 35);
            this.serverRemote.Name = "serverRemote";
            this.serverRemote.Size = new System.Drawing.Size(94, 17);
            this.serverRemote.TabIndex = 4;
            this.serverRemote.TabStop = true;
            this.serverRemote.Text = "Remote server";
            this.serverRemote.UseVisualStyleBackColor = true;
            // 
            // remoteIP
            // 
            this.remoteIP.Enabled = false;
            this.remoteIP.Location = new System.Drawing.Point(112, 34);
            this.remoteIP.Name = "remoteIP";
            this.remoteIP.Size = new System.Drawing.Size(100, 20);
            this.remoteIP.TabIndex = 5;
            this.remoteIP.Text = "127.0.0.1";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(870, 456);
            this.Controls.Add(this.remoteIP);
            this.Controls.Add(this.serverRemote);
            this.Controls.Add(this.serverLocal);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.listBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.RadioButton serverLocal;
        private System.Windows.Forms.RadioButton serverRemote;
        private System.Windows.Forms.TextBox remoteIP;
    }
}

