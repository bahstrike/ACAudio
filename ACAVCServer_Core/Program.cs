namespace ACAVCServer_Core
{
    internal class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            Console.WriteLine("Init");
            Server.Init();

            while (!Console.KeyAvailable)
                System.Threading.Thread.Sleep(1);


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