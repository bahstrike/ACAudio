using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace ACAudioVCServer
{
    public class ClientProcessor : WorkerThread
    {
        private ListenServer listener;
        
        class Player
        {
            private readonly TcpClient Client;
            public readonly string AccountName;
            public readonly string CharacterName;
            public readonly int WeenieID;

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
                return $"[{IPAddress}][{AccountName}][{CharacterName}][{WeenieID.ToString("X8")}]";
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

            public void Process()
            {
                // send periodic heartbeat if necessary
                if (DateTime.Now.Subtract(LastHeartbeat).TotalMilliseconds >= Packet.HeartbeatMsec)
                    Send(new Packet(Packet.MessageType.Heartbeat));
            }

            private Server.StreamInfo lastStreamInfo = null;
            public void SetCurrentStreamInfo(Server.StreamInfo streamInfo)
            {
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
                LastHeartbeat = DateTime.Now;
                p.InternalSend(Client);
            }

            public Packet Receive(int headerTimeoutMsec = Packet.DefaultTimeoutMsec, int dataTimeoutMsec = Packet.DefaultTimeoutMsec)
            {
                return Packet.InternalReceive(Client, headerTimeoutMsec, dataTimeoutMsec);
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

        private List<Player> Players = new List<Player>();

        protected override void _Stop_Post()
        {
            base._Stop_Post();

            foreach (Player player in Players)
                player.Disconnect("Server shutdown");
            Players.Clear();
        }

        public ClientProcessor(ListenServer _listener)
        {
            listener = _listener;
        }

        protected sealed override void _Run()
        {
            TcpClient[] clients = listener.CollectClients();
            foreach (TcpClient client in clients)
            {
                // wait for client config
                Packet clientInfo = Packet.InternalReceive(client);//raw packet receive since we have no player entry yet
                if (clientInfo == null || clientInfo.Message != Packet.MessageType.PlayerConnect)
                {
                    // didnt reply in proper fashion?  goodbye
                    client.Close();
                    continue;
                }


                // try to accept client into system
                string accountName = clientInfo.ReadString();
                string characterName = clientInfo.ReadString();
                int weenieID = clientInfo.ReadInt();

                Player player = new Player(client, accountName, characterName, weenieID);


                // check for ban / already connected / etc?
                if (false)
                {
                    player.Disconnect("banned or something lol");
                    continue;
                }



                // check if same account/character name are already connected
                foreach (Player existing in Players)
                {
                    if (player.AccountName != existing.AccountName ||
                        player.CharacterName != existing.CharacterName)
                        continue;

                    // we already have an entry for this character. what to do?
                    Server.Log($"Player {existing} was already connected. Removing existing player entry.");


                    if (!player.IPAddress.Equals(existing.IPAddress))
                        // if ip address is different, send disconnect to previous
                        existing.Disconnect("You were reconnecting from a new IP Address");
                    else
                        // if same ip address, i guess just close our socket and let the existing "connection" become stale
                        existing.Disconnect(null);

                    Players.Remove(existing);

                    break;
                }



                Players.Add(player);

                Server.Log($"Player {player} connected");




                // send server config
                player.SetCurrentStreamInfo(Server.CurrentStreamInfo);
            }


            // process players
            for(int playerIndex=0; playerIndex<Players.Count; playerIndex++)
            {
                Player player = Players[playerIndex];

                player.Process();

                // lost connection?
                if(!player.Connected)
                {
                    Server.Log($"Lost connection to {player}");
                    player.Disconnect(null);// no need to send disconnected message since connection was lost
                    Players.RemoveAt(playerIndex--);
                    continue;
                }

                // i guess they're still there :D
                player.SetCurrentStreamInfo(Server.CurrentStreamInfo);//only sends packet when it needs to


                // see what they have to say
                for (; ; )
                {
                    // dont wait for client unless we at least have a header
                    Packet playerPacket = player.Receive(0);
                    if (playerPacket == null)
                        break;

                    if (playerPacket.Message == Packet.MessageType.Disconnect)
                    {
                        string reason = playerPacket.ReadString();

                        Server.Log($"Player {player} disconnected: {reason}");
                        player.Disconnect(null);//no need to send disconnect message since client will have closed their socket
                        Players.RemoveAt(playerIndex--);
                        continue;
                    }


                    if (playerPacket.Message == Packet.MessageType.RawAudio)
                    {
                        // extract data from raw audio packet
                        byte[] buf = playerPacket.ReadBuffer();
                        if (buf == null || buf.Length == 0)
                            continue;



                        // reconstruct a detailed audio packet that includes the appropriate source information for redistribution
                        Packet detailAudio = new Packet(Packet.MessageType.DetailAudio);
                        detailAudio.WriteInt(player.WeenieID);
                        detailAudio.WriteBuffer(buf);



                        // for now, just send the packet straight back to everyone  (haxx loopback)
                        foreach (Player player2 in Players)
                        {
                            player2.Send(detailAudio);
                        }

                    }


                    // break out of loop if we only want to process 1 player packet at a time.   continue loop to process all
                    //break;
                }

            }
        }
    }
}
