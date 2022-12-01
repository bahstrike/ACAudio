#define SELFHOST

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


        private class SortNewest : IComparer<DateTime>
        {
            public int Compare(DateTime x, DateTime y)
            {
                return y.CompareTo(x);
            }
        }
        string DetectServerAddressViaThwargle(string accountName)
        {
            try
            {
                string thwargPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ThwargLauncher");
                if (!Directory.Exists(thwargPath))
                    return null;

                // check "Running" logs for most recent that matches provided account name
                SortedList<DateTime, string> newestGames = new SortedList<DateTime, string>(new SortNewest());
                foreach (string gameFile in Directory.GetFiles(Path.Combine(thwargPath, "Running"), "*.txt"))
                {
                    DateTime fileTime = new FileInfo(gameFile).LastWriteTime;

                    // skip if not today
                    if (DateTime.Now.Subtract(fileTime).TotalDays > 1)
                        continue;

                    newestGames.Add(fileTime, gameFile);
                }

                // scan most recent files for matching account name and try to scrape server name
                string serverName = null;
                foreach (DateTime fileTime in newestGames.Keys)
                {
                    string gameFile = newestGames[fileTime];

                    using (StreamReader sr = File.OpenText(gameFile))
                    {
                        string fileAccountName = null;
                        string fileServerName = null;
                        while (!sr.EndOfStream && (fileAccountName == null || fileServerName == null))
                        {
                            string ln = sr.ReadLine();
                            if (ln.StartsWith("AccountName:"))
                                fileAccountName = ln.Substring(ln.IndexOf(':') + 1);
                            else if (ln.StartsWith("ServerName:"))
                                fileServerName = ln.Substring(ln.IndexOf(':') + 1);
                        }

                        if (fileAccountName == null || fileAccountName != accountName)
                            continue;

                        serverName = fileServerName;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(serverName))
                    return null;

                // now we need to load all the server XMLs and try to find a match that includes a host address
                foreach (string xmlFile in Directory.GetFiles(Path.Combine(thwargPath, "Servers"), "*.xml"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(xmlFile);

                    foreach (XmlNode serverNode in xmlDoc.DocumentElement.SelectNodes("ServerItem"))
                    {
                        XmlNode node;

                        node = serverNode.SelectSingleNode("name");
                        if (node == null || node.InnerText != serverName)
                            continue;

                        node = serverNode.SelectSingleNode("connect_string");
                        if (node == null)
                            continue;

                        // strip port out
                        string connectString = node.InnerText;
                        int i = connectString.IndexOf(':');
                        if (i != -1)
                            connectString = connectString.Substring(0, i);

                        // we found it
                        return connectString;
                    }
                }
            }
            catch
            {
                // it failed for some dumb reason. oh well.
            }

            return null;
        }


        bool ServerULaw;
        int ServerBitDepth;
        int ServerSampleRate;

        // average length of how long our audio sample packets will consist of
        int ClientPacketMsec
        {
            get
            {
                // we're trying to scrape mic samples and generate a packet every Timer tick so i guess use whatever that timer frequency is
                return timer1.Interval;
            }
        }

        // whatever size playback stream buffer we want.  this also decides the delay before playback.
        int ClientBufferMsec
        {
            get
            {
                return ClientPacketMsec * 10;  // 50msec * 10 = 500msec
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
#if SELFHOST
            ACAudioVCServer.Server.LogCallback = ServerLogCallback;
            ACAudioVCServer.Server.Init();
#endif


            // ------------------------------------------------------------------------------------------------------------
            // USE THIS IN FINAL PLUGIN TO AUTODETECT VOICE HOST (assuming the server admin is running ACAudioVCServer)
            string serverAddress = DetectServerAddressViaThwargle("blahblahblah");
            // ------------------------------------------------------------------------------------------------------------


            // connect to server
            {
                try
                {
                    server = new TcpClient();
                    server.Connect("192.168.5.2"/*"127.0.0.1"*/, 42420);
                }
                catch
                {
                    server = null;
                }


                if (server != null)
                {
                    ACAudioVCServer.Packet clientInfo = new ACAudioVCServer.Packet(ACAudioVCServer.Packet.MessageType.PlayerConnect);

                    clientInfo.WriteString("account lol");
                    clientInfo.WriteString("toon" + Smith.MathLib.random.Next(20));
                    clientInfo.WriteInt(Smith.MathLib.random.Next());//weenie ID

                    clientInfo.Send(server);



                    ACAudioVCServer.Packet serverInfo = ACAudioVCServer.Packet.Receive(server);

                    ServerULaw = serverInfo.ReadBool();
                    ServerBitDepth = serverInfo.ReadInt();
                    ServerSampleRate = serverInfo.ReadInt();


                    LogMsg($"Received server info:  µ-law:{ServerULaw}  bitDepth={ServerBitDepth}  sampleRate={ServerSampleRate}");
                }
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

            // issue disconnect
            if (server != null)
            {
                ACAudioVCServer.Packet packet = new ACAudioVCServer.Packet(ACAudioVCServer.Packet.MessageType.Disconnect);
                packet.WriteString("Client exit");
                packet.Send(server);

                server.Close();
                server = null;
            }

#if SELFHOST
            ACAudioVCServer.Server.Shutdown();
#endif
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

            //LogMsg("SAMPLE CALLBACK" + (bytesToCopy < len ? " (STARVED)" : string.Empty));

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

            // have we been lost connection?
            if(server != null && !server.Connected)
            {
                server.Close();//probably not needed if disconnected but whatever
                server = null;
            }

            // anything to receieve?
            if (server != null)
            {
                ACAudioVCServer.Packet packet = ACAudioVCServer.Packet.Receive(server, 0);
                bool receivedAudioPacket = false;

                if (packet != null)
                {
                    if(packet.Message == ACAudioVCServer.Packet.MessageType.Disconnect)
                    {
                        string reason = packet.ReadString();

                        // we're being ordered to disconnect. server will have already closed their socket. just close ours
                        LogMsg("We have been disconnected from server: " + reason);
                        server.Close();
                        server = null;
                    }

                    if (packet.Message == ACAudioVCServer.Packet.MessageType.DetailAudio)
                    {
                        //LogMsg("Received packet");

                        lastReceivedPacketTime = DateTime.Now;

                        int weenieID = packet.ReadInt();

                        byte[] buf = packet.ReadBuffer();

                        if(buf != null && buf.Length > 0)
                        {
                            receivedAudioPacket = true;


                            int receiveBufferSize;
                            using (receiveBufferCrit.Lock)
                            {
                                if (ServerULaw)
                                    receiveBuffer.AddRange(WinSound.Utils.MuLawToLinear(buf, ServerBitDepth, 1));
                                else
                                    receiveBuffer.AddRange(buf);

                                receiveBufferSize = receiveBuffer.Count;
                            }

                            if (receiveStream == null)
                            {
                                int desiredMsec = ClientBufferMsec;  // wait until we have this much audio before initializing stream
                                int desiredBytes = ServerSampleRate * (ServerBitDepth / 8) * desiredMsec / 1000;

                                if (receiveBufferSize >= desiredBytes)
                                {
                                    // pull out the desired bytes from receive buffer and provide to stream immediately
                                    /* byte[] buf = new byte[desiredBytes];
                                     receiveBuffer.CopyTo(0, buf, 0, desiredBytes);
                                     receiveBuffer.RemoveRange(0, desiredBytes);*/

                                    LogMsg("Create/play receive stream");

                                    receiveStream = CreatePlaybackStream(ServerBitDepth, ServerSampleRate, desiredMsec/*playback delay*/, ClientPacketMsec/*match client's mic sampling frequency / expected packet size?*/, DumbReceiveSamples);

                                    // HAXXX  this needs to be "jitter buffer"'d
                                    Audio.fmod.playSound(receiveStream, null, false, out receiveChannel);
                                }
                            }
                        }
                    }
                }
                


                if(!receivedAudioPacket)
                {
                    // if there's nothing left in the receive buffer, and its been a while since we receieved anything new, then kill stream
                    if (receiveStream != null)
                    {
                        int receiveBufferSize;
                        using (receiveBufferCrit.Lock)
                            receiveBufferSize = receiveBuffer.Count;

                        if (receiveBufferSize == 0 && DateTime.Now.Subtract(lastReceivedPacketTime).TotalMilliseconds > ClientBufferMsec)
                        {
                            LogMsg("Destroy receive stream");

                            receiveChannel.stop();
                            receiveChannel = null;

                            receiveStream.release();
                            receiveStream = null;

                            lastReceivedPacketTime = new DateTime();
                        }
                    }
                }
            }


            label2.Text = $"connected:{(server != null && server.Connected ? "yes":"no")}  recieveStream:{(receiveStream != null ? "valid" : "null")}   receiveBuffer:{receiveBuffer.Count}";




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

                uint bytesPerSample = (uint)(1/*channels*/ * (ServerBitDepth / 8)/*bitdepth*/);
                IntPtr ptr1, ptr2;
                uint len1, len2;
                recordBuffer.@lock(lastRecordPosition * bytesPerSample, (uint)blocklength * bytesPerSample, out ptr1, out ptr2, out len1, out len2);

                byte[] buf = new byte[len1 + len2];
                if (ptr1 != IntPtr.Zero && len1 > 0)
                    Marshal.Copy(ptr1, buf, 0, (int)len1);

                if (ptr2 != IntPtr.Zero && len2 > 0)
                    Marshal.Copy(ptr2, buf, (int)len1, (int)len2);

                recordBuffer.unlock(ptr1, ptr2, len1, len2);

                lastRecordPosition = recordPosition;



                label1.Text = $"record position: {recordPosition}    buf:{buf.Length}";


                // uhh whatever just send the audio packet (if valid)
                if (server != null && buf.Length > 0)
                {
                    ACAudioVCServer.Packet packet = new ACAudioVCServer.Packet(ACAudioVCServer.Packet.MessageType.RawAudio);

                    if (ServerULaw)
                        packet.WriteBuffer(WinSound.Utils.LinearToMulaw(buf, ServerBitDepth, 1));
                    else
                        packet.WriteBuffer(buf);

                    packet.Send(server);

                    //LogMsg("Sent packet");
                }


                
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
            }


            Audio.Process(0.05, 0.05, Vec3.Zero, Vec3.Zero, Vec3.PosZ, Vec3.PosY);
        }



        FMOD.Sound CreateRecordBuffer(int bitDepth, int rate)
        {
            int channels = 1;

            FMOD.Sound sound;
            FMOD.CREATESOUNDEXINFO cs = new FMOD.CREATESOUNDEXINFO();
            cs.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
            cs.length = (uint)(rate * (bitDepth/8) * channels * 2);//(uint)buf.Length;
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
            if (buf != null)
                Marshal.Copy(buf, 0, data, buf.Length);

            return FMOD.RESULT.OK;
        }

        public static FMOD.Sound CreatePlaybackStream(int bitDepth, int rate, int bufferMsec, int samplingMsec, GetStreamSamples callback)
        {
            gss = callback;

            int channels = 1;

            FMOD.Sound sound;
            FMOD.CREATESOUNDEXINFO cs = new FMOD.CREATESOUNDEXINFO();
            cs.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
            cs.length = (uint)(bufferMsec * rate * (bitDepth/8) / 1000);//(uint)buf.Length;//(uint)(rate * sizeof(short) * channels * 2);//(uint)buf.Length;
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
            cs.decodebuffersize = (uint)(samplingMsec * rate * (bitDepth/8) / 1000);//(uint)(cs.length / 4);//(uint)(10 * rate * 2 / 1000);//call pcm callback with small buffer size and frequently?   //cs.length;// (uint)buflen;
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

            if (loopbackChannel != null)
            {
                loopbackChannel.stop();
                loopbackChannel = null;
            }

            if (recordBuffer != null)
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

            recordBuffer = CreateRecordBuffer(ServerBitDepth, ServerSampleRate);
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
