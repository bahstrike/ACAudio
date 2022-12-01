using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ACAudioVCServer
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        public void Log(string s)
        {
            while (logLB.Items.Count > 100)
                logLB.Items.RemoveAt(0);

            logLB.TopIndex = logLB.Items.Add(s);
        }

        CritSect pendingLogMessagesCrit = new CritSect();
        List<string> pendingLogMessages = new List<string>();
        void LogCallback(string s)
        {
            using (pendingLogMessagesCrit.Lock)
                pendingLogMessages.Add(s);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Log("Startup");

            Server.LogCallback = LogCallback;
            Server.Init();

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            using (pendingLogMessagesCrit.Lock)
            {
                foreach (string s in pendingLogMessages)
                    Log(s);
                pendingLogMessages.Clear();
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Server.Shutdown();
        }
    }
}
