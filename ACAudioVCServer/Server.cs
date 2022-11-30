// main server.  all logic is handled via child threads.

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace ACAudioVCServer
{
    public static class Server
    {
        private static ListenServer listener = null;
        private static ClientProcessor clientProcessor = null;

        public delegate void LogDelegate(string s);
        public static LogDelegate LogCallback = null;

        public static void Log(string s)
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
            if (clientProcessor != null)
            {
                clientProcessor.Stop();
                clientProcessor = null;
            }

            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }
        }
    }
}
