// this worker thread's sole purpose is to asynchronously accept incoming clients and queue them for another thread to pick up via CollectClients

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace ACAVCServer
{
    internal class ListenServer : WorkerThread
    {
        public readonly IPAddress IPAddress;
        public readonly int Port;

        public ListenServer(IPAddress _IPAddress, int _Port)
        {
            IPAddress = _IPAddress;
            Port = _Port;
        }

        protected sealed override void _Stop_Pre()
        {
            base._Stop_Pre();

            // this causes exception to break out of thread's blocking AcceptTcpClient() call
            if (listener != null)
                listener.Stop();
        }

        protected sealed override void _Stop_Post()
        {
            base._Stop_Post();
        
            // kill off any potential pending clients that weren't picked up by other logic
            Player[] pendingPlayers = CollectPlayers();
            foreach (Player player in pendingPlayers)
                player.Disconnect("Listen server shutdown");
        }

        public Player[] CollectPlayers()
        {
            Player[] ret;
            using (playersLock.Lock)
            {
                ret = players.ToArray();
                players.Clear();
            }
            return ret;
        }

        private TcpListener listener = null;
        private List<Player> players = new List<Player>();
        private CritSect playersLock = new CritSect();

        protected sealed override void _Run()
        {
            try
            {
                if (listener == null)
                {
                    listener = new TcpListener(IPAddress, Port);
                    listener.Start();

                    Server.Log($"Listening on {IPAddress}:{Port}");
                }

                Server.IncomingConnectionsCount++;
                    
                TcpClient client = listener.AcceptTcpClient();
                client.NoDelay = true;



                // wait for client config
                Packet clientInfo = Packet.InternalReceive(client, allowTimeoutToCancelPartial:true/*this is safe because if we get no complete packet then we close connection anyway*/);//raw packet receive since we have no player entry yet
                if (clientInfo != null)
                {
                    Server.PacketsReceivedCount++;
                    Server.PacketsReceivedBytes += (uint)clientInfo.FinalSizeBytes;
                }

                if (clientInfo == null || clientInfo.Message != Packet.MessageType.PlayerConnect)
                {
                    // didnt reply in proper fashion?  goodbye
                    client.Close();
                    return;
                }

                // try to accept client into system
                string accountName = clientInfo.ReadString();
                string characterName = clientInfo.ReadString();
                int weenieID = clientInfo.ReadInt();

                Player player = new Player(client, accountName, characterName, weenieID);


                using (playersLock.Lock)
                    players.Add(player);
            }
            catch
            {
                if (listener != null)
                {
                    try
                    {
                        listener.Stop();
                    }
                    catch
                    {

                    }

                    listener = null;
                }
            }
        }
    }
}
