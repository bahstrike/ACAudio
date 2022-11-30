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
using Smith;

namespace VCClientTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        class RecordDeviceEntry
        {
            public int ID;
            public string Name;
            public Guid Guid;
            public int SystemRate;
            public FMOD.SPEAKERMODE SpeakerMode;
            public int SpeakerModeChannels;
            public FMOD.DRIVER_STATE DriverState;

            public override string ToString()
            {
                return $"name={Name}  systemRate={SystemRate}  speakerMode={SpeakerMode}  spkModeChannels={SpeakerModeChannels}  driverState={DriverState}";
            }
        }


        TcpClient server = null;
        

        private void Form1_Load(object sender, EventArgs e)
        {
            ACAudioVCServer.Server.Init();


            // connect to server
            {
                server = new TcpClient();

                server.Connect("127.0.0.1", 42420);

                ACAudioVCServer.Packet serverInfo = ACAudioVCServer.Packet.Receive(server);

                int sampleRate = serverInfo.ReadInt();



                ACAudioVCServer.Packet clientInfo = new ACAudioVCServer.Packet();

                clientInfo.WriteString("account lol");
                clientInfo.WriteString("toon name");
                clientInfo.WriteInt(1337);//weenie ID

                clientInfo.Send(server);
            }



            Audio.Init(32);

            int numDrivers, numConnected;
            Audio.fmod.getRecordNumDrivers(out numDrivers, out numConnected);

            for (int x = 0; x < numDrivers; x++)
            {
                StringBuilder sbName = new StringBuilder(256);
                RecordDeviceEntry rde = new RecordDeviceEntry();
                rde.ID = x;
                Audio.fmod.getRecordDriverInfo(x, sbName, sbName.Capacity, out rde.Guid, out rde.SystemRate, out rde.SpeakerMode, out rde.SpeakerModeChannels, out rde.DriverState);
                rde.Name = sbName.ToString();

                // skip loopback entries?
                if (rde.Name.Contains("[loopback]"))
                    continue;

                int index = listBox1.Items.Add(rde);

                if ((rde.DriverState & FMOD.DRIVER_STATE.DEFAULT) != 0)
                    listBox1.SelectedIndex = index;
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            CloseRecordDevice();
            Audio.Shutdown();

            server.Close();

            ACAudioVCServer.Server.Shutdown();
        }


        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        uint lastRecordPosition = 0;



        class AudioBuffer
        {
            public DateTime Timestamp;
            public short[] Buffer;
        }

        // need a real "jitter buffer"  (and probably just store the compressed buffers too)
        List<AudioBuffer> buffers = new List<AudioBuffer>();


        int receiveBuffers = 0;

        DateTime recordTimestamp = new DateTime();
        private void timer1_Tick(object sender, EventArgs e)
        {
            // anything to receieve?
            if(server != null)
            {
                ACAudioVCServer.Packet packet = ACAudioVCServer.Packet.Receive(server, 0);

                if(packet != null)
                {
                    receiveBuffers++;
                }
            }





            if ((GetAsyncKeyState((int)' ') & 0x8000) != 0)
            {
                if (recordTimestamp == new DateTime())
                    OpenRecordDevice();

                recordTimestamp = DateTime.Now;
            }
            else
            {
                // not holding push-to-talk key..  wait a little extra delay before ending
                if (recordTimestamp != new DateTime())
                {
                    if (DateTime.Now.Subtract(recordTimestamp).TotalMilliseconds > 350)
                    {
                        CloseRecordDevice();
                        recordTimestamp = new DateTime();

                        buffers.Clear();
                    }
                }
            }


            if (CurrentRecordDevice == null)
                label1.Text = "no record device";
            else
            if (recordBuffer == null)
                label1.Text = "not recording";
            else
            {
                uint recordPosition;
                Audio.fmod.getRecordPosition(CurrentRecordDevice.ID, out recordPosition);

                int blocklength = (int)recordPosition - (int)lastRecordPosition;
                if (blocklength < 0)
                {
                    uint recordBufferLength;
                    recordBuffer.getLength(out recordBufferLength, FMOD.TIMEUNIT.PCM);

                    blocklength += (int)recordBufferLength;
                }

                uint bytesPerSample = 1/*channels*/ * 2/*bitdepth*/;
                IntPtr ptr1, ptr2;
                uint len1, len2;
                recordBuffer.@lock(lastRecordPosition * bytesPerSample, (uint)blocklength * bytesPerSample, out ptr1, out ptr2, out len1, out len2);

                short[] buf = new short[len1/2 + len2/2];
                if(ptr1 != IntPtr.Zero && len1 > 0)
                    Marshal.Copy(ptr1, buf, 0, (int)len1/2);

                if (ptr2 != IntPtr.Zero && len2 > 0)
                    Marshal.Copy(ptr2, buf, (int)len1/2, (int)len2/2);

                recordBuffer.unlock(ptr1, ptr2, len1, len2);

                lastRecordPosition = recordPosition;


                AudioBuffer abuf = new AudioBuffer();
                abuf.Timestamp = DateTime.Now;
                abuf.Buffer = buf;

                buffers.Add(abuf);
                int totalLen = 0;
                int maxLen = 0;
                foreach (AudioBuffer b in buffers)
                {
                    totalLen += b.Buffer.Length;
                    maxLen = Math.Max(maxLen, b.Buffer.Length);
                }


                label1.Text = $"record position: {recordPosition}    buf:{buf.Length}   maxBuf:{maxLen}  total:{((double)totalLen/44100.0).ToString("#0.00")}sec    receivePackets:{receiveBuffers}";



                // uhh whatever just send the audio packet
                if (server != null)
                {
                    ACAudioVCServer.Packet packet = new ACAudioVCServer.Packet();

                    foreach (short s in buf)
                        packet.WriteShort((ushort)s);

                    packet.Send(server);
                }



                int width = pictureBox1.Width;
                int height = pictureBox1.Height;
                Bitmap bmp = new Bitmap(width, height);
                for(int x=0; x<Math.Min(width, buf.Length); x++)
                {
                    int index = (x * buf.Length / width);// % 1;
                    short val = buf[x];//(short)((int)buf[index] | ((int)buf[index + 1] << 8));
                    bmp.SetPixel(x, (val+32768) * height / 65536, Color.Black);
                }
                pictureBox1.Image = bmp;
            }


            Audio.Process(0.05, 0.05, Vec3.Zero, Vec3.Zero, Vec3.PosZ, Vec3.PosY);
        }



        FMOD.Sound CreateRecordBuffer()
        {
            int channels = 1;
            int bitDepth = 16;
            int rate = 44100;

            FMOD.Sound sound;
            FMOD.CREATESOUNDEXINFO cs = new FMOD.CREATESOUNDEXINFO();
            cs.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
            cs.length = (uint)(rate * sizeof(short) * channels * 2);//(uint)buf.Length;
            cs.fileoffset = 0;
            cs.numchannels = channels;
            cs.defaultfrequency = rate;
            switch (bitDepth)
            {
                case 8:
                    cs.format = FMOD.SOUND_FORMAT.PCM8;
                    break;

                case 16:
                    cs.format = FMOD.SOUND_FORMAT.PCM16;
                    break;

                default:
                    cs.format = FMOD.SOUND_FORMAT.NONE;
                    break;
            }
            cs.decodebuffersize = 0;
            cs.initialsubsound = 0;
            cs.numsubsounds = 0;
            cs.inclusionlist = IntPtr.Zero;
            cs.inclusionlistnum = 0;
            cs.pcmreadcallback = null;
            cs.pcmsetposcallback = null;
            cs.nonblockcallback = null;
            cs.dlsname = IntPtr.Zero;
            cs.encryptionkey = IntPtr.Zero;
            cs.maxpolyphony = 0;
            cs.userdata = IntPtr.Zero;
            cs.fileuseropen = null;
            cs.fileuserclose = null;
            cs.fileuserread = null;
            cs.fileuserseek = null;
            cs.fileuserasyncread = null;
            cs.fileuserasynccancel = null;
            cs.fileuserdata = IntPtr.Zero;
            cs.filebuffersize = 0;
            cs.channelorder = FMOD.CHANNELORDER.DEFAULT;
            cs.channelmask = 0;
            cs.initialsoundgroup = IntPtr.Zero;
            cs.initialseekposition = 0;
            cs.initialseekpostype = 0;
            cs.ignoresetfilesystem = 0;
            cs.audioqueuepolicy = 0;
            cs.minmidigranularity = 0;
            cs.nonblockthreadid = 0;
            cs.fsbguid = IntPtr.Zero;

            FMOD.RESULT result = Audio.fmod.createSound((byte[])null, FMOD.MODE._2D | FMOD.MODE.OPENUSER | FMOD.MODE.LOOP_NORMAL, ref cs, out sound);
            if (result != FMOD.RESULT.OK || sound == null)
            {
                Log.Error("Failed to create sound: " + result.ToString());
                return null;
            }

            return sound;
        }

        RecordDeviceEntry CurrentRecordDevice
        {
            get
            {
                return listBox1.SelectedItem as RecordDeviceEntry;
            }
        }

        void CloseRecordDevice()
        {
            if (CurrentRecordDevice != null && recordBuffer != null)
                Audio.fmod.recordStop(CurrentRecordDevice.ID);

            if(loopbackChannel != null)
            {
                loopbackChannel.stop();
                loopbackChannel = null;
            }

            if(recordBuffer != null)
            {
                recordBuffer.release();
                recordBuffer = null;
            }

            lastRecordPosition = 0;
        }

        public bool Loopback = true;
        FMOD.Channel loopbackChannel = null;

        void OpenRecordDevice()
        {
            CloseRecordDevice();

            if (CurrentRecordDevice == null)
                return;

            recordBuffer = CreateRecordBuffer();
            Audio.fmod.recordStart(CurrentRecordDevice.ID, recordBuffer, true);


            if (Loopback)
            {
                System.Threading.Thread.Sleep(50);
                Audio.fmod.playSound(recordBuffer, null, false, out loopbackChannel);
            }
        }

        FMOD.Sound recordBuffer = null;

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //OpenRecordDevice();
        }
    }
}
