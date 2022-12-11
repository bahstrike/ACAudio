// Welcome to the ACAudio Voice Chat Server console application!
//
//
// This code really doesn't do much other than display real-time server metrics.
//
//
// In fact, all you need to host the server from ANY codebase is call Server.Start()
// and Server.Stop(). The server operates via two internal threads.
//
//
// You may wish to set Server.CurrentStreamInfo to adjust bandwidth VS quality.
// This can be updated at any time and will synchronize out to clients.
//
// Provide a callback to Server.LogCallback if you want logging.
// Note this is called from threads and you are responsible for synchronization.
//
// Provide a callback to Server.CheckPlayerCallback if you want the ability to
// reject incoming player connections (eg. if they are not actually online in
// your AC server, or if they are banned, etc).
// Note this is called from threads and you are responsible for synchronization.
//
// Call Server.GetPlayers() to get a list of all connected clients (thread-safe).
// You can kick a player from the server by setting Player.WantDisconnectReason to
// a valid string.
//
// The remaining members of Server are mainly about performance metrics and serve
// no functional purpose.

using System;
using System.Collections.Generic;
using ACAVCServer;

namespace ACAVCServerConsole
{
    internal class Program
    {
        static string genchars(char c, int num)
        {
            string s = string.Empty;
            for (int x = 0; x < num; x++)
                s += c;
            return s;
        }

        static void WriteLine()
        {
            WriteLine(string.Empty);
        }
        static void WriteLine(string s)
        {
            if (s.Length >= Console.WindowWidth)
                s = s.Substring(0, Console.WindowWidth-1);

            Console.WriteLine(s + genchars(' ', Console.WindowWidth - s.Length-1));
        }

        static void bargraph(string label, double v, string valMax, int barWidthChars = 30)
        {
            int fillBars = Math.Min(barWidthChars, (int)(v * (double)barWidthChars));
            label = $"{label}: ";
            WriteLine($"{genchars(' ', label.Length+1)}{genchars('-', barWidthChars)}");
            WriteLine($"{label}|{genchars('X', fillBars)}{genchars(' ', barWidthChars - fillBars)}|  {valMax}");
            WriteLine($"{genchars(' ', label.Length+1)}{genchars('-', barWidthChars)}");
        }

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

        static List<string> PendingLogMessages = new List<string>();

        static void LogCallback(string s)
        {
            lock(PendingLogMessages)
                PendingLogMessages.Add(s);
        }

        static List<string> LogMessages = new List<string>();

        static string CheckPlayerCallback(Player player)
        {
            // example pseudocode
            /*using(criticalsection.Lock)
            {
                if (!IsValidPlayerWeenie(player.WeenieID))
                    return "You are not in-game";

                if (IsBanned(player.AccountName))
                    return "You are banned";
            }*/

            return null;// allow connection
        }

        static void Main(string[] args)
        {
#if false
            while (!Console.KeyAvailable) ;
            Console.ReadKey();
#endif

            Console.Title = "ACAudio Voice Chat Server";

            // hook process exit so we can try a proper shutdown if console window is manually closed
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            // declare our local performance metric vars and whatnot
            uint totalIncomingConnectionsCount = 0;
            uint totalPacketsReceivedCount = 0;
            ulong totalPacketsReceivedBytes = 0;
            uint totalPacketsSentCount = 0;
            ulong totalPacketsSentBytes = 0;
            ulong numRuns = 0;
            ulong slowRuns = 0;
            double maxRunTime = 0.0;
            double avgRunTime = 0.0;
            int avgSentBytes = 0;
            int avgReceivedBytes = 0;

            int lastWidth = Console.WindowWidth;
            int lastHeight = Console.WindowHeight;

            // assign optional server callbacks
            Server.LogCallback = LogCallback;
            Server.CheckPlayerCallback = CheckPlayerCallback;
            Server.ShowPlayerIPAndAccountInLogs = false;

            // start the server!
            Server.Start();

            // loop until we want quit
            for(bool running=true; running; )
            {
                // check for quit
                while(Console.KeyAvailable)
                {
                    if(Console.ReadKey(true).Key == ConsoleKey.Escape)
                    {
                        running = false;
                        break;
                    }
                }

                // clear screen if changing sizes to eliminate leftover characters
                if(lastWidth != Console.WindowWidth || lastHeight != Console.WindowHeight)
                {
                    Console.Clear();

                    lastWidth = Console.WindowWidth;
                    lastHeight = Console.WindowHeight;
                }

                // grab pending log messages into local buffer and maintain size
                lock(PendingLogMessages)
                {
                    LogMessages.AddRange(PendingLogMessages);
                    PendingLogMessages.Clear();
                }
                int possibleLogsToShow = Math.Max(0, Console.WindowHeight - 20/*3 for header, 14 for top section plus 2 for "press any key" plus 1 extra space*/);
                while (LogMessages.Count > Math.Max(100, possibleLogsToShow))
                    LogMessages.RemoveAt(0);


                // retrieve / preprocess performance metrics
                double slowRunTime = ((double)100/*anticipate 100msec for an audio chunk?*/ / 1000.0) * 0.2/*lets set our bar lower than bare minimum that client might expect*/;
                totalIncomingConnectionsCount += (uint)Server.IncomingConnectionsCount;
                totalPacketsReceivedCount += (uint)Server.PacketsReceivedCount;
                totalPacketsReceivedBytes += Server.PacketsReceivedBytes;
                totalPacketsSentCount += (uint)Server.PacketsSentCount;
                totalPacketsSentBytes += Server.PacketsSentBytes;
                avgSentBytes = (int)(avgSentBytes + Server.PacketsSentBytes) / 2;
                avgReceivedBytes = (int)(avgReceivedBytes + Server.PacketsReceivedBytes) / 2;
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



                // draw info at top of screen
                Console.SetCursorPosition(0, 0);
                WriteLine("ACAudio Voice Chat Server 1.0 - BAH 2022");
                WriteLine(genchars('-', Console.WindowWidth-1));
                WriteLine();

                WriteLine($"Players:{Server.GetPlayers().Length}  TotalConnectAttempts:{totalIncomingConnectionsCount}");
                WriteLine($"PacketsSent:{totalPacketsSentCount} ({bytesizestring(totalPacketsSentBytes)})  PacketsReceived:{totalPacketsReceivedCount} ({bytesizestring(totalPacketsReceivedBytes)})");
                WriteLine($"numRums:{numRuns}  slowRuns:{slowRuns}   maxRun:{(int)(maxRunTime*1000)}msec  avgRun:{(int)(avgRunTime*1000)}msec");

                WriteLine();


                // draw performance bar graphs
                int barWidth = Console.WindowWidth * 70 / 100;

                bargraph(" CPU", avgRunTime/slowRunTime, $"{(int)(slowRunTime*1000.0)}msec", barWidth);

                int expectedReceiveBytesPerSlowRun = 40/*i dunno some number to make it look good*/ * Server.CurrentStreamInfo.DetermineExpectedBytes((int)(slowRunTime * 1000.0));
                bargraph("RECV", (double)avgReceivedBytes / (double)expectedReceiveBytesPerSlowRun, $"{bytesizestring((ulong)expectedReceiveBytesPerSlowRun)}/sec", barWidth);

                int expectedSendBytesPerSlowRun = 5/*i dunno some number of expected listeners for each speaker*/ * expectedReceiveBytesPerSlowRun;
                bargraph("SEND", (double)avgSentBytes / (double)expectedSendBytesPerSlowRun, $"{bytesizestring((ulong)expectedSendBytesPerSlowRun)}/sec", barWidth);



                // draw recent log messages
                WriteLine();
                int logsToShow = Math.Min(possibleLogsToShow, LogMessages.Count);
                for (int x = 0; x < possibleLogsToShow - logsToShow; x++)
                    WriteLine("|");
                for (int x = logsToShow; x > 0; x--)
                    WriteLine("| " + LogMessages[LogMessages.Count - x]);



                // menu
                WriteLine();
                WriteLine("<Press ESCAPE to stop server>");



                // reset accumulative counters since we've scraped the values for this console UI "tick"
                Server.IncomingConnectionsCount = 0;
                Server.PacketsReceivedCount = 0;
                Server.PacketsReceivedBytes = 0;
                Server.PacketsSentCount = 0;
                Server.PacketsSentBytes = 0;


                System.Threading.Thread.Sleep(50);
            }


            // disconnect clients and stop threads for a clean exit
            Server.Stop();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            // try to perform a proper shutdown even if we were manually closed
            Server.Stop();
        }
    }
}