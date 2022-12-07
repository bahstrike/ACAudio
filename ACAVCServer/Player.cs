using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using ACACommon;

namespace ACAVCServer
{
    public class Player
    {
        private readonly TcpClient Client;
        public readonly string AccountName;
        public readonly string CharacterName;
        public readonly int WeenieID;

        public int AllegianceID = StreamInfo.InvalidAllegianceID;
        public int FellowshipID = StreamInfo.InvalidFellowshipID;
        public Position Position = Position.Invalid;

        public Player(TcpClient _Client, string _AccountName, string _CharacterName, int _WeenieID)
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

        public DateTime LastServerStatusTime = new DateTime();

        public void Process()
        {
            // send periodic heartbeat if necessary
            if (DateTime.Now.Subtract(LastHeartbeat).TotalMilliseconds >= Packet.HeartbeatMsec)
                Send(new Packet(Packet.MessageType.Heartbeat));
        }

        private StreamInfo lastStreamInfo = null;
        public void SetCurrentStreamInfo(StreamInfo streamInfo)
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

        public void Send(Packet p)
        {
            //Server.Log($"SEND {p.Message} TO {this}");

            LastHeartbeat = DateTime.Now;
            p.InternalSend(Client);

            Server.PacketsSentCount++;
            Server.PacketsSentBytes += (uint)p.FinalSizeBytes;// requires Send before FinalSizeBytes is valid
        }

        public Packet Receive(int headerTimeoutMsec = Packet.DefaultTimeoutMsec, int dataTimeoutMsec = Packet.DefaultTimeoutMsec)
        {
            Packet p = Packet.InternalReceive(Client, headerTimeoutMsec, dataTimeoutMsec);
            if (p != null)
            {
                Server.PacketsReceivedCount++;
                Server.PacketsReceivedBytes += (uint)p.FinalSizeBytes;
            }

            return p;
        }

        // specify reason:null to skip sending a disconnect packet and just close the socket
        public void Disconnect(string reason)
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
