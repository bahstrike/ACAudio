using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ACACommon;
using Smith;

namespace VCStressTest
{
    public partial class Form1 : Form
    {
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


        public Form1()
        {
            InitializeComponent();
        }

        List<VCClient> Clients = new List<VCClient>();

        private void Form1_Load(object sender, EventArgs e)
        {
            VCClient.LogCallback = LogCallback;


        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            foreach (VCClient client in Clients)
                client.Shutdown();
            Clients.Clear();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            using (pendingLogMessagesCrit.Lock)
            {
                foreach (string s in pendingLogMessages)
                    Log(s);
                pendingLogMessages.Clear();
            }

            if(Clients.Count < 500)
            {
                VCClient client = new VCClient();

                client.Init("STRESSTEST" + Clients.Count.ToString(), "blahblah", 0x4000000 + Clients.Count);
                client.ServerIP = "192.168.5.2";

                Clients.Add(client);
            }


            foreach (VCClient client in Clients)
            {
                // VERY rare chance to change to one of several allegiances
                if (MathLib.random.NextDouble() < 0.00001)
                {
                    client.PlayerAllegianceID = 0x1000000 + MathLib.random.Next(10);
                }

                // rare chance to change to one of several fellowships
                if (MathLib.random.NextDouble() < 0.001)
                {
                    client.PlayerFellowshipID = 0x1000000 + MathLib.random.Next(10);
                }

                // pretty decent chance to move or portal
                if (MathLib.random.NextDouble() < 0.15)
                {
                    client.PlayerPosition = ACACommon.Position.FromLocal((uint)(0x1000000 + MathLib.random.Next(10)), Vec3.RandomUnit * 200.0);
                }


                // some chance to use voicechat
                if (MathLib.random.NextDouble() < 0.05)
                {
                    client.CurrentVoice = (StreamInfo.VoiceChannel)MathLib.random.Next(3);
                }
                else
                    client.CurrentVoice = StreamInfo.VoiceChannel.Invalid;


                client.Process(0.05);
            }
        }
    }
}
