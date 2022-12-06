﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using ACACommon;

namespace ACAudioVCServer
{
    public class ClientProcessor : WorkerThread
    {
        private ListenServer listener;

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
            StreamInfo streamInfo = Server.CurrentStreamInfo;//preache;  internal sync

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
                /*if (false)
                {
                    player.Disconnect("banned or something lol");
                    continue;
                }*/



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
                player.SetCurrentStreamInfo(streamInfo);
            }



            // process players
            for (int playerIndex=0; playerIndex<Players.Count; playerIndex++)
            {
                Player player = Players[playerIndex];

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


                    //Server.Log($"RECEIVE {playerPacket.Message} FROM {player}");


                    if (playerPacket.Message == Packet.MessageType.Disconnect)
                    {
                        string reason = playerPacket.ReadString();

                        Server.Log($"Player {player} disconnected: {reason}");
                        player.Disconnect(null);//no need to send disconnect message since client will have closed their socket
                        Players.RemoveAt(playerIndex--);
                        continue;
                    }

                    if(playerPacket.Message == Packet.MessageType.ClientStatus)
                    {
                        player.Position = Position.FromStream(playerPacket, true);

                        // update BVH

                    }


                    if (playerPacket.Message == Packet.MessageType.RawAudio)
                    {
                        int magic = playerPacket.ReadInt();
                        bool loopback = playerPacket.ReadBool();
                        bool speak3d = playerPacket.ReadBool();

                        // if magic doesnt match current server streaminfo (wrong format) then just ignore the rest of this packet
                        if (magic != streamInfo.magic)
                            continue;

                        // extract data from raw audio packet
                        byte[] buf = playerPacket.ReadBuffer();
                        if (buf == null || buf.Length == 0)
                            continue;



                        // reconstruct a detailed audio packet that includes the appropriate source information for redistribution
                        Packet detailAudio = new Packet(Packet.MessageType.DetailAudio);
                        detailAudio.WriteInt(streamInfo.magic);
                        detailAudio.WriteBool(speak3d);
                        detailAudio.WriteInt(player.WeenieID);
                        detailAudio.WriteBuffer(buf);


                        // if loopback, just send back to player
                        if (loopback)
                            player.Send(detailAudio);
                        else
                        {
                            // relay audio packet to anyone who should hear it   (implement BVH??)
                            foreach (Player player2 in Players)
                            {
                                // dont perform loopback
                                if (object.ReferenceEquals(player, player2))
                                    continue;

                                // perform proximity logic if 3d
                                if (speak3d)
                                {
                                    // skip if incompatible landblocks
                                    if (!player.Position.IsCompatibleWith(player2.Position))
                                        continue;

                                    // skip if too far apart
                                    if ((player.Position.Global - player2.Position.Global).Magnitude > StreamInfo.PlayerMaxDist)
                                        continue;
                                }

                                player2.Send(detailAudio);
                            }
                        }

                    }


                    // break out of loop if we only want to process 1 player packet at a time.   continue loop to process all
                    //break;
                }




                // check if we should send the client some info about their surroundings
                if(player.Position.IsValid && DateTime.Now.Subtract(player.LastServerStatusTime).TotalMilliseconds > 250)
                {
                    // replace with BVH
                    List<Player> nearbyPlayers = new List<Player>();
                    foreach(Player player2 in Players)
                    {
                        if (object.ReferenceEquals(player, player2))
                            continue;

                        if (!player2.Position.IsValid)
                            continue;

                        // skip if landblocks are incompatible
                        if (!player.Position.IsCompatibleWith(player2.Position))
                            continue;

                        // skip if outside of audible range
                        if ((player.Position.Global - player2.Position.Global).Magnitude > StreamInfo.PlayerMaxDist)
                            continue;

                        // should be audible
                        nearbyPlayers.Add(player2);

                        break;//could keep going if we want a real list, but for now we're just sending a flag if anyone is in range
                    }

                    Packet p = new Packet(Packet.MessageType.ServerStatus);
                    p.WriteInt(Players.Count);
                    p.WriteBool(nearbyPlayers.Count > 0);
                    player.Send(p);


                    player.LastServerStatusTime = DateTime.Now;
                }




                // do other internal stuff like send heartbeat or watever
                player.Process();
            }
        }
    }
}
