using ACAVCServerLib;

namespace ACAVCServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            while (!Console.KeyAvailable) ;
            Console.ReadKey();



            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            ulong numRuns = 0;
            ulong slowRuns = 0;
            double maxRunTime = 0.0;
            double avgRunTime = 0.0;


            Console.WriteLine("Init");
            Server.Init();

            while (!Console.KeyAvailable)
            {
                double[] runTimes = Server.CollectRunTimes();
                if (runTimes.Length > 0)
                {
                    numRuns += (ulong)runTimes.Length;

                    double avg = 0.0;
                    foreach (double tm in runTimes)
                    {
                        maxRunTime = Math.Max(maxRunTime, tm);
                        avg += tm;

                        if (tm > 0.01)
                            slowRuns++;
                    }
                    avg /= (double)runTimes.Length;

                    avgRunTime = (avgRunTime + avg) / 2.0;
                }

                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"Players:{Server.GetPlayers().Length}  TotalConnectAttempts:{Server.IncomingConnectionsCount}");
                Console.WriteLine($"PacketsSent:{Server.PacketsSentCount} ({Server.PacketsSentBytes / 1024}kb)  PacketsReceived:{Server.PacketsReceivedCount} ({Server.PacketsReceivedBytes / 1024}kb)");
                Console.WriteLine($"numRums:{numRuns}  slowRuns:{slowRuns}   maxRun:{maxRunTime.ToString("#0.000")}  avgRun:{avgRunTime.ToString("#0.000")}");

                Console.WriteLine();
                Console.WriteLine("<Press any key to stop server>");


                System.Threading.Thread.Sleep(1);
            }


            Console.Write("Shutdown");
            Server.Shutdown();
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            // try to perform a proper shutdown even if we were manually closed
            Server.Shutdown();
        }
    }
}