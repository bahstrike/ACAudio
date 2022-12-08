using System;
using System.Collections.Generic;
using System.Text;

namespace ACACommon
{
    public class StreamInfo
    {
        public readonly int magic;
        public bool ulaw;
        public int bitDepth;
        public int sampleRate;

        // could be incorporated to protocol but we're just sticking some common constants here
        public const double PlayerMinDist = 15.0;
        public const double PlayerMaxDist = 50.0;
        public const int DesiredAudioChunkMsec = 100;// shrug

        public const int InvalidAllegianceID = 0;
        public const int InvalidFellowshipID = 0;

        public enum VoiceChannel
        {
            Invalid = -1,
            Proximity3D,
            Allegiance,
            Fellowship
        }

        private static Random random = new Random((int)DateTime.Now.ToBinary());

        private StreamInfo()
        {
            
        }

        public StreamInfo(int _magic, bool _ulaw, int _bitDepth, int _sampleRate)
        {
            magic = _magic;
            ulaw = _ulaw;
            bitDepth = _bitDepth;
            sampleRate = _sampleRate;
        }

        public StreamInfo(bool _ulaw, int _bitDepth, int _sampleRate)
        {
            magic = random.Next();
            ulaw = _ulaw;
            bitDepth = _bitDepth;
            sampleRate = _sampleRate;
        }

        public static StreamInfo FromPacket(Packet p)
        {
            int magic = p.ReadInt();
            bool ulaw = p.ReadBool();
            int bitDepth = p.ReadInt();
            int sampleRate = p.ReadInt();

            return new StreamInfo(magic, ulaw, bitDepth, sampleRate);
        }

        public override string ToString()
        {
            return $"[{magic.ToString("X8")}]   ulaw:{ulaw}   bitDepth:{bitDepth}   sampleRate:{sampleRate}";
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
}
