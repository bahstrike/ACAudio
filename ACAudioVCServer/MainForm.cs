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

            bitDepthCombo.SelectedIndex = 2;// ulaw 16bit
            sampleRateCombo.SelectedIndex = 2;//8000
            UpdateStreamInfo();//dont wait for after thread started & timer to update thread info

            Server.LogCallback = LogCallback;
            Server.Init();

        }

        void UpdateStreamInfo()
        {
            // if our current/last stream info has changed, we should issue an update
            bool ulaw;
            int bitDepth;
            int sampleRate;
            switch (bitDepthCombo.SelectedIndex)
            {
                case 0:
                    ulaw = false;
                    bitDepth = 8;
                    break;

                case 1:
                    ulaw = false;
                    bitDepth = 16;
                    break;

                default://case 2:
                    ulaw = true;
                    bitDepth = 16;
                    break;
            }

            sampleRate = int.Parse(sampleRateCombo.SelectedItem as string);

            Server.CurrentStreamInfo = new Server.StreamInfo(ulaw, bitDepth, sampleRate);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            using (pendingLogMessagesCrit.Lock)
            {
                foreach (string s in pendingLogMessages)
                    Log(s);
                pendingLogMessages.Clear();
            }


            UpdateStreamInfo();//in case we dont have a indexchanged or something
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Server.Shutdown();
        }
    }
}
