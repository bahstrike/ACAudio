//#define SELFHOST

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Xml;
using Smith;
using System.Net.NetworkInformation;
using ACACommon;
using ACAVoiceClient;

namespace VCClientTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }


        public static void LogMsg(string s)
        {
            using (PendingLogMessagesCrit.Lock)
                PendingLogMessages.Add("[Client] " + s);
        }


        static CritSect PendingLogMessagesCrit = new CritSect();
        static List<string> PendingLogMessages = new List<string>();

        static void ServerLogCallback(string s)
        {
            using (PendingLogMessagesCrit.Lock)
                PendingLogMessages.Add("[Server] " + s);
        }

        static void ClientLogCallback(string s)
        {
            using (PendingLogMessagesCrit.Lock)
                PendingLogMessages.Add("[Client] " + s);
        }


        private void Form1_Load(object sender, EventArgs e)
        {
#if SELFHOST
            ACAudioVCServer.Server.LogCallback = ServerLogCallback;
            ACAudioVCServer.Server.CurrentStreamInfo = new ACAudioVCServer.Server.StreamInfo(true, 16, 8000);//we have to provide some default
            ACAudioVCServer.Server.Init();
#endif


            // get FMOD set up before invoking voicechat client
            Audio.Init(32);


            VCClient.LogCallback = ClientLogCallback;
            VCClient.Loopback = true;
            VCClient.Speak3D = false;
            VCClient.Init("VCClientTest", "toon"+MathLib.random.Next(20), MathLib.random.Next(), "192.168.5.2");


            // ------------------------------------------------------------------------------------------------------------
            // USE THIS IN FINAL PLUGIN TO AUTODETECT VOICE HOST (assuming the server admin is running ACAudioVCServer)
            string serverAddress = Utils.DetectServerAddressViaThwargle("blahblahblah");
            // ------------------------------------------------------------------------------------------------------------


            VCClient.RecordDeviceEntry[] recordDevices = VCClient.QueryRecordDevices();
            foreach(VCClient.RecordDeviceEntry rde in recordDevices)
            {
                int index = listBox1.Items.Add(rde);

                if ((rde.DriverState & FMOD.DRIVER_STATE.DEFAULT) != 0)
                    listBox1.SelectedIndex = index;
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // shutdown voice client before killing FMOD
            VCClient.Shutdown();

            Audio.Shutdown();

#if SELFHOST
            ACAudioVCServer.Server.Shutdown();
#endif
        }


        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        private void timer1_Tick(object sender, EventArgs e)
        {
            using (PendingLogMessagesCrit.Lock)
            {
                if (PendingLogMessages.Count > 0)
                {
                    listBox2.BeginUpdate();

                    while (PendingLogMessages.Count > 0)
                    {
                        while (listBox2.Items.Count > 100)
                            listBox2.Items.RemoveAt(0);

                        listBox2.TopIndex = listBox2.Items.Add(PendingLogMessages[0]);
                        PendingLogMessages.RemoveAt(0);
                    }

                    listBox2.EndUpdate();

                    listBox2.Update();
                }
            }



            VCClient.CurrentRecordDevice = listBox1.SelectedItem as VCClient.RecordDeviceEntry;

            VCClient.PushToTalkEnable = (GetAsyncKeyState((int)' ') & 0x8000) != 0;

            VCClient.Process(0.05);


            //label2.Text = $"connected:{(server != null && server.Connected ? "yes" : "no")}  recieveStreams:{ReceiveStreams.Count}";
            label2.Text = $"connected:{(VCClient.IsConnected ? "yes" : "no")}   receiveStreams:{VCClient.NumActiveReceiveStreams}";


            if (VCClient.CurrentRecordDevice == null)
                label1.Text = "no record device";
            else if (VCClient.IsRecording)
                label1.Text = "not recording";
            else
                label1.Text = $"recording";// position: {recordPosition}    buf:{buf.Length}";




#if false
                int width = pictureBox1.Width;
                int height = pictureBox1.Height;
                Bitmap bmp = new Bitmap(width, height);
                for (int x = 0; x < Math.Min(width, buf.Length/ bytesPerSample); x++)
                {
                    int index = (int)(x * (buf.Length/ bytesPerSample) / width);// % 1;
                    if (ServerBitDepth == 16)
                    {
                        short val = (short)((int)buf[index*2+0] | ((int)buf[index*2 + 1] << 8));
                        bmp.SetPixel(x, (val + 32768) * height / 65536, Color.Black);
                    } else
                    {
                        sbyte val = (sbyte)buf[x];
                        bmp.SetPixel(x, (val + 128) * height / 256, Color.Black);
                    }
                }
                pictureBox1.Image = bmp;
#endif


            Audio.Process(0.05, 0.05, Vec3.Zero, Vec3.Zero, Vec3.PosZ, Vec3.PosY);
        }



        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //OpenRecordDevice();
        }
    }
}
