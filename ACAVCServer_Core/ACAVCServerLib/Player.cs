using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace ACAVCServerLib
{
    public class Player
    {
        private readonly TcpClient Client;
        public readonly string AccountName;
        public readonly string CharacterName;
        public readonly int WeenieID;

        private volatile int _AllegianceID = StreamInfo.InvalidAllegianceID;
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

        private volatile int _FellowshipID = StreamInfo.InvalidFellowshipID;
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
        public Position Position
        {
            get
            {
                using (_PositionCrit.Lock)
                    return _Position;
            }

            internal set
            {
                using (_PositionCrit.Lock)
                    _Position = value;
            }
        }

        private volatile string _WantDisconnectReason = null;
        /// <summary>
        /// cant clear once issued:  but give a reason string and this player socket will be disconnected.
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

        public IPAddress IPAddress
        {
            get
            {
                return ((IPEndPoint)Client.Client.RemoteEndPoint).Address;
            }
        }

        public bool Connected
        {
            get
            {
                return Client.Connected;
            }
        }

        private DateTime _LastServerStatusTime = new DateTime();
        public DateTime LastServerStatusTime
        {
            get
            {
                return _LastServerStatusTime;
            }

            internal set
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
