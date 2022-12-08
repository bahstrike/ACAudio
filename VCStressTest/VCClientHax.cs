using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ACACommon;
using Smith;

namespace VCStressTest
{
    public class VCClient
    {
        public delegate void LogDelegate(string s);
        public static LogDelegate LogCallback = null;

        public static void Log(string s)
        {
            if (LogCallback != null)
                LogCallback(s);
        }

        private volatile TcpClient server = null;// ehh maybe needs more sync protections than "volatile" but we just prototyping for now
        public string ServerIP = null;
        //public static int ServerPort = DefaultServerPort;
        public const int DefaultServerPort = 42420;

        private string AccountName = null;
        private string CharacterName = null;
        private int WeenieID = 0;

        private bool _IsInitialized = false;
        public bool IsInitialized
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

        private volatile int CurrentInitNumber = 0;

        public void Init(string _AccountName, string _CharacterName, int _WeenieID)
        {
            Shutdown();

            AccountName = _AccountName;
            CharacterName = _CharacterName;
            WeenieID = _WeenieID;

            _IsInitialized = true;
        }

        public void Shutdown()
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

        private DateTime lastSentPlayerStatusTime = new DateTime();
        private Position lastSentPlayerPosition = Position.Invalid;
        private int lastSentPlayerAllegianceID = StreamInfo.InvalidAllegianceID;
        private int lastSentPlayerFellowshipID = StreamInfo.InvalidFellowshipID;

        // when closing socket, and ensuring gameplay flags are set for next connection to send relevant data
        private void ServerClose()
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

        private bool _AreThereNearbyPlayers = false;// flag to prevent sending RawAudio packets if server says nobody is around to hear them
        public bool AreThereNearbyPlayers
        {
            get
            {
                return _AreThereNearbyPlayers;
            }
        }

        private int _TotalConnectedPlayers = 0;
        public int TotalConnectedPlayers
        {
            get
            {
                return _TotalConnectedPlayers;
            }
        }

        public void Process(double dt)
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

                    if (packet.Message == Packet.MessageType.ServerStatus)
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



                        ReceiveStream stream = GetOrCreateReceiveStream(id);
                        if (stream != null)
                        {
                            stream.SupplyPacket(packet);
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


            // maybe sometimes randomly switch voice channel?


            if (streamInfo != null && CurrentVoice != StreamInfo.VoiceChannel.Invalid)
            {
                

                // only track and handle samples if we are connected anywhere
                if (server != null)
                {
                    // do we have samples to send, and is it time to send an audio chunk?
                    if (DateTime.Now.Subtract(lastSentRecordSamplesTime).TotalMilliseconds >= StreamInfo.DesiredAudioChunkMsec)
                    {
                        lastSentRecordSamplesTime = DateTime.Now;//ok we're doing something with these samples now
                        byte[] outBuf = new byte[StreamInfo.DesiredAudioChunkMsec * streamInfo.bitDepth / 8 * streamInfo.sampleRate / 1000];

                        if (
                            Loopback ||
                            (CurrentVoice == StreamInfo.VoiceChannel.Proximity3D && AreThereNearbyPlayers) || // dont send a 3d audio packet unless there are nearby players.  but allow loopback or non-3d
                            (CurrentVoice == StreamInfo.VoiceChannel.Fellowship && PlayerFellowshipID != StreamInfo.InvalidFellowshipID) || //just make sure not invalid?
                            (CurrentVoice == StreamInfo.VoiceChannel.Allegiance && PlayerAllegianceID != StreamInfo.InvalidAllegianceID)
                            )
                        {
                            Packet packet = new Packet(Packet.MessageType.RawAudio);

                            packet.WriteInt(streamInfo.magic);//embed id of known current format
                            packet.WriteBool(Loopback);
                            packet.WriteInt((int)CurrentVoice);

                            if (streamInfo.ulaw)
                                packet.WriteBuffer(WinSound.Utils.LinearToMulaw(outBuf, streamInfo.bitDepth, 1));
                            else
                                packet.WriteBuffer(outBuf);

                            SendToServer(packet);

                            Log("Sent RawAudio packet");
                        }
                        else
                        {
                            Log("Didn't send RawAudio; nobody to hear");
                        }
                    }
                }
            }

        }

        public bool IsConnected
        {
            get
            {
                return (server != null && server.Connected);
            }
        }

        public int NumActiveReceiveStreams
        {
            get
            {
                using (ReceiveStreamsCrit.Lock)
                    return ReceiveStreams.Count;
            }
        }


        public bool Loopback = false;
        public StreamInfo.VoiceChannel CurrentVoice = StreamInfo.VoiceChannel.Invalid;
        public Position PlayerPosition = Position.Invalid;
        public int PlayerAllegianceID = StreamInfo.InvalidAllegianceID;
        public int PlayerFellowshipID = StreamInfo.InvalidFellowshipID;


        private CritSect _CurrentStreamInfoCrit = new CritSect();
        private StreamInfo _CurrentStreamInfo = null;
        public StreamInfo CurrentStreamInfo
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
            public readonly VCClient VCClient;

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

            private CritSect receiveBufferCrit = new CritSect();
            private List<byte> receiveBuffer = new List<byte>();

            private DateTime lastReceivedPacketTime = new DateTime();

            public ReceiveStream(VCClient _VCClient, ReceiveStreamID _ID, StreamInfo _StreamInfo)
            {
                VCClient = _VCClient;
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

                if (_ID_Unmanaged != null)
                {
                    Marshal.FreeHGlobal(_ID_Unmanaged);
                    _ID_Unmanaged = IntPtr.Zero;
                }
            }

            public void Process(out bool wantDestroy)
            {
                wantDestroy = false;


                using (receiveBufferCrit.Lock)
                {
                    int bytesToEat = Math.Min(receiveBuffer.Count, MathLib.random.Next(1000));
                    if (bytesToEat > 0)
                        receiveBuffer.RemoveRange(0, bytesToEat);
                }



                int receiveBufferSize;
                using (receiveBufferCrit.Lock)
                    receiveBufferSize = receiveBuffer.Count;

                if (receiveBufferSize == 0 && DateTime.Now.Subtract(lastReceivedPacketTime).TotalMilliseconds > ClientBufferMsec)
                    wantDestroy = true;

                if (ID.StreamInfoMagic != VCClient.CurrentStreamInfo.magic)
                    wantDestroy = true;

                // if this is the first (mainthread) situation we have provided samples to playback stream then this is our mark to create in-game speaking icon
                if (SpeakingIconState == SpeakingIconStateType.GotSamples)
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

                return true;
            }

            private volatile SpeakingIconStateType SpeakingIconState = SpeakingIconStateType.WaitingForSamples;
            private enum SpeakingIconStateType
            {
                WaitingForSamples,
                GotSamples,
                Done
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

        CritSect ReceiveStreamsCrit = new CritSect();
        Dictionary<ReceiveStreamID, ReceiveStream> ReceiveStreams = new Dictionary<ReceiveStreamID, ReceiveStream>();

        ReceiveStream GetReceiveStream(ReceiveStreamID id)
        {
            using (ReceiveStreamsCrit.Lock)
            {
                ReceiveStream stream;
                if (!ReceiveStreams.TryGetValue(id, out stream))
                    stream = null;

                return stream;
            }
        }

        ReceiveStream GetOrCreateReceiveStream(ReceiveStreamID id)
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
                    Log($"Create receive stream {id}");

                    stream = new ReceiveStream(this, id, streamInfo);
                    ReceiveStreams.Add(id, stream);
                }

                return stream;
            }
        }

        void DestroyReceiveStream(ReceiveStreamID id)
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

        void DestroyAllReceiveStreams()
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

        private volatile bool _WaitingForConnect = false;
        public bool WaitingForConnect
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
                if (ca.InitNumber == ca.VCClient.CurrentInitNumber)
                    ca.VCClient.server = ca.Server;
            }
            catch
            {

            }

            // regardless.. this attempt is done
            if (ca.InitNumber == ca.VCClient.CurrentInitNumber)
                ca.VCClient._WaitingForConnect = false;
        }

        private string currentServerHost = null;
        //private static int currentServerPort = 0;

        private bool SentServerHandshake = false;
        private DateTime lastServerConnectAttempt = new DateTime();
        void MaintainServer()
        {
            // if we dont have player info, bail now
            if (string.IsNullOrEmpty(AccountName) || string.IsNullOrEmpty(CharacterName) /*|| WeenieID == 0*/)
                return;


            // if desired target server has changed, kill previous connection
            if (currentServerHost != ServerIP /*|| currentServerPort != ServerPort*/)
            {
                // if we were previously connected, kill it
                if (server != null)
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
            if (DateTime.Now.Subtract(lastServerConnectAttempt).TotalMilliseconds < Packet.HeartbeatMsec/*can replace with another value if desired*/)
                return;

            // we are trying now
            lastServerConnectAttempt = DateTime.Now;
            SentServerHandshake = false;

            try
            {
                TcpClient tryServer = new TcpClient();

                Log($"Attempting connection to {ServerIP}");//:{ServerPort}");

                ConnectAttempt ca = new ConnectAttempt();
                ca.VCClient = this;
                ca.InitNumber = CurrentInitNumber;
                ca.Server = tryServer;

                if (tryServer.BeginConnect(ServerIP, DefaultServerPort/*ServerPort*/, ConnectCallback, ca) != null)
                    _WaitingForConnect = true;
            }
            catch
            {
                server = null;
                stagedInfo = null;
            }

        }


        private class ConnectAttempt
        {
            public VCClient VCClient;
            public int InitNumber;
            public TcpClient Server;
        }

        DateTime lastSentRecordSamplesTime = new DateTime();

        DateTime lastHeartbeat = new DateTime();
        private void SendToServer(Packet p)
        {
            if (server == null)
                return;

            lastHeartbeat = DateTime.Now;
            p.InternalSend(server);
        }

        private Packet.StagedInfo stagedInfo = null;
        private Packet ReceiveFromServer()
        {
            if (server == null)
                return null;

            return Packet.InternalReceive(server, ref stagedInfo);
        }

    }
}
