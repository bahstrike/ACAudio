using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;

using Smith;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using VirindiViewService.Controls;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Diagnostics;

using ACACommon;
using System.Net;
using System.Drawing;

namespace ACAVCServer
{
    [WireUpBaseEvents]
    [FriendlyName(PluginName)]
    public class PluginCore : Decal.Adapter.PluginBase
    {
        public const string PluginName = "ACAVCServer";//needs to match namespace or "embedded resources" wont work

        private static PluginCore _Instance = null;
        public static PluginCore Instance { get { return _Instance; } }

        public static int InstanceNumberGen = 0;
        public int InstanceNumber = 0;

        public static PluginHost PluginHost
        {
            get
            {
                return Instance.Host;
            }
        }

        public static CoreManager CoreManager
        {
            get
            {
                return Instance.Core;
            }
        }

        public VirindiViewService.HudView View;


        private static void _LogInfo(string s)
        {
            Log($"INFO: {s}");
        }

        private static void _LogWarning(string s)
        {
            Log($"WARNING: {s}");
        }

        private static void _LogError(string s)
        {
            Log($"ERROR: {s}");
        }

        class Metrics
        {
            // define some UI scales (non-functional; would change depending on server popularity)
            public const int expectedSpeakingPlayers = 30;
            public const int expectedListenersPerPlayer = 5;


            // declare our local performance metric vars and whatnot
            public uint totalIncomingConnectionsCount = 0;
            public uint totalPacketsReceivedCount = 0;
            public ulong totalPacketsReceivedBytes = 0;
            public uint totalPacketsSentCount = 0;
            public ulong totalPacketsSentBytes = 0;
            public ulong numRuns = 0;
            public ulong slowRuns = 0;
            public double maxRunTime = 0.0;
            public double avgRunTime = 0.0;
            public int avgSentBytes = 0;
            public int avgReceivedBytes = 0;
            public const int sleepMsec = 50;

            public static double slowRunTime
            {
                get
                {
                    return ((double)100/*anticipate 100msec for an audio chunk?*/ / 1000.0) * 0.2/*lets set our bar lower than bare minimum that client might expect*/;
                }
            }

            public void Preprocess()
            {
                // retrieve / preprocess performance metrics

                totalIncomingConnectionsCount += (uint)Server.IncomingConnectionsCount;
                Server.IncomingConnectionsCount = 0;

                totalPacketsReceivedCount += (uint)Server.PacketsReceivedCount;
                Server.PacketsReceivedCount = 0;

                uint receivedBytes = Server.PacketsReceivedBytes;
                Server.PacketsReceivedBytes = 0;
                totalPacketsReceivedBytes += receivedBytes;
                avgReceivedBytes = (int)(avgReceivedBytes + receivedBytes) / 2;

                totalPacketsSentCount += (uint)Server.PacketsSentCount;
                Server.PacketsSentCount = 0;

                uint sentBytes = Server.PacketsSentBytes;
                Server.PacketsSentBytes = 0;
                totalPacketsSentBytes += sentBytes;
                avgSentBytes = (int)(avgSentBytes + sentBytes) / 2;

                double[] runTimes = Server.CollectRunTimes();
                if (runTimes.Length > 0)
                {
                    numRuns += (ulong)runTimes.Length;

                    double avg = 0.0;
                    foreach (double tm in runTimes)
                    {
                        maxRunTime = Math.Max(maxRunTime, tm);
                        avg += tm;

                        if (tm > slowRunTime)
                            slowRuns++;
                    }
                    avg /= (double)runTimes.Length;

                    avgRunTime = (avgRunTime + avg) / 2.0;
                }
            }
        }

        Metrics metrics = new Metrics();

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup()
        {
            InstanceNumber = InstanceNumberGen++;
            _Instance = this;

            Smith.Log.InfoCallback = _LogInfo;
            Smith.Log.WarningCallback = _LogWarning;
            Smith.Log.ErrorCallback = _LogError;

            try
            {
                if (File.Exists(FinalLogFilepath))
                    File.Delete(FinalLogFilepath);

                Log("----------------------------------------------------------------------");
                Log("                       ACAVCServer Startup");
                Log("----------------------------------------------------------------------");



                //Log("Generate virindi view");
                VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
                VirindiViewService.ViewProperties properties;
                VirindiViewService.ControlGroup controls;
                parser.ParseFromResource($"{PluginName}.mainView.xml", out properties, out controls);

                View = new VirindiViewService.HudView(properties, controls);




                HudCombo determineIPCombo = View["DetermineIP"] as HudCombo;
                determineIPCombo.Clear();

                determineIPCombo.AddItem("-MANUAL-", null);
                determineIPCombo.AddItem("ipinfo.io/ip", null);
                determineIPCombo.AddItem("bot.whatismyipaddress.com", null);
                determineIPCombo.AddItem("icanhazip.com", null);

                determineIPCombo.Change += DetermineIPCombo_Change;



                // create custom stuff
                /*HudFixedLayout contentLayout = View["MainContent"] as HudFixedLayout;
                HudMeter hudCPU = new HudMeter();
                hudCPU.InternalName = "CPUMeter";
                Box2 rc = new Box2(View["CPULabel"].ClipRegion);
                rc.Left = rc.Right;
                rc.Right = (double)View["CPUPostfix"].ClipRegion.Left;
                contentLayout.AddControl(hudCPU, (Rectangle)rc);*/

                HudProgressBar meter;

                meter = View["CPUMeter"] as HudProgressBar;
                meter.Min = 0;
                meter.Max = 100;
                meter.Position = 0;

                meter = View["RECVMeter"] as HudProgressBar;
                meter.Min = 0;
                meter.Max = 100;
                meter.Position = 0;

                meter = View["SENDMeter"] as HudProgressBar;
                meter.Min = 0;
                meter.Max = 100;
                meter.Position = 0;




                using (INIFile ini = INIFile)
                {
                    (View["Enable"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "Enable", "1") != "0";
                    (View["Advertise"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "Advertise", "1") != "0";

                    determineIPCombo.Current = int.Parse(ini.GetKeyString(Core.CharacterFilter.AccountName, "DetermineIP", "0"));

                }



                // trigger UI events
                DetermineIPCombo_Change(determineIPCombo, new EventArgs());





                Server.LogCallback = Log;




                //Log("hook stuff");
                Core.RenderFrame += _Process;
                Core.CharacterFilter.Logoff += _CharacterFilter_Logoff;
                Core.ChatBoxMessage += _ChatBoxMessage;



            }
            catch (Exception ex)
            {
                Log($"Startup exception: {ex.Message}");
            }
        }

        private void DetermineIPCombo_Change(object sender, EventArgs e)
        {
            HudCombo combo = (sender as HudCombo);
            if (combo.Current == 0)
            {
                using (INIFile ini = INIFile)
                    (View["PublicIP"] as HudTextBox).Text = ini.GetKeyString(Core.CharacterFilter.AccountName, "ManualPublicIP", string.Empty);

                return;
            }

            string queryAddress = "http://" + (combo[combo.Current] as HudStaticText).Text;

            try
            {
                string ipaddress = new WebClient().DownloadString(queryAddress).Replace("\\r\\n", "").Replace("\\n", "").Trim();

                IPAddress dummy;
                if (!IPAddress.TryParse(ipaddress, out dummy))
                    throw new Exception();//cheap shortcut to "failed" scenario

                (View["PublicIP"] as HudTextBox).Text = ipaddress;
            }
            catch
            {
                (View["PublicIP"] as HudTextBox).Text = "-failed-";
            }
        }

        private bool LogOff = false;
        private void _CharacterFilter_Logoff(object sender, LogoffEventArgs e)
        {
            if (LogOff)
                return;

            Log("LOGOFF LOL");


            LogOff = true;


            using (INIFile ini = INIFile)
            {
                ini.WriteKey(Core.CharacterFilter.AccountName, "Enable", (View["Enable"] as HudCheckBox).Checked ? "1" : "0");
                ini.WriteKey(Core.CharacterFilter.AccountName, "Advertise", (View["Advertise"] as HudCheckBox).Checked ? "1" : "0");

                HudCombo determineIPCombo = View["DetermineIP"] as HudCombo;
                ini.WriteKey(Core.CharacterFilter.AccountName, "DetermineIP", determineIPCombo.Current.ToString());
                if (determineIPCombo.Current == 0)
                    ini.WriteKey(Core.CharacterFilter.AccountName, "ManualPublicIP", (View["PublicIP"] as HudTextBox).Text);
                else
                    ini.WriteKey(Core.CharacterFilter.AccountName, "ManualPublicIP", string.Empty);
            }

        }

        byte[] ServerIP
        {
            get
            {
                try
                {
                    string ipstr = (View["PublicIP"] as HudTextBox).Text;

                    string[] ipparts = ipstr.Split('.');
                    if (ipparts.Length != 4)
                        return null;

                    byte[] b = new byte[4];
                    for (int x = 0; x < 4; x++)
                        b[x] = byte.Parse(ipparts[x]);
                    return b;
                }
                catch
                {
                    return null;
                }
            }
        }

        public WorldObject Player
        {
            get
            {
                return Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First;
            }
        }

        public INIFile INIFile
        {
            get
            {
                return new INIFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ACAVCServer.ini"));
            }
        }

        private long LoginCompleteTimestamp = 0;
        public double WorldTime
        {
            get
            {
                if (LoginCompleteTimestamp == 0)
                {
                    //Log("logincompletetimestamp is 0 so the answer is no");
                    return 0.0;
                }

                return PerfTimer.TimeBetween(LoginCompleteTimestamp, PerfTimer.Timestamp);
            }
        }

        long lastTimestamp = 0;
        private void _Process(object sender, EventArgs e)
        {
            if (lastTimestamp == 0)
            {
                lastTimestamp = PerfTimer.Timestamp;
                return;
            }

            long curtimestamp = PerfTimer.Timestamp;
            double truedt = PerfTimer.TimeBetween(lastTimestamp, curtimestamp);
            if (truedt <= 0.0)
                return;

            double dt;
            if (truedt > (1.0 / 20.0))
                dt = 1.0 / 20.0;
            else
                dt = truedt;
            lastTimestamp = curtimestamp;

            try
            {
                Process(dt, truedt);
            }
            catch (Exception ex)
            {
                Log($"Process exception: {ex.Message}");
            }
        }

        class PlayerJoinAttempt
        {
            public readonly string PlayerName;
            public readonly bool Silent;
            public readonly DateTime Start = DateTime.Now;

            public const int TimeoutMsec = 2000;

            public PlayerJoinAttempt(string _PlayerName, bool _Silent)
            {
                PlayerName = _PlayerName;
                Silent = _Silent;
            }

            public void Tell(TellPacket p)
            {
                BotTell(PlayerName, p.GenerateString());
            }
        }

        List<PlayerJoinAttempt> PlayerJoinAttempts = new List<PlayerJoinAttempt>();

        PlayerJoinAttempt GetPlayerJoinAttempt(string playerName)
        {
            foreach (PlayerJoinAttempt pja in PlayerJoinAttempts)
                if (pja.PlayerName == playerName)
                    return pja;

            return null;
        }

        PlayerJoinAttempt GetOrCreatePlayerJoinAttempt(string playerName, bool silent)
        {
            PlayerJoinAttempt pja = GetPlayerJoinAttempt(playerName);
            if (pja != null)
                return pja;

            pja = new PlayerJoinAttempt(playerName, silent);
            PlayerJoinAttempts.Add(pja);
            return pja;
        }

        void DestroyPlayerJoinAttempt(string playerName)
        {
            PlayerJoinAttempt pja = GetPlayerJoinAttempt(playerName);
            if (pja == null)
                return;

            PlayerJoinAttempts.Remove(pja);
        }

        public static bool ShowTellProtocol = false;

        private void _ChatBoxMessage(object sender, ChatTextInterceptEventArgs e)
        {
            if (!EnableServer)
                return;

            //Log($"RAWCHAT ({e.Target}): |{e.Text}|");



            //8:28:24 PM: RAWCHAT (0): |You tell Strike Test, "ACA*333133p"
            if (e.Text.Contains("You tell"))
            {
                int i = e.Text.IndexOf(',');
                if (i != -1)
                {
                    string content = e.Text.Substring(i + 3);
                    if (content.StartsWith(TellPacket.prefix))
                    {
                        if (!ShowTellProtocol)
                        {
                            // dont show to user if its good
                            e.Eat = true;
                            return;
                        }
                    }
                }
            }



            ACAUtils.ChatMessage cm = ACAUtils.InterpretChatMessage(e.Text);
            if (cm == null)
                return;


            //Log($"CHAT ({e.Target}): [{cm.Channel}][{cm.ID.ToString("X8")}][{cm.PlayerName}][{cm.Mode}]:[{cm.Content}]");


            if (cm.Mode == "tells you")
            {

                if (cm.Content.Equals("help", StringComparison.InvariantCultureIgnoreCase))
                {
                    BotTell(cm.PlayerName, "I am an ACAudio Voice Chat Server.");
                    BotTell(cm.PlayerName, "Tell me 'join' to connect!");
                    BotTell(cm.PlayerName, "The ACAudio Decal plugin is available here:");
                    BotTell(cm.PlayerName, "https://acaudio.bah.wtf");

                }
                else
                    if (cm.Content.Equals("join", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (ServerIP == null)
                        BotTell(cm.PlayerName, "Sorry, my server isn't set up properly at this time.");
                    else
                    {
                        // initiate a join attempt if they /tell us "join"
                        PlayerJoinAttempt pja = GetOrCreatePlayerJoinAttempt(cm.PlayerName, false);
                        TellPacket p = new TellPacket(TellPacket.MessageType.RequestInfo);
                        pja.Tell(p);
                    }
                }
                else
                {
                    if (!ShowTellProtocol && cm.Content.StartsWith(TellPacket.prefix))
                        // dont show to user if its good
                        e.Eat = true;

                    // no recognized tell text;  assume its a packet and try
                    TellPacket clientPacket = TellPacket.FromString(cm.Content);
                    if (clientPacket == null)
                    {
                        if (!cm.Content.StartsWith(TellPacket.prefix))
                            BotTell(cm.PlayerName, "I didn't understand that. Try 'help' to see what I recognize.");
                    }
                    else
                    {
                        PlayerJoinAttempt pja = GetPlayerJoinAttempt(cm.PlayerName);
                        if (pja == null)
                        {
                            if (clientPacket.Message == TellPacket.MessageType.Join)
                            {
                                if (ServerIP == null)
                                {

                                }
                                else
                                {
                                    // initiate a join attempt if they send us a join packet
                                    pja = GetOrCreatePlayerJoinAttempt(cm.PlayerName, true);
                                    TellPacket p = new TellPacket(TellPacket.MessageType.RequestInfo);
                                    pja.Tell(p);
                                }
                            }
                            else
                            {
                                if (!pja.Silent)
                                    BotTell(cm.PlayerName, "Something went wrong. Please try to 'join' again.");
                                //BotTell(cm.PlayerName, "Don't give me ACA protocols unless we are handshaking.");
                            }
                        }
                        else
                        {
                            if (clientPacket.Message == TellPacket.MessageType.ClientInfo)
                            {
                                string characterName = clientPacket.ReadString();
                                int weenieID = clientPacket.ReadInt();

                                if (pja.PlayerName != characterName ||
                                    cm.ID != weenieID)
                                {
                                    //BotTell(cm.PlayerName, "Your join request doesn't match.");
                                }
                                else
                                {
                                    // success; send server info

                                    byte[] ip = ServerIP;
                                    if (ip != null)
                                    {
                                        TellPacket serverInfo = new TellPacket(TellPacket.MessageType.ServerInfo);
                                        serverInfo.WriteByte(ip[0]);
                                        serverInfo.WriteByte(ip[1]);
                                        serverInfo.WriteByte(ip[2]);
                                        serverInfo.WriteByte(ip[3]);
                                        serverInfo.WriteBits(42420, 16);

                                        pja.Tell(serverInfo);

                                        if (!pja.Silent)
                                            BotTell(cm.PlayerName, "Success!");
                                    }
                                }
                            }
                            else
                            {
                                //BotTell(cm.PlayerName, "I wasn't expecting that message type.");
                            }

                            // kill the attempt after receiving any message
                            DestroyPlayerJoinAttempt(cm.PlayerName);
                        }
                    }

                }
            }
        }

        static void BotChat(string s)
        {
            _QueueChat($"/e says, \"{s}\" -b-");
        }

        static void BotTell(string targetName, string s)
        {
            _QueueChat($"/tell {targetName},{s}");
        }


        static CritSect PendingChatsCrit = new CritSect();
        static List<string> PendingChats = new List<string>();

        static void _QueueChat(string s)
        {
            using (PendingChatsCrit.Lock)
            {
                while (PendingChats.Count > 100)
                    PendingChats.RemoveAt(0);

                PendingChats.Add(s);
            }
        }

        static void _DispatchChatSingle()
        {
            string outgoing = null;
            using (PendingChatsCrit.Lock)
            {
                if (PendingChats.Count > 0)
                {
                    outgoing = PendingChats[0];
                    PendingChats.RemoveAt(0);
                }
            }

            if (!string.IsNullOrEmpty(outgoing))
            {
                lastDispatchChat = DateTime.Now;
                CoreManager.Current.Actions.InvokeChatParser(outgoing);
            }
        }

        public bool EnableServer
        {
            get
            {
                return (View["Enable"] as HudCheckBox).Checked;
            }
        }

        public bool EnableAvertisement
        {
            get
            {
                try
                {
                    return (View["Advertise"] as HudCheckBox).Checked;
                }
                catch
                {
                    return false;
                }
            }
        }

        private DateTime lastAdvertisement = new DateTime();

        private void Process(double dt, double truedt)
        {
            // if we havent finished logging in, just dont do anything
            if (NeedFirstLoginPlayerWeenie)
                return;



            bool enableServer = EnableServer;//precache

            if (!enableServer)
            {
                // server should not be running. do nothing here
                Server.Shutdown();
            }
            else
            if (!Server.IsRunning)
            {
                try
                {
                    Server.Init();

                    lastAdvertisement = new DateTime();
                }
                catch (Exception ex)
                {
                    Log($"server init exception: {ex.Message}");
                }
            }




            if (enableServer && Server.IsRunning && EnableAvertisement)
            {
                if (lastAdvertisement == new DateTime())
                {
                    BotChat("ACAudio Voice Chat Server is Online!");

                    lastAdvertisement = DateTime.Now;
                }
                else if (DateTime.Now.Subtract(lastAdvertisement).TotalMinutes >= (5.0 + MathLib.random.Next(10)))
                {
                    BotChat("I am an ACAudio Voice Chat Server. Tell me 'help' to learn more.");

                    lastAdvertisement = DateTime.Now;
                }
            }


            // time-out join attempts
            for (int x = 0; x < PlayerJoinAttempts.Count; x++)
            {
                PlayerJoinAttempt pja = PlayerJoinAttempts[x];

                if (DateTime.Now.Subtract(pja.Start).TotalMilliseconds >= PlayerJoinAttempt.TimeoutMsec)
                {
                    if (!pja.Silent)
                        BotTell(pja.PlayerName, "Your join attempt timed-out.");

                    PlayerJoinAttempts.RemoveAt(x--);
                }
            }


            updateUITime -= dt;
            if (updateUITime <= 0.0)
            {
                if (!enableServer)
                    metrics = new Metrics();// clear metrics if server disabled
                else
                    metrics.Preprocess();


                (View["Status"] as HudStaticText).Text =
                    $"Players:{Server.GetPlayers().Length}  TotalConnectAttempts:{metrics.totalIncomingConnectionsCount}" +
                    $"\nPacketsReceived:{metrics.totalPacketsReceivedCount} ({bytesizestring(metrics.totalPacketsReceivedBytes)})    PacketsSent:{metrics.totalPacketsSentCount} ({bytesizestring(metrics.totalPacketsSentBytes)})  " +
                    $"\nnumRums:{metrics.numRuns}  slowRuns:{metrics.slowRuns}   maxRun:{(int)(metrics.maxRunTime * 1000)}msec  avgRun:{(int)(metrics.avgRunTime * 1000)}msec";



                //bargraph(" CPU", avgRunTime / slowRunTime, $"{(int)(slowRunTime * 1000.0)}msec", barWidth);
                (View["CPUMeter"] as HudProgressBar).Position = (int)(metrics.avgRunTime / Metrics.slowRunTime * 100.0);
                (View["CPUPostfix"] as HudStaticText).Text = $"{(int)(Metrics.slowRunTime * 1000.0)}msec";

                double secPerTick = (double)Metrics.sleepMsec / 1000.0;
                int expectedReceiveBytesPerSlowRun = (int)(Metrics.expectedSpeakingPlayers * Server.CurrentStreamInfo.DetermineExpectedBytes((int)(Metrics.slowRunTime * 1000.0)) / secPerTick);
                //bargraph("RECV", (double)avgReceivedBytes / (double)expectedReceiveBytesPerSlowRun / secPerTick, $"{bytesizestring((ulong)expectedReceiveBytesPerSlowRun)}/sec", barWidth);
                (View["RECVMeter"] as HudProgressBar).Position = (int)((double)metrics.avgReceivedBytes / (double)expectedReceiveBytesPerSlowRun / secPerTick * 100.0);
                (View["RECVPostfix"] as HudStaticText).Text = $"{bytesizestring((ulong)expectedReceiveBytesPerSlowRun)}/sec";

                int expectedSendBytesPerSlowRun = Metrics.expectedListenersPerPlayer * expectedReceiveBytesPerSlowRun;
                //bargraph("SEND", (double)avgSentBytes / (double)expectedSendBytesPerSlowRun / secPerTick, $"{bytesizestring((ulong)expectedSendBytesPerSlowRun)}/sec", barWidth);
                (View["SENDMeter"] as HudProgressBar).Position = (int)((double)metrics.avgSentBytes / (double)expectedSendBytesPerSlowRun / secPerTick * 100.0);
                (View["SENDPostfix"] as HudStaticText).Text = $"{bytesizestring((ulong)expectedSendBytesPerSlowRun)}/sec";

                updateUITime += secPerTick;
            }


            if (DateTime.Now.Subtract(lastDispatchChat).TotalMilliseconds > 250)
                _DispatchChatSingle();
        }

        double updateUITime = 0.0;

        static string bytesizestring(ulong bytes)
        {
            const int kb = 1024;
            const int mb = kb * 1024;
            const int gb = mb * 1024;

            if (bytes < mb)
                return $"{bytes / kb}kb";
            else if (bytes < gb)
                return $"{((double)bytes / (double)mb).ToString("#0.0")}mb";
            else
                return $"{((double)bytes / (double)gb).ToString("#0.0")}gb";
        }

        private static DateTime lastDispatchChat = new DateTime();

        public bool NeedFirstLoginPlayerWeenie = true;

        [BaseEvent("LoginComplete", "CharacterFilter")]
        private void CharacterFilter_LoginComplete(object sender, EventArgs e)
        {
            WriteToChat($"Startup");

            // even though we're logged in, we havent spawned in-game yet.  set flag to do logic upon first gamespawn
            NeedFirstLoginPlayerWeenie = true;


            // start our world timer
            LoginCompleteTimestamp = PerfTimer.Timestamp;
        }

        [BaseEvent("ChangePortalMode", "CharacterFilter")]
        private void CharacterFilter_ChangePortalMode(object sender, ChangePortalModeEventArgs e)
        {
            if (e.Type == PortalEventType.ExitPortal)
            {


                if (NeedFirstLoginPlayerWeenie)
                {
                    NeedFirstLoginPlayerWeenie = false;


                }
            }
        }


        /// <summary>
        /// This is called when the plugin is shut down. This happens only once.
        /// </summary>
        protected override void Shutdown()
        {
            //Log("unhook stuff");
            Core.ChatBoxMessage -= _ChatBoxMessage;
            Core.CharacterFilter.Logoff -= _CharacterFilter_Logoff;
            Core.RenderFrame -= _Process;


            Server.Shutdown();


            Log("----------------------------------------------------------------------");
            Log("                       ACAVCServer Shutdown");
            Log("----------------------------------------------------------------------");
        }

        private void FilterCore_CommandLineText(object sender, ChatParserInterceptEventArgs e)
        {
            /*if (false)
            {

                //Do not execute as AC command.
                e.Eat = true;
            }*/

        }

        public static Stream GetEmbeddedFile(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream($"{PluginName}.{name}");
        }

        public static void WriteToChat(string message)
        {
            try
            {
                Instance.Host.Actions.AddChatText($"[{PluginName}] {message}", 5);
                Log($"WriteToChat: {message}");
            }
            catch (Exception)
            {

            }
        }

        private static string FinalLogFilepath
        {
            get
            {
                return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $"{PluginName}.log");
            }
        }

        private static CritSect LogCrit = new CritSect();
        public static void Log(string ln)
        {
            using (LogCrit.Lock)
            {
                try
                {
                    using (StreamWriter logFile = File.AppendText(FinalLogFilepath))
                        logFile.WriteLine($"{DateTime.Now.ToLongTimeString()}: {ln}");
                }
                catch
                {

                }
            }
        }


        [DllImport("Decal.dll")]
        static extern int DispatchOnChatCommand(ref IntPtr str, [MarshalAs(UnmanagedType.U4)] int target);

        static bool Decal_DispatchOnChatCommand(string cmd)
        {
            IntPtr bstr = Marshal.StringToBSTR(cmd);

            try
            {
                bool eaten = (DispatchOnChatCommand(ref bstr, 1) & 0x1) > 0;

                return eaten;
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }

        /// <summary>
        /// This will first attempt to send the messages to all plugins. If no plugins set e.Eat to true on the message, it will then simply call InvokeChatParser.
        /// </summary>
        /// <param name="cmd"></param>
        public static void DispatchChatToBoxWithPluginIntercept(string cmd)
        {
            if (string.IsNullOrEmpty(cmd))
                return;

            if (!Decal_DispatchOnChatCommand(cmd))
                CoreManager.Current.Actions.InvokeChatParser(cmd);
        }

    }
}
