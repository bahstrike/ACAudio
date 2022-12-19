using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ACACommon;
using Smith;

namespace ACAudio
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
        public static string ServerIP = null;
        //public static int ServerPort = DefaultServerPort;
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

        public delegate Vec3? GetWeeniePositionDelegate(int weenieID);
        public static GetWeeniePositionDelegate GetWeeniePosition = null;

        public delegate void SpeakingIconDelegate(int weenieID);
        public static SpeakingIconDelegate CreateSpeakingIcon = null;
        public static SpeakingIconDelegate DestroySpeakingIcon = null;

        public delegate bool CheckPlayerDelegate(int weenieID);
        public static CheckPlayerDelegate CheckForMute = null;

        private static volatile int CurrentInitNumber = 0;

        public static void Init(string _AccountName, string _CharacterName, int _WeenieID)
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

            _IsInitialized = true;
        }

        public static void Shutdown()
        {
            CurrentInitNumber++;// invalidate previous connect number in case a connect attempt callback completes during our downtime

            CurrentVoice = StreamInfo.VoiceChannel.Invalid;// we arent talking anymore


            // wait until any possible pending connect attempt has completed
            //while (WaitingForConnect)
            //System.Threading.Thread.Sleep(10);

            // issue disconnect
            if (server != null)
            {
                Packet packet = new Packet(Packet.MessageType.Disconnect);
                packet.WriteString("Client exit");
                SendToServer(packet);

                ServerClose();
            }

            DestroyAllReceiveStreams();

            CloseRecordDevice();


            ServerIP = null;
            //ServerPort = 0;

            AccountName = null;
            CharacterName = null;
            WeenieID = 0;


            SentServerHandshake = false;
            lastHeartbeat = new DateTime();
            lastSentPlayerStatusTime = new DateTime();

            _IsInitialized = false;
        }

        private static DateTime lastSentPlayerStatusTime = new DateTime();
        private static Position lastSentPlayerPosition = Position.Invalid;
        private static int lastSentPlayerAllegianceID = StreamInfo.InvalidAllegianceID;
        private static int lastSentPlayerFellowshipID = StreamInfo.InvalidFellowshipID;

        // when closing socket, and ensuring gameplay flags are set for next connection to send relevant data
        private static void ServerClose()
        {
            if (server != null)
            {
                server.Close();//probably not needed if disconnected but whatever
                server = null;
            }

            stagedInfo = null;// dont retain any possible staged packet info from previous connection!!

            lastSentPlayerStatusTime = new DateTime();
            lastSentPlayerPosition = Position.Invalid;
            lastSentPlayerAllegianceID = StreamInfo.InvalidAllegianceID;
            lastSentPlayerFellowshipID = StreamInfo.InvalidFellowshipID;
        }

        private static bool _AreThereNearbyPlayers = false;// flag to prevent sending RawAudio packets if server says nobody is around to hear them
        public static bool AreThereNearbyPlayers
        {
            get
            {
                return _AreThereNearbyPlayers;
            }
        }

        private static int _TotalConnectedPlayers = 0;
        public static int TotalConnectedPlayers
        {
            get
            {
                return _TotalConnectedPlayers;
            }
        }

        public static void Process(double dt)
        {
            if (!IsInitialized)
                return;

            // have we been lost connection?
            if (!WaitingForConnect && server != null && !server.Connected)
            {
                Log("Lost connection to server");

                ServerClose();
            }

            // connect to server straight away if possible, when we lost one
            MaintainServer();

            // anything to send/receieve?
            if (server != null)
            {
                // check if we need to send new player status
                if (
                    // send immediately if our basic info has sufficiently changed
                    !PlayerPosition.IsCompatibleWith(lastSentPlayerPosition) ||
                    PlayerAllegianceID != lastSentPlayerAllegianceID ||
                    PlayerFellowshipID != lastSentPlayerFellowshipID ||

                    // otherwise send periodically if our position has changed
                    (!PlayerPosition.Equals(lastSentPlayerPosition) && DateTime.Now.Subtract(lastSentPlayerStatusTime).TotalMilliseconds > 250)  // send periodically if we're moving
                    )
                {

                    Packet p = new Packet(Packet.MessageType.ClientStatus);

                    p.WriteInt(PlayerAllegianceID);
                    p.WriteInt(PlayerFellowshipID);
                    PlayerPosition.ToStream(p, true);

                    SendToServer(p);


                    lastSentPlayerStatusTime = DateTime.Now;
                    lastSentPlayerPosition = PlayerPosition;//struct, its ok: deepcopy
                    lastSentPlayerAllegianceID = PlayerAllegianceID;
                    lastSentPlayerFellowshipID = PlayerFellowshipID;
                }

                // try to receive stuff
                for (; ; )
                {
                    Packet packet = ReceiveFromServer();
                    if (packet == null)
                        break;

                    if (packet.Message == Packet.MessageType.Disconnect)
                    {
                        string reason = packet.ReadString();

                        // we're being ordered to disconnect. server will have already closed their socket. just close ours
                        Log("We have been disconnected from server: " + reason);
                        ServerClose();
                        break;
                    }

                    if(packet.Message == Packet.MessageType.ServerStatus)
                    {
                        _TotalConnectedPlayers = packet.ReadInt();
                        _AreThereNearbyPlayers = packet.ReadBool();
                    }

                    if (packet.Message == Packet.MessageType.DetailAudio)
                    {
                        //LogMsg("Received packet");

                        ReceiveStreamID id = new ReceiveStreamID();
                        id.StreamInfoMagic = packet.ReadInt();
                        id.Channel = (StreamInfo.VoiceChannel)packet.ReadInt();
                        id.WeenieID = packet.ReadInt();



                        // if we have a previous stream with this audio source (weenie) but different magic then we should kill it immediately because we're about to start another one
                        using (ReceiveStreamsCrit.Lock)
                        {
                            List<ReceiveStreamID> destroyStreams = new List<ReceiveStreamID>();
                            foreach (ReceiveStreamID checkID in ReceiveStreams.Keys)
                                if (checkID.WeenieID == id.WeenieID && 
                                    (checkID.StreamInfoMagic != id.StreamInfoMagic ||
                                     checkID.Channel != id.Channel)
                                    )
                                    destroyStreams.Add(checkID);

                            foreach (ReceiveStreamID destroyID in destroyStreams)
                                DestroyReceiveStream(destroyID);
                        }


                        // check for mute
                        if (CheckForMute == null || !CheckForMute(id.WeenieID))
                        {
                            ReceiveStream stream = GetOrCreateReceiveStream(id);
                            if (stream != null)
                            {
                                stream.SupplyPacket(packet);
                            }
                        }
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


                if (server != null)
                {
                    // send periodic heartbeat
                    if (DateTime.Now.Subtract(lastHeartbeat).TotalMilliseconds >= Packet.HeartbeatMsec)
                        SendToServer(new Packet(Packet.MessageType.Heartbeat));
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
            if ((currentRecordStreamInfo != null && currentRecordStreamInfo.magic != streamInfo.magic) || (CurrentRecordDevice == null || CurrentRecordDevice.ID != recordBufferDeviceID))
                CloseRecordDevice();//nobody wants previous quality samples so flag for re-open, to force new settings


            if (CurrentVoice != StreamInfo.VoiceChannel.Invalid)
            {
                if (recordTimestamp == new DateTime())
                    OpenRecordDevice(streamInfo);
            }
            else
            {
                // not holding push-to-talk key..  wait a little extra delay before ending
                if (recordTimestamp != new DateTime())
                {
                    // have delay to avoid average person tendency to misjudge releasing push-to-talk VS their final speech.
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



                // only track and handle samples if we are connected anywhere
                if (server != null)
                {
                    // if we got new stuff, add it
                    if (buf.Length > 0)
                        pendingRecordSamples.AddRange(buf);


                    // do we have samples to send, and is it time to send an audio chunk?
                    if (DateTime.Now.Subtract(lastSentRecordSamplesTime).TotalMilliseconds >= StreamInfo.DesiredAudioChunkMsec && pendingRecordSamples.Count > 0)
                    {
                        lastSentRecordSamplesTime = DateTime.Now;//ok we're doing something with these samples now
                        byte[] outBuf = pendingRecordSamples.ToArray();
                        pendingRecordSamples.Clear();

                        if (
                            Loopback ||
                            (CurrentVoice == StreamInfo.VoiceChannel.Proximity3D && AreThereNearbyPlayers) || // dont send a 3d audio packet unless there are nearby players.  but allow loopback or non-3d
                            (CurrentVoice == StreamInfo.VoiceChannel.Fellowship && PlayerFellowshipID != StreamInfo.InvalidFellowshipID) || //just make sure not invalid?
                            (CurrentVoice == StreamInfo.VoiceChannel.Allegiance && PlayerAllegianceID != StreamInfo.InvalidAllegianceID)
                            )
                        {
                            Packet packet = new Packet(Packet.MessageType.RawAudio);

                            packet.WriteInt(currentRecordStreamInfo.magic);//embed id of known current format
                            packet.WriteBool(Loopback);
                            packet.WriteInt((int)CurrentVoice);

                            if (currentRecordStreamInfo.ulaw)
                                packet.WriteBuffer(WinSound.Utils.LinearToMulaw(outBuf, currentRecordStreamInfo.bitDepth, 1));
                            else
                                packet.WriteBuffer(outBuf);

                            SendToServer(packet);

                            //Log("Sent RawAudio packet");
                        }
                        else
                        {
                            //Log("Didn't send RawAudio; nobody to hear");
                        }
                    }
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


        public static RecordDeviceEntry CurrentRecordDevice = null;
        public static bool Loopback = false;
        public static StreamInfo.VoiceChannel CurrentVoice = StreamInfo.VoiceChannel.Invalid;
        public static Position PlayerPosition = Position.Invalid;
        public static int PlayerAllegianceID = StreamInfo.InvalidAllegianceID;
        public static int PlayerFellowshipID = StreamInfo.InvalidFellowshipID;


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
            private Audio.Channel Channel = null;

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

                if (VCClient.DestroySpeakingIcon != null)
                    VCClient.DestroySpeakingIcon(ID.WeenieID);

                if (Channel != null)
                {
                    Channel.Stop();
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

                // if 3d positional, keep updating position
                if (Channel != null && ID.Channel == StreamInfo.VoiceChannel.Proximity3D)
                {
                    Vec3? pos = null;

                    if (VCClient.GetWeeniePosition != null)
                        pos = VCClient.GetWeeniePosition(ID.WeenieID);

                    if(pos.HasValue)
                        Channel.SetPosition(pos.Value, Vec3.Zero);
                }

                // if this is the first (mainthread) situation we have provided samples to playback stream then this is our mark to create in-game speaking icon
                if(SpeakingIconState == SpeakingIconStateType.GotSamples)
                {
                    if (VCClient.CreateSpeakingIcon != null)
                        VCClient.CreateSpeakingIcon(ID.WeenieID);

                    SpeakingIconState = SpeakingIconStateType.Done;
                }
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

                        Stream = CreatePlaybackStream(StreamInfo, ID.Channel == StreamInfo.VoiceChannel.Proximity3D, ClientBufferMsec/*playback delay*/, ClientPacketMsec/*match client's mic sampling frequency / expected packet size?*/, ID_Unmanaged);

                        Stream.set3DMinMaxDistance((float)StreamInfo.PlayerMinDist, (float)StreamInfo.PlayerMaxDist);//5.0f, 35.0f);

                        // playing with smith audio so the master volume can work (when alt-tabbed out).. could be risky given the "smartness" acaudio does with channels. need to incorporate properly.
                        // technically we should pause to ensure position (and maybe other) properties are set first.. but since stream will be silence until PCM sample callback, then Process should have enough time to sync info
                        Channel = Audio.PlaySound(Stream, ID.Channel == StreamInfo.VoiceChannel.Proximity3D ? Audio.DimensionMode._3DPositional : Audio.DimensionMode._2D, true);

                        // maybe wait until pcm callback flags as being called for first time
                        //if (VCClient.CreateSpeakingIcon != null)
                            //VCClient.CreateSpeakingIcon(ID.WeenieID);
                    }
                }

                return true;
            }

            private volatile SpeakingIconStateType SpeakingIconState = SpeakingIconStateType.WaitingForSamples;
            private enum SpeakingIconStateType
            {
                WaitingForSamples,
                GotSamples,
                Done
            }

            public byte[] RetrieveSamples(int len)
            {
                if (SpeakingIconState == SpeakingIconStateType.WaitingForSamples)
                    SpeakingIconState = SpeakingIconStateType.GotSamples;

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

                //Log($"{ID}: SAMPLE CALLBACK{(bytesToCopy < len ? $" (STARVED {100 - (bytesToCopy * 100 / len)}%)" : string.Empty)}");

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
            public StreamInfo.VoiceChannel Channel;
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
            // dont make a stream if we havent gotten the current settings yet
            StreamInfo streamInfo = CurrentStreamInfo;
            if (streamInfo == null || streamInfo.magic != id.StreamInfoMagic)
                return null;

            using (ReceiveStreamsCrit.Lock)
            {
                ReceiveStream stream;
                if (!ReceiveStreams.TryGetValue(id, out stream))
                {
                    //Log($"Create receive stream {id}");

                    stream = new ReceiveStream(id, streamInfo);
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

            //Log($"Destroy receive stream {id}");

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

        private static volatile bool _WaitingForConnect = false;
        public static bool WaitingForConnect
        {
            get
            {
                return _WaitingForConnect;
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            if (!ar.IsCompleted)
                return;

            ConnectAttempt ca = ar.AsyncState as ConnectAttempt;
            try
            {
                ca.Server.EndConnect(ar);

                // looks OK
                if (ca.InitNumber == CurrentInitNumber)
                    server = ca.Server;
                }
            catch
            {

            }

            // regardless.. this attempt is done
            if (ca.InitNumber == CurrentInitNumber)
                _WaitingForConnect = false;
        }

        private static string currentServerHost = null;
        //private static int currentServerPort = 0;

        private static bool SentServerHandshake = false;
        private static DateTime lastServerConnectAttempt = new DateTime();
        static void MaintainServer()
        {
            // if we dont have player info, bail now
            if (string.IsNullOrEmpty(AccountName) || string.IsNullOrEmpty(CharacterName) /*|| WeenieID == 0*/)
                return;


            // if desired target server has changed, kill previous connection
            if(currentServerHost != ServerIP /*|| currentServerPort != ServerPort*/)
            {
                // if we were previously connected, kill it
                if(server != null)
                {
                    if (server.Connected)
                    {
                        Packet p = new Packet(Packet.MessageType.Disconnect);
                        p.WriteString("Changing servers");
                        SendToServer(p);
                    }

                    ServerClose();
                }

                currentServerHost = ServerIP;
                //currentServerPort = ServerPort;


                // try new connection immediately
                lastServerConnectAttempt = new DateTime();
                _WaitingForConnect = false;
            }



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
            if (DateTime.Now.Subtract(lastServerConnectAttempt).TotalMilliseconds < 30000)
                return;

            // we are trying now
            lastServerConnectAttempt = DateTime.Now;
            SentServerHandshake = false;

            try
            {
                if (!string.IsNullOrEmpty(ServerIP))
                {
                    TcpClient tryServer = new TcpClient();

                    Log($"Attempting connection to {ServerIP}");//:{ServerPort}");

                    ConnectAttempt ca = new ConnectAttempt();
                    ca.InitNumber = CurrentInitNumber;
                    ca.Server = tryServer;

                    if (tryServer.BeginConnect(ServerIP, DefaultServerPort/*ServerPort*/, ConnectCallback, ca) != null)
                        _WaitingForConnect = true;
                }
            }
            catch
            {
                server = null;
                stagedInfo = null;
            }

        }


        private class ConnectAttempt
        {
            public int InitNumber;
            public TcpClient Server;
        }


        static DateTime recordTimestamp = new DateTime();

        static uint lastRecordPosition = 0;

        static List<byte> pendingRecordSamples = new List<byte>();
        static DateTime lastSentRecordSamplesTime = new DateTime();

        static DateTime lastHeartbeat = new DateTime();
        private  static void SendToServer(Packet p)
        {
            if (server == null)
                return;

            lastHeartbeat = DateTime.Now;
            p.InternalSend(server);
        }

        private static Packet.StagedInfo stagedInfo = null;
        private static Packet ReceiveFromServer()
        {
            if (server == null)
                return null;

            return Packet.InternalReceive(server, ref stagedInfo);
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
            //if (currentRecordStreamInfo != null)//just dumb filter for log message/ can remove lol
                //Log("MICROPHONE: END");

            if (CurrentRecordDevice != null && recordBuffer != null)
                // this seems to sometimes take ~150msec on side PC with wireless headset mic.  could result in frameskips if left on main thread
                Audio.fmod.recordStop(CurrentRecordDevice.ID);

            /*if (loopbackChannel != null)
            {
                loopbackChannel.Stop();
                loopbackChannel = null;
            }*/

            if (recordBuffer != null)
            {
                recordBuffer.release();
                recordBuffer = null;
            }

            lastRecordPosition = 0;
            recordTimestamp = new DateTime();
            currentRecordStreamInfo = null;
            recordBufferDeviceID = -1;
        }

        //public static bool Loopback = false;
        //static Audio.Channel loopbackChannel = null;


        static int recordBufferDeviceID = -1;
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

            recordBufferDeviceID = CurrentRecordDevice.ID;
            recordBuffer = CreateRecordBuffer(currentRecordStreamInfo);
            Audio.fmod.recordStart(CurrentRecordDevice.ID, recordBuffer, true);


            /*if (Loopback)
            {
                System.Threading.Thread.Sleep(50);
                loopbackChannel = Audio.PlaySound(recordBuffer, Audio.DimensionMode._2D, true);//Audio.fmod.playSound(recordBuffer, null, false, out loopbackChannel);
            }*/

            recordTimestamp = DateTime.Now;


            //Log("MICROPHONE: START");
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

            return sound;//return Audio.RegisterSound(sound, "MICROPHONE", Audio.DimensionMode._2D, true);
        }

        private static FMOD.SOUND_PCMREADCALLBACK PCMReadCallbackDelegate = new FMOD.SOUND_PCMREADCALLBACK(PCMReadCallback);
        private static FMOD.RESULT PCMReadCallback(IntPtr soundraw, IntPtr data, uint datalen)
        {
            try
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
            }
            catch(Exception ex)
            {
                Log($"ITS ME, PCMREADCALLBACK.  IM THE BITCH: {ex.Message}");
            }

            return FMOD.RESULT.OK;
        }

        private static FMOD.Sound CreatePlaybackStream(StreamInfo streamInfo, bool is3d, int bufferMsec, int samplingMsec, IntPtr userdata)
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

            FMOD.RESULT result = Audio.fmod.createStream((byte[])null, FMOD.MODE.CREATESTREAM | (is3d ? FMOD.MODE._3D : FMOD.MODE._2D) | FMOD.MODE.OPENUSER | FMOD.MODE.OPENONLY | FMOD.MODE.LOOP_NORMAL, ref cs, out sound);
            if (result != FMOD.RESULT.OK || sound == null)
            {
                Log("Failed to create sound: " + result.ToString());
                return null;
            }

            return sound;
        }
    }
}
