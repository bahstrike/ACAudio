using System;
using System.Net;
using System.Net.Sockets;

namespace ACAVCServer
{
    /// <summary>
    /// Contains information about a connected client.
    /// </summary>
    public class Player
    {
        private readonly TcpClient Client;

        /// <summary>
        /// The name of the player's account used when connecting to the Asheron's Call server.
        /// </summary>
        public readonly string AccountName;

        /// <summary>
        /// The in-game name of the player's character.
        /// </summary>
        public readonly string CharacterName;

        /// <summary>
        /// The in-game unique ID of the player's world object ("weenie").
        /// </summary>
        public readonly int WeenieID;

        private volatile int _AllegianceID = 0;
        /// <summary>
        /// The ID of the in-game monarch the player is in, or 0 otherwise. May not be available until a short while after the player connects.
        /// </summary>
        public int AllegianceID
        {
            get
            {
                return _AllegianceID;
            }

            internal set
            {
                _AllegianceID = value;
            }
        }

        private volatile int _FellowshipID = 0;
        /// <summary>
        /// The ID of the in-game fellowship the player is in, or 0 otherwise.
        /// </summary>
        public int FellowshipID
        {
            get
            {
                return _FellowshipID;
            }

            internal set
            {
                _FellowshipID = value;
            }
        }

        private CritSect _PositionCrit = new CritSect();
        private Position _Position = Position.Invalid;
        internal Position Position
        {
            get
            {
                using (_PositionCrit.Lock)
                    return _Position;
            }

            set
            {
                using (_PositionCrit.Lock)
                    _Position = value;
            }
        }

        private volatile string _WantDisconnectReason = null;
        /// <summary>
        /// Set to any non-null string to kick the player. Cannot be undone once set!
        /// </summary>
        public string WantDisconnectReason
        {
            internal get
            {
                return _WantDisconnectReason;
            }

            set
            {
                _WantDisconnectReason = value;
            }
        }


        internal Player(TcpClient _Client, string _AccountName, string _CharacterName, int _WeenieID)
        {
            Client = _Client;
            AccountName = _AccountName;
            CharacterName = _CharacterName;
            WeenieID = _WeenieID;
        }

        private DateTime LastHeartbeat = new DateTime();

        public override string ToString()
        {
            string str = $"[{CharacterName}][{WeenieID.ToString("X8")}][{AllegianceID.ToString("X8")}][{FellowshipID.ToString("X8")}]";

            if (Server.ShowPlayerIPAndAccountInLogs)
                str = $"[{IPAddress}][{AccountName}]{str}";

            return str;
        }

        /// <summary>
        /// IP address of the connected player.
        /// </summary>
        public IPAddress IPAddress
        {
            get
            {
                return ((IPEndPoint)Client.Client.RemoteEndPoint).Address;
            }
        }

        /// <summary>
        /// Whether or not the player is still connected.
        /// </summary>
        public bool Connected
        {
            get
            {
                return Client.Connected;
            }
        }

        private DateTime _LastServerStatusTime = new DateTime();
        internal DateTime LastServerStatusTime
        {
            get
            {
                return _LastServerStatusTime;
            }

            set
            {
                _LastServerStatusTime = value;
            }
        }

        internal string Process()
        {
            if (!string.IsNullOrEmpty(WantDisconnectReason))
                return WantDisconnectReason;

            // send periodic heartbeat if necessary
            if (DateTime.Now.Subtract(LastHeartbeat).TotalMilliseconds >= Packet.HeartbeatMsec)
                Send(new Packet(Packet.MessageType.Heartbeat));

            return null;//still good
        }

        private StreamInfo lastStreamInfo = null;
        internal void SetCurrentStreamInfo(StreamInfo streamInfo)
        {
            // if incoming is invalid then bust
            if (streamInfo == null)
                return;

            // if same who cares
            if (lastStreamInfo != null && lastStreamInfo.magic == streamInfo.magic)
                return;

            // if new, lets submit info immediately
            Packet packet = new Packet(Packet.MessageType.StreamInfo);

            packet.WriteInt(streamInfo.magic);
            packet.WriteBool(streamInfo.ulaw);//µ-law
            packet.WriteInt(streamInfo.bitDepth);//bitdepth
            packet.WriteInt(streamInfo.sampleRate);//8000);//11025);//22050);//44100);//sampling frequency

            Send(packet);

            // remember
            lastStreamInfo = streamInfo;
        }

        internal void Send(Packet p)
        {
            //Server.Log($"SEND {p.Message} TO {this}");

            LastHeartbeat = DateTime.Now;
            p.InternalSend(Client);

            Server.PacketsSentCount++;
            Server.PacketsSentBytes += (uint)p.FinalSizeBytes;// requires Send before FinalSizeBytes is valid
        }

        private Packet.StagedInfo stagedInfo = null;
        internal Packet Receive()
        {
            Packet p = Packet.InternalReceive(Client, ref stagedInfo);
            if (p != null)
            {
                Server.PacketsReceivedCount++;
                Server.PacketsReceivedBytes += (uint)p.FinalSizeBytes;
            }

            return p;
        }

        // specify reason:null to skip sending a disconnect packet and just close the socket
        internal void Disconnect(string reason)
        {
            if (reason != null)
            {
                Packet packet = new Packet(Packet.MessageType.Disconnect);
                packet.WriteString(reason);
                Send(packet);
            }

            Client.Close();
        }
    }
}
