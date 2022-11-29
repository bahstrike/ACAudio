using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace ACAudioVCServer
{
    public partial class MainForm : Form
    {
        private static TcpListener listener = null;
        private static TcpClient client = null;

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

        private void Form1_Load(object sender, EventArgs e)
        {
            Log("Startup");

            if (listener == null)
            {
                listener = new TcpListener(IPAddress.Any, 43420);
                listener.Start();
            }

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (client == null)
            {
                client = listener.AcceptTcpClient();
                client.NoDelay = true;

                // connected = true
                using (MemoryStream rms = new MemoryStream())
                {
                    BinaryWriter rbw = new BinaryWriter(rms);


                }
            }
        }
    }
}
