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

            public void Disconnect()
            {
                // send "screw you" packet?

                Client.Close();
            }
        }

        private List<Player> Players = new List<Player>();

        protected override void _Stop_Pre()
        {
            base._Stop_Pre();

            foreach (Player player in Players)
                player.Disconnect();
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
                // send server config
                Packet serverInfo = new Packet();

                serverInfo.WriteInt(44100);//sampling frequency

                serverInfo.Send(client);


                // wait for client config
                Packet clientInfo = Packet.Receive(client);
                if(clientInfo == null)
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
                if(false)
                {
                    player.Disconnect();
                    continue;
                }


                Players.Add(player);
            }


            // process players
            foreach(Player player in Players)
            {
                // dont wait for each client unless we at least have a header
                Packet playerPacket = Packet.Receive(player.Client, 0);
                if (playerPacket == null)
                    continue;


                // for now, just send the packet straight back  (haxx loopback)
                playerPacket.Send(player.Client);
            }
        }
    }
}
