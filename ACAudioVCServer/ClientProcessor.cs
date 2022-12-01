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
            public TcpClient Client;
            public string AccountName;
            public string CharacterName;
            public int WeenieID;

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

            public void Disconnect(string reason)
            {
                Packet packet = new Packet(Packet.MessageType.Disconnect);
                packet.WriteString(reason);
                packet.Send(Client);

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
                Packet clientInfo = Packet.Receive(client);
                if (clientInfo == null)
                {
                    // didnt reply in proper fashion?  goodbye
                    client.Close();
                    continue;
                }


                // try to accept client into system
                Player player = new Player();
                player.Client = client;
                player.AccountName = clientInfo.ReadString();
                player.CharacterName = clientInfo.ReadString();
                player.WeenieID = clientInfo.ReadInt();


                // check for ban / already connected / etc?
                if (false)
                {
                    player.Disconnect("banned or something lol");
                    continue;
                }



                // check if same account/character name are already connected
                foreach(Player existing in Players)
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
                        existing.Client.Close();

                    Players.Remove(existing);

                    break;
                }



                Players.Add(player);

                Server.Log($"Player {player} connected");




                // send server config
                Packet serverInfo = new Packet(Packet.MessageType.StreamInfo);

                serverInfo.WriteBool(true);//µ-law
                serverInfo.WriteInt(16);//bitdepth
                serverInfo.WriteInt(8000);//8000);//11025);//22050);//44100);//sampling frequency

                serverInfo.Send(client);
            }


            // process players
            for(int playerIndex=0; playerIndex<Players.Count; playerIndex++)
            {
                Player player = Players[playerIndex];

                // lost connection?
                if(!player.Client.Connected)
                {
                    Server.Log($"Lost connection to {player}");
                    player.Client.Close();// no need to send disconnected message since connection was lost
                    Players.RemoveAt(playerIndex--);
                    continue;
                }

                // dont wait for each client unless we at least have a header
                Packet playerPacket = Packet.Receive(player.Client, 0);
                if (playerPacket == null)
                    continue;

                if(playerPacket.Message == Packet.MessageType.Disconnect)
                {
                    string reason = playerPacket.ReadString();

                    Server.Log($"Player {player} disconnected: {reason}");
                    player.Client.Close();//no need to send disconnect message since client will have closed their socket
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
                        detailAudio.Send(player2.Client);
                    }

                }

                //Server.Log("Relayed packet");
            }
        }
    }
}
