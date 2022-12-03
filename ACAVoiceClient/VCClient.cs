using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ACACommon;
using Smith;

namespace ACAVoiceClient
{
    public static class VCClient
    {
        public delegate void LogDelegate(string s);
        public static LogDelegate LogCallback = null;

        public static void Log(string s)
        {
            if (LogCallback != null)
                LogCallback(s);
        }

        private static volatile TcpClient server = null;// ehh maybe needs more sync protections than "volatile" but we just prototyping for now
        private static string ServerIP = null;
        private static int ServerPort = 0;
        public const int DefaultServerPort = 42420;

        private static string AccountName = null;
        private static string CharacterName = null;
        private static int WeenieID = 0;

        private static bool _IsInitialized = false;
        public static bool IsInitialized
        {
            get
            {
                return _IsInitialized;
            }
        }

        public static void Init(string _AccountName, string _CharacterName, int _WeenieID, string _ServerIP, int _ServerPort=DefaultServerPort)
        {
            Shutdown();

            // we expect FMOD to have already been initialized by the caller
            if (Audio.fmod == null)
            {
                Log("Can't initialize voice client;  FMOD is not initialized");
                return;
            }

            AccountName = _AccountName;
            CharacterName = _CharacterName;
            WeenieID = _WeenieID;

            ServerIP = _ServerIP;
            ServerPort = _ServerPort;

            _IsInitialized = true;



            // connect to server straight away
            MaintainServer();
        }

        public static void Shutdown()
        {
            // issue disconnect
            if (server != null)
            {
                Packet packet = new Packet(Packet.MessageType.Disconnect);
                packet.WriteString("Client exit");
                SendToServer(packet);

                server.Close();
                server = null;
            }

            DestroyAllReceiveStreams();

            CloseRecordDevice();



            ServerIP = null;
            ServerPort = 0;

            AccountName = null;
            CharacterName = null;
            WeenieID = 0;


            SentServerHandshake = false;
            lastHeartbeat = new DateTime();

            _IsInitialized = false;
        }

        public static void Process(double dt)
        {
            if (!IsInitialized)
                return;

            // have we been lost connection?
            if (!WaitingForConnect && server != null && !server.Connected)
            {
                Log("Lost connection to server");

                server.Close();//probably not needed if disconnected but whatever
                server = null;
            }

            // connect to server straight away if possible, when we lost one
            MaintainServer();

            // anything to receieve?
            if (server != null)
            {
                // send periodic heartbeat
                if (DateTime.Now.Subtract(lastHeartbeat).TotalMilliseconds >= Packet.HeartbeatMsec)
                    SendToServer(new Packet(Packet.MessageType.Heartbeat));

                for (; ; )
                {
                    Packet packet = ReceiveFromServer(0);
                    if (packet == null)
                        break;

                    if (packet.Message == Packet.MessageType.Disconnect)
                    {
                        string reason = packet.ReadString();

                        // we're being ordered to disconnect. server will have already closed their socket. just close ours
                        Log("We have been disconnected from server: " + reason);
                        server.Close();
                        server = null;
                    }

                    if (packet.Message == Packet.MessageType.DetailAudio)
                    {
                        //LogMsg("Received packet");

                        ReceiveStreamID id = new ReceiveStreamID();
                        id.StreamInfoMagic = packet.ReadInt();
                        id.WeenieID = packet.ReadInt();



                        // if we have a previous stream with this audio source (weenie) but different magic then we should kill it immediately because we're about to start another one
                        using (ReceiveStreamsCrit.Lock)
                        {
                            List<ReceiveStreamID> destroyStreams = new List<ReceiveStreamID>();
                            foreach (ReceiveStreamID checkID in ReceiveStreams.Keys)
                                if (checkID.StreamInfoMagic != id.StreamInfoMagic &&
                                    checkID.WeenieID == id.WeenieID)
                                    destroyStreams.Add(checkID);

                            foreach (ReceiveStreamID destroyID in destroyStreams)
                                DestroyReceiveStream(destroyID);
                        }



                        ReceiveStream stream = GetOrCreateReceiveStream(id);

                        stream.SupplyPacket(packet);
                    }

                    if (packet.Message == Packet.MessageType.StreamInfo)
                    {
                        StreamInfo newStreamInfo = StreamInfo.FromPacket(packet);

                        // this should be only place that the current stream info is updated
                        using (_CurrentStreamInfoCrit.Lock)
                            _CurrentStreamInfo = newStreamInfo;

                        Log($"Received streaminfo: {newStreamInfo}");
                    }
                }
            }


            // process receive streams
            ReceiveStream[] streams;
            using (ReceiveStreamsCrit.Lock)
            {
                streams = new ReceiveStream[ReceiveStreams.Count];
                ReceiveStreams.Values.CopyTo(streams, 0);
            }
            foreach (ReceiveStream stream in streams)
            {
                bool wantDestroy;
                stream.Process(out wantDestroy);

                if (wantDestroy)
                    DestroyReceiveStream(stream.ID);
            }


            // if current record device isnt the up-to-date settings then lets force a re-open
            StreamInfo streamInfo = CurrentStreamInfo;//precache because of sync
            if (currentRecordStreamInfo != null && currentRecordStreamInfo.magic != streamInfo.magic)
                CloseRecordDevice();//nobody wants previous quality samples so flag for re-open, to force new settings


            if (PushToTalkEnable)
            {
                if (recordTimestamp == new DateTime())
                    OpenRecordDevice(streamInfo);
            }
            else
            {
                // not holding push-to-talk key..  wait a little extra delay before ending
                if (recordTimestamp != new DateTime())
                {
                    if (DateTime.Now.Subtract(recordTimestamp).TotalMilliseconds > 350)
                    {
                        CloseRecordDevice();
                    }
                }
            }


            if (CurrentRecordDevice != null && recordBuffer != null)
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

                uint bytesPerSample = (uint)(1/*channels*/ * (currentRecordStreamInfo.bitDepth / 8)/*bitdepth*/);
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



                // uhh whatever just send the audio packet (if valid)
                if (server != null && buf.Length > 0)
                {
                    Packet packet = new Packet(Packet.MessageType.RawAudio);

                    packet.WriteInt(currentRecordStreamInfo.magic);//embed id of known current format

                    if (currentRecordStreamInfo.ulaw)
                        packet.WriteBuffer(WinSound.Utils.LinearToMulaw(buf, currentRecordStreamInfo.bitDepth, 1));
                    else
                        packet.WriteBuffer(buf);

                    SendToServer(packet);

                    //LogMsg("Sent packet");
                }
            }

        }


        public static bool IsConnected
        {
            get
            {
                return (server != null && server.Connected);
            }
        }

        public static bool IsRecording
        {
            get
            {
                return (CurrentRecordDevice != null && recordBuffer != null);
            }
        }

        public static int NumActiveReceiveStreams
        {
            get
            {
                using (ReceiveStreamsCrit.Lock)
                    return ReceiveStreams.Count;
            }
        }


        public static volatile bool PushToTalkEnable = false;
        public static volatile RecordDeviceEntry CurrentRecordDevice = null;


        private static CritSect _CurrentStreamInfoCrit = new CritSect();
        private static StreamInfo _CurrentStreamInfo = null;
        public static StreamInfo CurrentStreamInfo
        {
            get
            {
                using (_CurrentStreamInfoCrit.Lock)
                    return _CurrentStreamInfo;
            }
        }

        // average length of how long our audio sample packets will consist of
        static int ClientPacketMsec
        {
            get
            {
                // we're trying to scrape mic samples and generate a packet every Timer tick so i guess use whatever that timer frequency is
                return 50;//timer1.Interval;
            }
        }

        // whatever size playback stream buffer we want.  this also decides the delay before playback.
        static int ClientBufferMsec
        {
            get
            {
                return ClientPacketMsec * 10;  // 50msec * 10 = 500msec
            }
        }

        class ReceiveStream : IDisposable
        {
            public readonly ReceiveStreamID ID;
            private IntPtr _ID_Unmanaged = IntPtr.Zero;
            public IntPtr ID_Unmanaged
            {
                get
                {
                    return _ID_Unmanaged;
                }
            }

            public readonly StreamInfo StreamInfo;

            private FMOD.Sound Stream = null;
            private FMOD.Channel Channel = null;

            private CritSect receiveBufferCrit = new CritSect();
            private List<byte> receiveBuffer = new List<byte>();

            private DateTime lastReceivedPacketTime = new DateTime();

            public ReceiveStream(ReceiveStreamID _ID, StreamInfo _StreamInfo)
            {
                ID = _ID;
                StreamInfo = _StreamInfo;

                _ID_Unmanaged = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ReceiveStreamID)));
                Marshal.StructureToPtr(ID, _ID_Unmanaged, false);
            }

            ~ReceiveStream()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);

                GC.SuppressFinalize(this);
            }

            volatile bool disposed = false;
            protected virtual void Dispose(bool disposing)
            {
                if (disposed)
                    return;

                disposed = true;

                if (Channel != null)
                {
                    Channel.stop();
                    Channel = null;
                }

                if (Stream != null)
                {
                    Stream.release();
                    Stream = null;
                }

                if (_ID_Unmanaged != null)
                {
                    Marshal.FreeHGlobal(_ID_Unmanaged);
                    _ID_Unmanaged = IntPtr.Zero;
                }
            }

            public void Process(out bool wantDestroy)
            {
                wantDestroy = false;

                int receiveBufferSize;
                using (receiveBufferCrit.Lock)
                    receiveBufferSize = receiveBuffer.Count;

                if (receiveBufferSize == 0 && DateTime.Now.Subtract(lastReceivedPacketTime).TotalMilliseconds > ClientBufferMsec)
                    wantDestroy = true;

                if (ID.StreamInfoMagic != CurrentStreamInfo.magic)
                    wantDestroy = true;
            }

            public bool SupplyPacket(Packet packet)
            {
                byte[] buf = packet.ReadBuffer();
                if (buf == null || buf.Length == 0)
                    return false;

                lastReceivedPacketTime = DateTime.Now;

                int receiveBufferSize;
                using (receiveBufferCrit.Lock)
                {
                    if (StreamInfo.ulaw)
                        receiveBuffer.AddRange(WinSound.Utils.MuLawToLinear(buf, StreamInfo.bitDepth, 1));
                    else
                        receiveBuffer.AddRange(buf);

                    receiveBufferSize = receiveBuffer.Count;
                }

                if (Stream == null)
                {
                    //int desiredMsec = ClientBufferMsec;  // wait until we have this much audio before initializing stream
                    //int desiredBytes = ServerSampleRate * (ServerBitDepth / 8) * desiredMsec / 1000;

                    //if (receiveBufferSize >= ClientBufferMsec)
                    {
                        // pull out the desired bytes from receive buffer and provide to stream immediately
                        /* byte[] buf = new byte[desiredBytes];
                            receiveBuffer.CopyTo(0, buf, 0, desiredBytes);
                            receiveBuffer.RemoveRange(0, desiredBytes);*/

                        //LogMsg("Create/play receive stream");

                        Stream = CreatePlaybackStream(StreamInfo, ClientBufferMsec/*playback delay*/, ClientPacketMsec/*match client's mic sampling frequency / expected packet size?*/, ID_Unmanaged);

                        // HAXXX  this needs to be "jitter buffer"'d
                        Audio.fmod.playSound(Stream, null, false, out Channel);
                    }
                }

                return true;
            }

            public byte[] RetrieveSamples(int len)
            {
#if true
                //LogMsg($"retrievesamples for stream weenie {WeenieID}");

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

                Log($"{ID}: SAMPLE CALLBACK{(bytesToCopy < len ? $" (STARVED {100 - (bytesToCopy * 100 / len)}%)" : string.Empty)}");

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
        }

        public struct ReceiveStreamID
        {
            public int StreamInfoMagic;
            public int WeenieID;

            public override string ToString()
            {
                return $"({StreamInfoMagic.ToString("X8")})({WeenieID.ToString("X8")})";
            }

            public static ReceiveStreamID FromUnmanaged(IntPtr ptr)
            {
                return (ReceiveStreamID)Marshal.PtrToStructure(ptr, typeof(ReceiveStreamID));
            }
        }

        static CritSect ReceiveStreamsCrit = new CritSect();
        static Dictionary<ReceiveStreamID, ReceiveStream> ReceiveStreams = new Dictionary<ReceiveStreamID, ReceiveStream>();

        static ReceiveStream GetReceiveStream(ReceiveStreamID id)
        {
            using (ReceiveStreamsCrit.Lock)
            {
                ReceiveStream stream;
                if (!ReceiveStreams.TryGetValue(id, out stream))
                    stream = null;

                return stream;
            }
        }

        static ReceiveStream GetOrCreateReceiveStream(ReceiveStreamID id)
        {
            using (ReceiveStreamsCrit.Lock)
            {
                ReceiveStream stream;
                if (!ReceiveStreams.TryGetValue(id, out stream))
                {
                    Log($"Create receive stream {id}");

                    stream = new ReceiveStream(id, CurrentStreamInfo);
                    ReceiveStreams.Add(id, stream);
                }

                return stream;
            }
        }

        static void DestroyReceiveStream(ReceiveStreamID id)
        {
            ReceiveStream stream;
            using (ReceiveStreamsCrit.Lock)
            {
                if (!ReceiveStreams.TryGetValue(id, out stream))
                    return;

                ReceiveStreams.Remove(id);
            }

            Log($"Destroy receive stream {id}");

            stream.Dispose();
        }

        static void DestroyAllReceiveStreams()
        {
            ReceiveStream[] streams;
            using (ReceiveStreamsCrit.Lock)
            {
                streams = new ReceiveStream[ReceiveStreams.Values.Count];
                ReceiveStreams.Values.CopyTo(streams, 0);

                ReceiveStreams.Clear();
            }

            foreach (ReceiveStream stream in streams)
                stream.Dispose();
        }

        private static volatile bool WaitingForConnect = false;
        private static void ConnectCallback(IAsyncResult ar)
        {
            if (!ar.IsCompleted)
                return;

            TcpClient newServer = ar.AsyncState as TcpClient;
            try
            {
                newServer.EndConnect(ar);

                // looks OK
                server = newServer;
            }
            catch
            {

            }

            // regardless.. this attempt is done
            WaitingForConnect = false;
        }

        private static bool SentServerHandshake = false;
        private static DateTime lastServerConnectAttempt = new DateTime();
        static void MaintainServer()
        {
            if (WaitingForConnect)
            {
                lastServerConnectAttempt = DateTime.Now;//refresh timer while our attempt is active  (ensures our next attempt will only start from desired value w/o tcp timeout)
                return;
            }

            // is server already good?
            if (server != null && server.Connected)
            {
                if (!SentServerHandshake)
                {
                    // if its our first time here, then we should introduce ourselves
                    Packet clientInfo = new Packet(Packet.MessageType.PlayerConnect);

                    clientInfo.WriteString(AccountName);
                    clientInfo.WriteString(CharacterName);
                    clientInfo.WriteInt(WeenieID);

                    SendToServer(clientInfo);

                    SentServerHandshake = true;
                }

                return;
            }

            // dont try reconnect that often
            if (DateTime.Now.Subtract(lastServerConnectAttempt).TotalMilliseconds < Packet.HeartbeatMsec/*can replace with another value if desired*/)
                return;

            // we are trying now
            lastServerConnectAttempt = DateTime.Now;
            SentServerHandshake = false;

            try
            {
                TcpClient tryServer = new TcpClient();

                Log($"Attempting connection to {ServerIP}:{ServerPort}");

                WaitingForConnect = true;
                tryServer.BeginConnect(ServerIP, ServerPort, ConnectCallback, tryServer);
            }
            catch
            {
                server = null;
            }

        }


        static DateTime recordTimestamp = new DateTime();

        static uint lastRecordPosition = 0;

        static DateTime lastHeartbeat = new DateTime();
        private  static void SendToServer(Packet p)
        {
            if (server == null)
                return;

            lastHeartbeat = DateTime.Now;
            p.InternalSend(server);
        }

        private static Packet ReceiveFromServer(int headerTimeoutMsec = Packet.DefaultTimeoutMsec, int dataTimeoutMsec = Packet.DefaultTimeoutMsec)
        {
            if (server == null)
                return null;

            return Packet.InternalReceive(server, headerTimeoutMsec, dataTimeoutMsec);
        }


        public class RecordDeviceEntry
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

        public static RecordDeviceEntry[] QueryRecordDevices()
        {
            if (Audio.fmod == null)
                return null;

            int numDrivers, numConnected;
            Audio.fmod.getRecordNumDrivers(out numDrivers, out numConnected);

            List<RecordDeviceEntry> entries = new List<RecordDeviceEntry>();
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

                entries.Add(rde);
            }

            return entries.ToArray();
        }



        static void CloseRecordDevice()
        {
            if (currentRecordStreamInfo != null)//just dumb filter for log message/ can remove lol
                Log("MICROPHONE: END");

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
            recordTimestamp = new DateTime();
            currentRecordStreamInfo = null;
        }

        public static bool Loopback = false;
        static FMOD.Channel loopbackChannel = null;


        static FMOD.Sound recordBuffer = null;

        static StreamInfo currentRecordStreamInfo = null;
        static void OpenRecordDevice(StreamInfo streamInfo)
        {
            // if properties are the same then its fine
            //if (ACAudioVCServer.Server.StreamInfo.CompareProperties(currentRecordStreamInfo, streamInfo))
            //return;

            CloseRecordDevice();

            if (CurrentRecordDevice == null || streamInfo == null)
                return;

            currentRecordStreamInfo = streamInfo;

            recordBuffer = CreateRecordBuffer(currentRecordStreamInfo);
            Audio.fmod.recordStart(CurrentRecordDevice.ID, recordBuffer, true);


            if (Loopback)
            {
                System.Threading.Thread.Sleep(50);
                Audio.fmod.playSound(recordBuffer, null, false, out loopbackChannel);
            }

            recordTimestamp = DateTime.Now;


            Log("MICROPHONE: START");
        }

        static FMOD.Sound CreateRecordBuffer(StreamInfo streamInfo)
        {
            int channels = 1;

            FMOD.Sound sound;
            FMOD.CREATESOUNDEXINFO cs = new FMOD.CREATESOUNDEXINFO();
            cs.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
            cs.length = (uint)(streamInfo.sampleRate * (streamInfo.bitDepth / 8) * channels * 2);//(uint)buf.Length;
            cs.fileoffset = 0;
            cs.numchannels = channels;
            cs.defaultfrequency = streamInfo.sampleRate;
            switch (streamInfo.bitDepth)
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
                Log("Failed to create sound: " + result.ToString());
                return null;
            }

            return sound;
        }

        private static FMOD.SOUND_PCMREADCALLBACK PCMReadCallbackDelegate = new FMOD.SOUND_PCMREADCALLBACK(PCMReadCallback);
        private static FMOD.RESULT PCMReadCallback(IntPtr soundraw, IntPtr data, uint datalen)
        {
            FMOD.Sound sound = new FMOD.Sound(soundraw);

            IntPtr userdata;
            sound.getUserData(out userdata);
            ReceiveStreamID id = ReceiveStreamID.FromUnmanaged(userdata);

            ReceiveStream stream = GetReceiveStream(id);

            byte[] buf;
            if (stream == null)
            {
                buf = new byte[datalen];
                for (int x = 0; x < datalen; x++)
                    buf[x] = 0;
            }
            else
                buf = stream.RetrieveSamples((int)datalen);

            if (buf != null)
                Marshal.Copy(buf, 0, data, buf.Length);

            return FMOD.RESULT.OK;
        }

        private static FMOD.Sound CreatePlaybackStream(StreamInfo streamInfo, int bufferMsec, int samplingMsec, IntPtr userdata)
        {
            int channels = 1;

            FMOD.Sound sound;
            FMOD.CREATESOUNDEXINFO cs = new FMOD.CREATESOUNDEXINFO();
            cs.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
            cs.length = (uint)(bufferMsec * streamInfo.sampleRate * (streamInfo.bitDepth / 8) / 1000);//(uint)buf.Length;//(uint)(rate * sizeof(short) * channels * 2);//(uint)buf.Length;
            cs.fileoffset = 0;
            cs.numchannels = channels;
            cs.defaultfrequency = streamInfo.sampleRate;
            switch (streamInfo.bitDepth)
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
            cs.decodebuffersize = (uint)(samplingMsec * streamInfo.sampleRate * (streamInfo.bitDepth / 8) / 1000);//(uint)(cs.length / 4);//(uint)(10 * rate * 2 / 1000);//call pcm callback with small buffer size and frequently?   //cs.length;// (uint)buflen;
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
            cs.userdata = userdata;
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

            FMOD.RESULT result = Audio.fmod.createStream((byte[])null, FMOD.MODE.CREATESTREAM | FMOD.MODE._2D | FMOD.MODE.OPENUSER | FMOD.MODE.OPENONLY | FMOD.MODE.LOOP_NORMAL, ref cs, out sound);
            if (result != FMOD.RESULT.OK || sound == null)
            {
                Log("Failed to create sound: " + result.ToString());
                return null;
            }

            return sound;
        }
    }
}
