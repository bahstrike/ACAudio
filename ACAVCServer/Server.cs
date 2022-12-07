﻿// main server.  all logic is handled via child threads.

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using ACACommon;
using Smith;

namespace ACAVCServer
{
    public static class Server
    {
        private static ListenServer listener = null;
        private static ClientProcessor clientProcessor = null;

        public delegate void LogDelegate(string s);
        public static LogDelegate LogCallback = null;

        public static volatile bool ShowPlayerIPAndAccountInLogs = false;

        public static volatile int IncomingConnectionsCount = 0;
        public static volatile int PacketsReceivedCount = 0;
        public static volatile uint PacketsReceivedBytes = 0;//should be reset to 0 by whoever is scraping the value to prevent overflow
        public static volatile int PacketsSentCount = 0;
        public static volatile uint PacketsSentBytes = 0;//should be reset to 0 by whoever is scraping the value to prevent overflow

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
        private static StreamInfo _CurrentStreamInfo = null;
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
    }
}