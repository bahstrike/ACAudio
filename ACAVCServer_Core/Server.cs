// main server.  all logic is handled via child threads.
//
// all public API of this module should be thread-safe.
// however, the Server.xxxxxCallback  events typically require caller to synchronize their own data.

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace ACAVCServer_Core
{
    public static class Server
    {
        private static ListenServer listener = null;
        private static ClientProcessor clientProcessor = null;

        // NOTE: LogCallback is called from threads; you must handle syncronization yourself!
        public delegate void LogDelegate(string s);
        public static LogDelegate LogCallback = null;//optional

        // WHEN INTEGRATED WITH A REAL AC SERVER:
        // it is recommended you handle CheckPlayer to determine if said account/character/weenie is
        // a legitimate online player who is actually in-game and deserves to join voicechat server.
        //
        // PARAMETER:
        // ONLY the following fields are valid  (other items like allegiance/fellowship come in packets later):
        //    Player.IPAddress
        //    Player.AccountName
        //    Player.CharacterName
        //    Player.WeenieID
        //
        // Player objects are inherently thread-safe.   you may retain Player objects for future use/query.
        // you may arbitrarily kick a Player by setting Player.WantDisconnectReason to any string other than null.
        //
        //
        // RETURN:
        // string return value should be NULL to accept player.
        // any other string will be the "reason" sent with the Disconnect packet to the rejected player.
        //
        // NOTE:
        // if returning NULL to allow player, there is still an internal check for same account/character names.
        // the previous player entry's socket will be disconnected and allow this new IPAddress and WeenieID
        //
        // WARNING:
        // CheckPlayerCallback is called from threads; you must handle syncronization yourself!
        public delegate string CheckPlayerDelegate(Player player);
        public static CheckPlayerDelegate CheckPlayerCallback = null;//optional; null allows all player connections

        public static volatile bool ShowPlayerIPAndAccountInLogs = false;

        public struct PerformanceMetrics
        {
            public readonly ulong IncomingConnectionsCount;
            public readonly ulong PacketsReceivedCount;
            public readonly ulong PacketsReceivedBytes;
            public readonly ulong PacketsSentCount;
            public readonly ulong PacketsSentBytes;

            public PerformanceMetrics(ulong _IncomingConnectionsCount,
                                        ulong _PacketsReceivedCount,
                                        ulong _PacketsReceivedBytes,
                                        ulong _PacketsSentCount,
                                        ulong _PacketsSentBytes)
            {
                IncomingConnectionsCount = _IncomingConnectionsCount;
                PacketsReceivedCount = _PacketsReceivedCount;
                PacketsReceivedBytes = _PacketsReceivedBytes;
                PacketsSentCount = _PacketsSentCount;
                PacketsSentBytes = _PacketsSentBytes;
            }

            public static PerformanceMetrics operator +(PerformanceMetrics a, PerformanceMetrics b)
            {
                return new PerformanceMetrics(
                    a.IncomingConnectionsCount + b.IncomingConnectionsCount,
                    a.PacketsReceivedCount + b.PacketsReceivedCount,
                    a.PacketsReceivedBytes + b.PacketsReceivedBytes,
                    a.PacketsSentCount + b.PacketsSentCount,
                    a.PacketsSentBytes + b.PacketsSentBytes);
            }

            public static PerformanceMetrics Zero
            {
                get
                {
                    return new PerformanceMetrics(0, 0, 0, 0, 0);
                }
            }
        }

        public static PerformanceMetrics CollectCurrentPerformanceMetrics()
        {
            // snag values
            PerformanceMetrics perf = new PerformanceMetrics((ulong)IncomingConnectionsCount, (ulong)PacketsReceivedCount, (ulong)PacketsReceivedBytes, (ulong)PacketsSentCount, (ulong)PacketsSentBytes);

            // reset
            IncomingConnectionsCount = 0;
            PacketsReceivedCount = 0;
            PacketsReceivedBytes = 0;
            PacketsSentCount = 0;
            PacketsSentBytes = 0;

            // return
            return perf;
        }

        internal static volatile int IncomingConnectionsCount = 0;
        internal static volatile int PacketsReceivedCount = 0;
        internal static volatile uint PacketsReceivedBytes = 0;//should be reset to 0 by whoever is scraping the value to prevent overflow
        internal static volatile int PacketsSentCount = 0;
        internal static volatile uint PacketsSentBytes = 0;//should be reset to 0 by whoever is scraping the value to prevent overflow

        internal static void Log(string s)
        {
            if (LogCallback != null)
                LogCallback(s);
        }

        public static void Init()
        {
            Shutdown();

            listener = new ListenServer(IPAddress.Any, 42420);
            listener.Start();

            clientProcessor = new ClientProcessor(listener);
            clientProcessor.Start();
        }

        public static void Shutdown()
        {
            // tear down listener first just in case someones trying to connect while we are shutting down
            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }

            if (clientProcessor != null)
            {
                clientProcessor.Stop();
                clientProcessor = null;
            }

            IncomingConnectionsCount = 0;
            PacketsReceivedCount = 0;
            PacketsReceivedBytes = 0;
            PacketsSentCount = 0;
            PacketsSentBytes = 0;
        }

        private static CritSect _CurrentStreamInfoCrit = new CritSect();
        private static StreamInfo _CurrentStreamInfo = new StreamInfo(true, 16, 8000);
        public static StreamInfo CurrentStreamInfo
        {
            get
            {
                using (_CurrentStreamInfoCrit.Lock)
                    return _CurrentStreamInfo;
            }

            set
            {
                using (_CurrentStreamInfoCrit.Lock)
                {
                    // dont change if the actual properties are teh same.. preserve the previous magic number (better for packet sequencing and such perhaps)
                    if (StreamInfo.CompareProperties(value, _CurrentStreamInfo))
                        return;

                    _CurrentStreamInfo = value;
                }
            }
        }

        public static Player[] GetPlayers()
        {
            if (clientProcessor == null)
                return new Player[0];

            return clientProcessor.GetPlayers();
        }

        public static double[] CollectRunTimes()
        {
            if (clientProcessor == null)
                return new double[0];

            return clientProcessor.CollectRunTimes();
        }
    }
}
