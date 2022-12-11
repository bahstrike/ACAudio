using System;
using System.Collections.Generic;

namespace ACAVCServer
{
    internal class ClientProcessor : WorkerThread
    {
        private ListenServer listener;

        private CritSect PlayersCrit = new CritSect();
        private List<Player> Players = new List<Player>();

        public Player[] GetPlayers()
        {
            using (PlayersCrit.Lock)
                return Players.ToArray();
        }

        protected override void _Stop_Post()
        {
            base._Stop_Post();

            using (PlayersCrit.Lock)
            {
                foreach (Player player in Players)
                    player.Disconnect("Server shutdown");
                Players.Clear();
            }
        }

        public ClientProcessor(ListenServer _listener)
        {
            listener = _listener;
        }

        protected sealed override void _Run()
        {
            DateTime start = DateTime.Now;

            StreamInfo streamInfo = Server.CurrentStreamInfo;//preache;  internal sync

            Player[] newPlayers = listener.CollectPlayers();
            foreach (Player player in newPlayers)
            {


                // allow hosting server to validate a real connected player, or if they are offline or banned, etc
                if(Server.CheckPlayerCallback != null)
                {
                    string reason = Server.CheckPlayerCallback(player);

                    if(reason != null)
                    {
                        player.Disconnect(reason);
                        continue;
                    }
                }



                // check if same account/character name are already connected
                using (PlayersCrit.Lock)
                {
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
                }

                Server.Log($"Player {player} connected");




                // send server config
                player.SetCurrentStreamInfo(streamInfo);
            }



            // process players
            Player[] currentPlayers = GetPlayers();
            foreach (Player player in currentPlayers)
            {
                // lost connection?
                if (!player.Connected)
                {
                    Server.Log($"Lost connection to {player}");
                    player.Disconnect(null);// no need to send disconnected message since connection was lost
                    using (PlayersCrit.Lock)
                        Players.Remove(player);
                    continue;
                }

                // i guess they're still there :D
                player.SetCurrentStreamInfo(Server.CurrentStreamInfo);//only sends packet when it needs to


                // see what they have to say
                for (; ; )
                {
                    // dont wait for client unless we at least have a header
                    Packet playerPacket = player.Receive();
                    if (playerPacket == null)
                        break;


                    //Server.Log($"RECEIVE {playerPacket.Message} FROM {player}");


                    if (playerPacket.Message == Packet.MessageType.Disconnect)
                    {
                        string reason = playerPacket.ReadString();

                        Server.Log($"Player {player} disconnected: {reason}");
                        player.Disconnect(null);//no need to send disconnect message since client will have closed their socket
                        using (PlayersCrit.Lock)
                            Players.Remove(player);
                        continue;
                    }

                    if (playerPacket.Message == Packet.MessageType.ClientStatus)
                    {
                        int newAllegID = playerPacket.ReadInt();
                        int newFellowID = playerPacket.ReadInt();

                        if (newAllegID != player.AllegianceID)
                            Server.Log($"Updating player allegiance {player} --> {newAllegID.ToString("X8")}");

                        if (newFellowID != player.FellowshipID)
                            Server.Log($"Updating player fellowship {player} --> {newFellowID.ToString("X8")}");

                        player.AllegianceID = newAllegID;
                        player.FellowshipID = newFellowID;
                        player.Position = Position.FromStream(playerPacket.Stream, true);

                        // update BVH

                    }


                    if (playerPacket.Message == Packet.MessageType.RawAudio)
                    {
                        int magic = playerPacket.ReadInt();
                        bool loopback = playerPacket.ReadBool();
                        StreamInfo.VoiceChannel speakChannel = (StreamInfo.VoiceChannel)playerPacket.ReadInt();
                        if (speakChannel == StreamInfo.VoiceChannel.Invalid)
                            continue;

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
                        detailAudio.WriteInt((int)speakChannel);
                        detailAudio.WriteInt(player.WeenieID);
                        detailAudio.WriteBuffer(buf);


                        // if loopback, just send back to player
                        if (loopback)
                            player.Send(detailAudio);
                        else
                        {
                            // relay audio packet to anyone who should hear it   (implement BVH??)
                            foreach (Player player2 in currentPlayers)
                            {
                                // dont perform loopback
                                if (object.ReferenceEquals(player, player2))
                                    continue;

                                // perform proximity logic if 3d
                                if (speakChannel == StreamInfo.VoiceChannel.Proximity3D)
                                {
                                    // skip if incompatible landblocks
                                    if (!player.Position.IsCompatibleWith(player2.Position))
                                        continue;

                                    // skip if too far apart
                                    if ((player.Position.Global - player2.Position.Global).Magnitude > StreamInfo.PlayerMaxDist)
                                        continue;
                                }
                                else
                                    if (speakChannel == StreamInfo.VoiceChannel.Allegiance)
                                {
                                    // must skip if either allegiance ID is invalid (two invalids dont make a match)
                                    if (player.AllegianceID == 0 || player2.AllegianceID == 0)
                                        continue;

                                    if (player.AllegianceID != player2.AllegianceID)
                                        continue;
                                }
                                else if (speakChannel == StreamInfo.VoiceChannel.Fellowship)
                                {
                                    // must skip if either fellowship ID is invalid (two invalids dont make a match)
                                    if (player.FellowshipID == 0 || player2.FellowshipID == 0)
                                        continue;

                                    if (player.FellowshipID != player2.FellowshipID)
                                        continue;
                                }
                                else
                                    // unrecognized channel type
                                    continue;

                                player2.Send(detailAudio);
                            }
                        }

                    }


                    // break out of loop if we only want to process 1 player packet at a time.   continue loop to process all
                    //break;
                }




                // check if we should send the client some info about their surroundings
                if (player.Position.IsValid && DateTime.Now.Subtract(player.LastServerStatusTime).TotalMilliseconds > 250)
                {
                    // replace with BVH
                    List<Player> nearbyPlayers = new List<Player>();
                    foreach (Player player2 in currentPlayers)
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
                string disconnectReason = player.Process();
                if (!string.IsNullOrEmpty(disconnectReason))
                    using (PlayersCrit.Lock)
                    {
                        player.Disconnect(disconnectReason);
                        Players.Remove(player);// remove from our real list but i guess let the shadowcopy pretend
                    }
            }

            double runSeconds = DateTime.Now.Subtract(start).TotalSeconds;
            using (RunTimesCrit.Lock)
            {
                while (RunTimes.Count > 10000)
                    RunTimes.RemoveAt(0);

                RunTimes.Add(runSeconds);
            }
        }

        private CritSect RunTimesCrit = new CritSect();
        private List<double> RunTimes = new List<double>();

        public double[] CollectRunTimes()
        {
            using (RunTimesCrit.Lock)
            {
                double[] ret = RunTimes.ToArray();
                RunTimes.Clear();
                return ret;
            }
        }
    }
}
