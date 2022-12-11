using System;

namespace ACAVCServer
{
    /// <summary>
    /// Represents a voice codec.
    /// </summary>
    public class StreamInfo
    {
        internal readonly int magic;

        /// <summary>
        /// Whether to use µ-law compression. Cuts bandwidth in half for 16-bit. Do not use for 8-bit (results in no savings).
        /// </summary>
        public readonly bool ulaw;

        /// <summary>
        /// Sample bit depth;  either 8 or 16.
        /// </summary>
        public readonly int bitDepth;

        /// <summary>
        /// Sample rate; typical values are 8000, 11025, 22050, 44100.
        /// </summary>
        public readonly int sampleRate;

        // could be incorporated to protocol but we're just sticking some common constants here
        internal const double PlayerMinDist = 15.0;
        internal const double PlayerMaxDist = 50.0;

        internal enum VoiceChannel
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

        internal StreamInfo(int _magic, bool _ulaw, int _bitDepth, int _sampleRate)
        {
            magic = _magic;
            ulaw = _ulaw;
            bitDepth = _bitDepth;
            sampleRate = _sampleRate;
        }

        /// <summary>
        /// Defines the a voice codec.
        /// </summary>
        /// <param name="_ulaw">Whether to use µ-law compression (only for 16-bit; cuts bandwidth in half)</param>
        /// <param name="_bitDepth">Either 8 or 16</param>
        /// <param name="_sampleRate">Typical values are 8000, 11025, 22050, 44100</param>
        public StreamInfo(bool _ulaw, int _bitDepth, int _sampleRate)
        {
            magic = random.Next();
            ulaw = _ulaw;
            bitDepth = _bitDepth;
            sampleRate = _sampleRate;
        }

        /// <summary>
        /// Calculate how many bytes this voice codec will require for a particular length audio fragment.
        /// </summary>
        /// <param name="msec">Length of audio fragment, in milliseconds</param>
        /// <returns></returns>
        public int DetermineExpectedBytes(int msec)
        {
            return (bitDepth / 8 * msec * sampleRate / ((ulaw&bitDepth==16) ? 2 : 1))/1000;
        }

        internal static StreamInfo FromPacket(Packet p)
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

        internal static bool CompareProperties(StreamInfo a, StreamInfo b)
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
