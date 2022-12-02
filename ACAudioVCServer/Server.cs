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
        }

        public class StreamInfo
        {
            public readonly int magic;
            public bool ulaw;
            public int bitDepth;
            public int sampleRate;

            private StreamInfo()
            {

            }

            public StreamInfo(bool _ulaw, int _bitDepth, int _sampleRate)
            {
                magic = Smith.MathLib.random.Next();
                ulaw = _ulaw;
                bitDepth = _bitDepth;
                sampleRate = _sampleRate;
            }

            public static bool CompareProperties(StreamInfo a, StreamInfo b)
            {
                // cant match if one is null
                if (a == null || b == null)
                    return false;

                return (a.ulaw == b.ulaw &&
                    a.bitDepth == b.bitDepth &&
                    a.sampleRate == b.sampleRate);
            }
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
    }
}
