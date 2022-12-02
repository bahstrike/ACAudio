using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml;

namespace ACAVoiceClient
{
    public static class Utils
    {
        private class SortNewest : IComparer<DateTime>
        {
            public int Compare(DateTime x, DateTime y)
            {
                return y.CompareTo(x);
            }
        }

        public static string DetectServerAddressViaThwargle(string accountName)
        {
            try
            {
                string thwargPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ThwargLauncher");
                if (!Directory.Exists(thwargPath))
                    return null;

                // check "Running" logs for most recent that matches provided account name
                SortedList<DateTime, string> newestGames = new SortedList<DateTime, string>(new SortNewest());
                foreach (string gameFile in Directory.GetFiles(Path.Combine(thwargPath, "Running"), "*.txt"))
                {
                    DateTime fileTime = new FileInfo(gameFile).LastWriteTime;

                    // skip if not today
                    if (DateTime.Now.Subtract(fileTime).TotalDays > 1)
                        continue;

                    newestGames.Add(fileTime, gameFile);
                }

                // scan most recent files for matching account name and try to scrape server name
                string serverName = null;
                foreach (DateTime fileTime in newestGames.Keys)
                {
                    string gameFile = newestGames[fileTime];

                    using (StreamReader sr = File.OpenText(gameFile))
                    {
                        string fileAccountName = null;
                        string fileServerName = null;
                        while (!sr.EndOfStream && (fileAccountName == null || fileServerName == null))
                        {
                            string ln = sr.ReadLine();
                            if (ln.StartsWith("AccountName:"))
                                fileAccountName = ln.Substring(ln.IndexOf(':') + 1);
                            else if (ln.StartsWith("ServerName:"))
                                fileServerName = ln.Substring(ln.IndexOf(':') + 1);
                        }

                        if (fileAccountName == null || fileAccountName != accountName)
                            continue;

                        serverName = fileServerName;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(serverName))
                    return null;

                // now we need to load all the server XMLs and try to find a match that includes a host address
                foreach (string xmlFile in Directory.GetFiles(Path.Combine(thwargPath, "Servers"), "*.xml"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(xmlFile);

                    foreach (XmlNode serverNode in xmlDoc.DocumentElement.SelectNodes("ServerItem"))
                    {
                        XmlNode node;

                        node = serverNode.SelectSingleNode("name");
                        if (node == null || node.InnerText != serverName)
                            continue;

                        node = serverNode.SelectSingleNode("connect_string");
                        if (node == null)
                            continue;

                        // strip port out
                        string connectString = node.InnerText;
                        int i = connectString.IndexOf(':');
                        if (i != -1)
                            connectString = connectString.Substring(0, i);

                        // we found it
                        return connectString;
                    }
                }
            }
            catch
            {
                // it failed for some dumb reason. oh well.
            }

            return null;
        }

    }
}

// THE WinSound NAMESPACE IS FROM: https://www.codeproject.com/Articles/482735/TCP-Audio-Streamer-and-Player-Voice-Chat-over-IP
namespace WinSound
{
    /// <summary>
    /// Utils
    /// </summary>
    public class Utils
    {
        /// <summary>
        /// Konstruktor
        /// </summary>
        public Utils()
        {

        }

        //Konstanten
        const int SIGN_BIT = (0x80);
        const int QUANT_MASK = (0xf);
        const int NSEGS = (8);
        const int SEG_SHIFT = (4);
        const int SEG_MASK = (0x70);
        const int BIAS = (0x84);
        const int CLIP = 8159;
        static short[] seg_uend = new short[] { 0x3F, 0x7F, 0xFF, 0x1FF, 0x3FF, 0x7FF, 0xFFF, 0x1FFF };

        /// <summary>
        /// GetBytesPerInterval
        /// </summary>
        /// <param name="header"></param>
        /// <returns></returns>
        public static int GetBytesPerInterval(uint SamplesPerSecond, int BitsPerSample, int Channels)
        {
            int blockAlign = ((BitsPerSample * Channels) >> 3);
            int bytesPerSec = (int)(blockAlign * SamplesPerSecond);
            uint sleepIntervalFactor = 1000 / 20; //20 Milliseconds
            int bytesPerInterval = (int)(bytesPerSec / sleepIntervalFactor);

            //Fertig
            return bytesPerInterval;
        }
        /// <summary>
        /// MulawToLinear
        /// </summary>
        /// <param name="ulaw"></param>
        /// <returns></returns>
        public static Int32 MulawToLinear(Int32 ulaw)
        {
            ulaw = ~ulaw;
            int t = ((ulaw & QUANT_MASK) << 3) + BIAS;
            t <<= (ulaw & SEG_MASK) >> SEG_SHIFT;
            return ((ulaw & SIGN_BIT) > 0 ? (BIAS - t) : (t - BIAS));
        }
        /// <summary>
        /// search
        /// </summary>
        /// <param name="val"></param>
        /// <param name="table"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        static short search(short val, short[] table, short size)
        {
            short i;
            int index = 0;
            for (i = 0; i < size; i++)
            {
                if (val <= table[index])
                {
                    return (i);
                }
                index++;
            }
            return (size);
        }

        /// <summary>
        /// linear2ulaw.
        /// </summary>
        /// <param name="pcm_val"></param>
        /// <returns></returns>
        public static Byte linear2ulaw(short pcm_val)
        {
            short mask = 0;
            short seg = 0;
            Byte uval = 0;

            /* Get the sign and the magnitude of the value. */
            pcm_val = (short)(pcm_val >> 2);
            if (pcm_val < 0)
            {
                pcm_val = (short)-pcm_val;
                mask = 0x7F;
            }
            else
            {
                mask = 0xFF;
            }
            /* clip the magnitude */
            if (pcm_val > CLIP)
            {
                pcm_val = CLIP;
            }
            pcm_val += (BIAS >> 2);

            /* Convert the scaled magnitude to segment number. */
            seg = search(pcm_val, seg_uend, (short)8);

            /*
            * Combine the sign, segment, quantization bits;
            * and complement the code word.
            */
            /* out of range, return maximum value. */
            if (seg >= 8)
            {
                return (Byte)(0x7F ^ mask);
            }
            else
            {
                uval = (Byte)((seg << 4) | ((pcm_val >> (seg + 1)) & 0xF));
                return ((Byte)(uval ^ mask));
            }
        }
        /// <summary>
        /// MuLawToLinear
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static Byte[] MuLawToLinear(Byte[] bytes, int bitsPerSample, int channels)
        {
            //Anzahl Spuren
            int blockAlign = channels * bitsPerSample / 8;

            //Für jeden Wert
            Byte[] result = new Byte[bytes.Length * blockAlign];
            for (int i = 0, counter = 0; i < bytes.Length; i++, counter += blockAlign)
            {
                //In Bytes umwandeln
                int value = MulawToLinear(bytes[i]);
                Byte[] values = BitConverter.GetBytes(value);

                switch (bitsPerSample)
                {
                    case 8:
                        switch (channels)
                        {
                            //8 Bit 1 Channel
                            case 1:
                                result[counter] = values[0];
                                break;

                            //8 Bit 2 Channel
                            case 2:
                                result[counter] = values[0];
                                result[counter + 1] = values[0];
                                break;
                        }
                        break;

                    case 16:
                        switch (channels)
                        {
                            //16 Bit 1 Channel
                            case 1:
                                result[counter] = values[0];
                                result[counter + 1] = values[1];
                                break;

                            //16 Bit 2 Channels
                            case 2:
                                result[counter] = values[0];
                                result[counter + 1] = values[1];
                                result[counter + 2] = values[0];
                                result[counter + 3] = values[1];
                                break;
                        }
                        break;
                }
            }

            //Fertig
            return result;
        }
        /// <summary>
        /// MuLawToLinear32
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="bitsPerSample"></param>
        /// <param name="channels"></param>
        /// <returns></returns>
        public static int[] MuLawToLinear32(Byte[] bytes, int bitsPerSample, int channels)
        {
            //Anzahl Spuren
            int blockAlign = channels;

            //Für jeden Wert
            int [] result = new int[bytes.Length * blockAlign];
            for (int i = 0, counter = 0; i < bytes.Length; i++, counter += blockAlign)
            {
                //In Int32 umwandeln
                int value = MulawToLinear(bytes[i]);
                
                switch (bitsPerSample)
                {
                    case 8:
                        switch (channels)
                        {
                            //8 Bit 1 Channel
                            case 1:
                                result[counter] = value;
                                break;

                            //8 Bit 2 Channel
                            case 2:
                                result[counter] = value;
                                result[counter + 1] = value;
                                break;
                        }
                        break;

                    case 16:
                        switch (channels)
                        {
                            //16 Bit 1 Channel
                            case 1:
                                result[counter] = value;
                                break;

                            //16 Bit 2 Channels
                            case 2:
                                result[counter] = value;
                                result[counter + 1] = value;
                                break;
                        }
                        break;
                }
            }

            //Fertig
            return result;
        }
        /// <summary>
        /// MuLawToLinear
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static Byte[] LinearToMulaw(Byte[] bytes, int bitsPerSample, int channels)
        {
            //Anzahl Spuren
            int blockAlign = channels * bitsPerSample / 8;

            //Ergebnis
            Byte[] result = new Byte[bytes.Length / blockAlign];
            int resultIndex = 0;
            for (int i = 0; i < result.Length; i++)
            {
                //Je nach Auflösung
                switch (bitsPerSample)
                {
                    case 8:
                        switch (channels)
                        {
                            //8 Bit 1 Channel
                            case 1:
                                result[i] = linear2ulaw(bytes[resultIndex]);
                                resultIndex += 1;
                                break;

                            //8 Bit 2 Channel
                            case 2:
                                result[i] = linear2ulaw(bytes[resultIndex]);
                                resultIndex += 2;
                                break;
                        }
                        break;

                    case 16:
                        switch (channels)
                        {
                            //16 Bit 1 Channel
                            case 1:
                                result[i] = linear2ulaw(BitConverter.ToInt16(bytes, resultIndex));
                                resultIndex += 2;
                                break;

                            //16 Bit 2 Channels
                            case 2:
                                result[i] = linear2ulaw(BitConverter.ToInt16(bytes, resultIndex));
                                resultIndex += 4;
                                break;
                        }
                        break;
                }
            }

            //Fertig
            return result;
        }
    }
}
