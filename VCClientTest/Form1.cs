#define ULAW

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
                return Name;//$"name={Name}  systemRate={SystemRate}  speakerMode={SpeakerMode}  spkModeChannels={SpeakerModeChannels}  driverState={DriverState}";
            }
        }


        public void LogMsg(string s)
        {
            using (PendingLogMessagesCrit.Lock)
                PendingLogMessages.Add("[Client] " + s);
        }

        TcpClient server = null;


        static ACAudioVCServer.CritSect PendingLogMessagesCrit = new ACAudioVCServer.CritSect();
        static List<string> PendingLogMessages = new List<string>();

        static void ServerLogCallback(string s)
        {
            using (PendingLogMessagesCrit.Lock)
                PendingLogMessages.Add("[Server] " + s);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ACAudioVCServer.Server.LogCallback = ServerLogCallback;
            ACAudioVCServer.Server.Init();


            // connect to server
            {
                server = new TcpClient();

                server.Connect("127.0.0.1", 42420);

                ACAudioVCServer.Packet serverInfo = ACAudioVCServer.Packet.Receive(server);

                int sampleRate = serverInfo.ReadInt();


                LogMsg($"Received server info: sampleRate={sampleRate}");


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



        FMOD.Sound receiveStream = null;
        FMOD.Channel receiveChannel = null;
        private byte[] DumbReceiveSamples(int len)
        {
#if true
            byte[] rBuf = new byte[len];

            int bytesToCopy;
            using (receiveBufferCrit.Lock)
            {
                bytesToCopy = Math.Min(len, receiveBuffer.Count);
                if (bytesToCopy > 0)
                {
                    receiveBuffer.CopyTo(0, rBuf, 0, bytesToCopy);
                    receiveBuffer.RemoveRange(0, bytesToCopy);
                }
            }

            // if we didnt have enough samples, fill the rest with silence
            for (int x = bytesToCopy; x < len; x++)
                rBuf[x] = 0;// should this be a 16-bit mid-range value like 32768 instead?

            LogMsg("SAMPLE CALLBACK" + (bytesToCopy < len ? " (STARVED)" : string.Empty));

            return rBuf;
#else
            int bytes = Math.Min(len, receiveBuffer.Count);
            if (bytes <= 0)
                return null;

            byte[] rBuf = new byte[bytes];

            receiveBuffer.CopyTo(0, rBuf, 0, bytes);
            receiveBuffer.RemoveRange(0, bytes);

            return rBuf;
#endif
        }


        ACAudioVCServer.CritSect receiveBufferCrit = new ACAudioVCServer.CritSect();
        List<byte> receiveBuffer = new List<byte>();


        int receiveBuffers = 0;

        DateTime lastReceivedPacketTime = new DateTime();

        DateTime recordTimestamp = new DateTime();
        private void timer1_Tick(object sender, EventArgs e)
        {
            using (PendingLogMessagesCrit.Lock)
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

                // anything to receieve?
                if (server != null)
                {
                    ACAudioVCServer.Packet packet = ACAudioVCServer.Packet.Receive(server, 0);

                    if (packet != null)
                    {
                    LogMsg("Received packet");

                        receiveBuffers++;
                    lastReceivedPacketTime = DateTime.Now;

                        int numSamples = packet.ReadInt();

                    int receiveBufferSize;
                    using (receiveBufferCrit.Lock)
                    {
#if ULAW
                        if (numSamples > 0)
                        {
                            byte[] ulaw = packet.ReadBytes(numSamples);

                            byte[] linear = WinSound.Utils.MuLawToLinear(ulaw, 16, 1);

                            receiveBuffer.AddRange(linear);




                            /*short[] sbuf = new short[linear.Length / 2];
                            for(int x=0; x<sbuf.Length; x++)
                            {
                                sbuf[x] = (short)(linear[x * 2 + 0] | (linear[x * 2 + 1] << 8));
                            }


                            int bufMin = int.MaxValue;
                            int bufMax = int.MinValue;
                            long bufAvg = 0;
                            if (sbuf.Length > 0)
                            {
                                foreach (short s in sbuf)
                                {
                                    bufMin = Math.Min(bufMin, s);
                                    bufMax = Math.Max(bufMax, s);
                                    bufAvg += s;
                                }
                                bufAvg /= sbuf.Length;

                                LogMsg($"min:{bufMin}  max:{bufMax}   avg:{bufAvg}");
                            }*/
                        }
#else

                        for (int x = 0; x < numSamples; x++)
                        {
                            //16-bit samples
                            receiveBuffer.Add(packet.ReadByte());
                            receiveBuffer.Add(packet.ReadByte());
                        }
#endif

                        receiveBufferSize = receiveBuffer.Count;
                    }

                        if (receiveStream == null)
                        {
                        int desiredMsec = 500;  // wait until we have this much audio before initializing stream
                        int desiredBytes = 44100 * 2 * desiredMsec / 1000;

                        if (receiveBufferSize >= desiredBytes)
                        {
                            // pull out the desired bytes from receive buffer and provide to stream immediately
                           /* byte[] buf = new byte[desiredBytes];
                            receiveBuffer.CopyTo(0, buf, 0, desiredBytes);
                            receiveBuffer.RemoveRange(0, desiredBytes);*/

                            LogMsg("Create/play receive stream");

                            receiveStream = CreatePlaybackStream(desiredMsec/*playback delay*/, 50/*match client's mic sampling frequency / expected packet size?*/, DumbReceiveSamples);


                            // HAXXX  this needs to be "jitter buffer"'d
                            Audio.fmod.playSound(receiveStream, null, false, out receiveChannel);
                        }
                        }
                    } else
                {
                    // if there's nothing left in the receive buffer, and its been a while since we receieved anything new, then kill stream
                    if(receiveStream != null)
                    {
                        int receiveBufferSize;
                        using (receiveBufferCrit.Lock)
                            receiveBufferSize = receiveBuffer.Count;

                            if (receiveBufferSize == 0 && DateTime.Now.Subtract(lastReceivedPacketTime).TotalMilliseconds > 500)
                            {
                                receiveChannel.stop();
                                receiveChannel = null;

                                receiveStream.release();
                                receiveStream = null;

                                lastReceivedPacketTime = new DateTime();
                            }
                    }
                }
                }


            label2.Text = $"recieveStream:{(receiveStream != null ? "valid" : "null")}   receiveBuffer:{receiveBuffer.Count}";




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


                int bufMin = int.MaxValue;
                int bufMax = int.MinValue;
                long bufAvg = 0;
                if (buf.Length > 0)
                {
                    foreach (short s in buf)
                    {
                        bufMin = Math.Min(bufMin, s);
                        bufMax = Math.Max(bufMax, s);
                        bufAvg += s;
                    }
                    bufAvg /= buf.Length;
                }


                label1.Text = $"record position: {recordPosition}    buf:{buf.Length}    receivePackets:{receiveBuffers}    min:{bufMin}   max:{bufMax}    avg:{bufAvg}";


                // uhh whatever just send the audio packet
                if (server != null)
                {
                    ACAudioVCServer.Packet packet = new ACAudioVCServer.Packet();

#if ULAW
                    byte[] linear = new byte[buf.Length * 2];
                    for(int x=0; x<buf.Length; x++)
                    {
                        linear[x * 2 + 0] = (byte)(buf[x]&0xFF);
                        linear[x * 2 + 1] = (byte)((buf[x] >> 8)&0xFF);
                    }

                    byte[] ulaw = WinSound.Utils.LinearToMulaw(linear, 16, 1);

                    packet.WriteInt(ulaw.Length);
                    for (int x = 0; x < ulaw.Length; x++)
                        packet.WriteByte(ulaw[x]);



                    // check decompressed results against original
                    {
                        byte[] newlinear = WinSound.Utils.MuLawToLinear(ulaw, 16, 1);

                        short[] sbuf = new short[newlinear.Length / 2];
                        for (int x = 0; x < sbuf.Length; x++)
                        {
                            sbuf[x] = (short)(newlinear[x * 2 + 0] | (newlinear[x * 2 + 1] << 8));
                        }


                        int tbufMin = int.MaxValue;
                        int tbufMax = int.MinValue;
                        long tbufAvg = 0;
                        if (sbuf.Length > 0)
                        {
                            foreach (short s in sbuf)
                            {
                                tbufMin = Math.Min(tbufMin, s);
                                tbufMax = Math.Max(tbufMax, s);
                                tbufAvg += s;
                            }
                            tbufAvg /= sbuf.Length;

                            LogMsg($"min:{bufMin}/{tbufMin}  max:{bufMax}/{tbufMax}   avg:{bufAvg}/{tbufAvg}");
                        }
                    }
#else
                    packet.WriteInt(buf.Length);
                    foreach (short s in buf)
                        packet.WriteShort((ushort)s);
#endif

                    packet.Send(server);

                    LogMsg("Sent packet");
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

        public delegate byte[] GetStreamSamples(int maxlen);
        private static GetStreamSamples gss = null;

        public static FMOD.SOUND_PCMREADCALLBACK PCMReadCallbackDelegate = new FMOD.SOUND_PCMREADCALLBACK(PCMReadCallback);
        public static FMOD.RESULT PCMReadCallback(IntPtr soundraw, IntPtr data, uint datalen)
        {
            FMOD.Sound sound = new FMOD.Sound(soundraw);

            //IntPtr userdata;
            //sound.getUserData(out userdata);

            byte[] buf = gss((int)datalen);
            if(buf != null)
                Marshal.Copy(buf, 0, data, buf.Length);

            return FMOD.RESULT.OK;
        }

        public static FMOD.Sound CreatePlaybackStream(int bufferMsec, int samplingMsec, GetStreamSamples callback)
        {
            gss = callback;

            int channels = 1;
            int rate = 44100;
            int bitDepth = 16;

            FMOD.Sound sound;
            FMOD.CREATESOUNDEXINFO cs = new FMOD.CREATESOUNDEXINFO();
            cs.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
            cs.length = (uint)(bufferMsec * rate * 2 / 1000);//(uint)buf.Length;//(uint)(rate * sizeof(short) * channels * 2);//(uint)buf.Length;
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
            cs.decodebuffersize = (uint)(samplingMsec * rate * 2 / 1000);//(uint)(cs.length / 4);//(uint)(10 * rate * 2 / 1000);//call pcm callback with small buffer size and frequently?   //cs.length;// (uint)buflen;
            cs.initialsubsound = 0;
            cs.numsubsounds = 0;
            cs.inclusionlist = IntPtr.Zero;
            cs.inclusionlistnum = 0;
            cs.pcmreadcallback = PCMReadCallbackDelegate;
            cs.pcmsetposcallback = null;
            cs.nonblockcallback = null;
            cs.dlsname = IntPtr.Zero;
            cs.encryptionkey = IntPtr.Zero;
            cs.maxpolyphony = 0;
            cs.userdata = IntPtr.Zero;// Marshal.GetFunctionPointerForDelegate(callback);
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

            FMOD.RESULT result = Audio.fmod.createStream((byte[])null, FMOD.MODE.CREATESTREAM | FMOD.MODE._2D | FMOD.MODE.OPENUSER | FMOD.MODE.LOOP_NORMAL, ref cs, out sound);
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

        public bool Loopback = false;
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
