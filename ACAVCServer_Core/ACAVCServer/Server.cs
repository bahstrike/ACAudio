using System.Net;

namespace ACAVCServer
{
    /// <summary>
    /// Main server module. All operation is handled in internal threads. All methods are thread-safe except where noted (such as Callbacks).
    /// </summary>
    public static class Server
    {
        private static ListenServer listener = null;
        private static ClientProcessor clientProcessor = null;

        /// <summary>
        /// Delegate declaration for <see cref="LogCallback"/>.
        /// </summary>
        /// <param name="s">Log message string</param>
        public delegate void LogDelegate(string s);

        /// <summary>
        /// Optional callback to receive log messages. Invoked by threads; you must handle synchronization yourself.
        /// </summary>
        public static LogDelegate LogCallback = null;//optional

        /// <summary>
        /// Delegate declaration for <see cref="CheckPlayerCallback"/>
        /// </summary>
        /// <param name="player">Incoming player connection</param>
        /// <returns>Reason to reject player, or null to accept</returns>
        public delegate string CheckPlayerDelegate(Player player);

        /// <summary>
        /// Optional callback to check incoming player connections and reject if desired. Return null to accept, or return a reject reason string. Only IPAddress/AccountName/CharacterName/WeenieID are valid. Invoked by threads; you must handle synchronization yourself.
        /// </summary>
        public static CheckPlayerDelegate CheckPlayerCallback = null;//optional; null allows all player connections

        /// <summary>
        /// Whether log messages should contain player IP addresses and account names.
        /// </summary>
        public static volatile bool ShowPlayerIPAndAccountInLogs = true;

        /// <summary>
        /// Total number of raw TCP connection attempts to listen port. You can reset to 0 if you wish to maintain your own running total.
        /// </summary>
        public static volatile int IncomingConnectionsCount = 0;

        /// <summary>
        /// Total number of TCP packets successfully received. You can reset to 0 if you wish to maintain your own running total.
        /// </summary>
        public static volatile int PacketsReceivedCount = 0;

        /// <summary>
        /// Total number of bytes received via successful packets. You can reset to 0 if you wish to maintain your own running total.
        /// </summary>
        public static volatile uint PacketsReceivedBytes = 0;//should be reset to 0 by whoever is scraping the value to prevent overflow

        /// <summary>
        /// Total number of TCP packets sent to clients. You can reset to 0 if you wish to maintain your own running total.
        /// </summary>
        public static volatile int PacketsSentCount = 0;

        /// <summary>
        /// Total number of bytes sent to clients via packets. You can reset to 0 if you wish to maintain your own running total.
        /// </summary>
        public static uint PacketsSentBytes = 0;

        internal static void Log(string s)
        {
            if (LogCallback != null)
                LogCallback(s);
        }

        /// <summary>
        /// Determines if the server is currently running.
        /// </summary>
        public static bool IsRunning
        {
            get
            {
                return (listener != null && clientProcessor != null);
            }
        }

        /// <summary>
        /// Start the server by spinning up internal threads. If server was already running, it is stopped before starting again.
        /// </summary>
        /// <param name="serverIP">Optional override to force hosting on a particular adapter. Pass null for implicit IPAddress.Any</param>
        public static void Start(IPAddress serverIP=null)
        {
            Stop();

            listener = new ListenServer(serverIP ?? IPAddress.Any, 42420);
            listener.Start();

            clientProcessor = new ClientProcessor(listener);
            clientProcessor.Start();
        }

        /// <summary>
        /// Gracefully shuts down server by issuing disconnect message to all players and tearing down internal threads.
        /// </summary>
        public static void Stop()
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

            // reset metrics
            IncomingConnectionsCount = 0;
            PacketsReceivedCount = 0;
            PacketsReceivedBytes = 0;
            PacketsSentCount = 0;
            PacketsSentBytes = 0;
        }

        private static CritSect _CurrentStreamInfoCrit = new CritSect();
        private static StreamInfo _CurrentStreamInfo = new StreamInfo(true, 16, 8000);

        /// <summary>
        /// Gets/sets the current voice codec. This may be updated at any time and will automatically synchronize to clients.
        /// </summary>
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

        /// <summary>
        /// Gets a list of all currently connected players.
        /// </summary>
        /// <returns>Array of players</returns>
        public static Player[] GetPlayers()
        {
            if (clientProcessor == null)
                return new Player[0];

            return clientProcessor.GetPlayers();
        }

        /// <summary>
        /// Retrieves the count/durations of recent client processing runs. Internal buffer is cleared with each call. Optional; will only maintain several thousand of the most recent values.
        /// </summary>
        /// <returns>Array of durations, in seconds</returns>
        public static double[] CollectRunTimes()
        {
            if (clientProcessor == null)
                return new double[0];

            return clientProcessor.CollectRunTimes();
        }
    }
}
