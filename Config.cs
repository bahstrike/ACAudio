using System;
using System.Collections.Generic;
using System.Text;
using Smith;
using System.IO;

namespace ACAudio
{
    public static class Config
    {
        public enum SoundMode
        {
            Song,
            _2D,
            _3D,
            Hybrid
        }

        public class SoundAttributes
        {
            public string file = string.Empty;
            public double vol = 1.0;
            public double mindist = 5.0;
            public double maxdist = 15.0;
            public bool sync = true;
            public bool looping = true;
            public bool randomstart = false;
            public double interval = 0.0;
            public double chance = 1.0;
            public double fade = 0.08;
            public double precache = 5.0;
            public SoundMode mode = SoundMode._3D;

            public SoundAttributes Clone()
            {
                SoundAttributes o = new SoundAttributes();

                o.file = file;
                o.vol = vol;
                o.mindist = mindist;
                o.maxdist = maxdist;
                o.sync = sync;
                o.looping = looping;
                o.randomstart = randomstart;
                o.interval = interval;
                o.chance = chance;
                o.fade = fade;
                o.precache = precache;
                o.mode = mode;

                return o;
            }
        }


        private static Stack<SoundAttributes> SoundAttributeStack = new Stack<SoundAttributes>();
        private static SoundAttributes CurrentSound
        {
            get
            {
                return SoundAttributeStack.Peek();
            }
        }

        public static void Clear()
        {

            // reset stack and prepopulate default
            SoundAttributeStack.Clear();
            SoundAttributeStack.Push(new SoundAttributes());
        }

        public static void Load(string filename)
        {
            Clear();

            _Load(filename);
        }

        private static void _Load(string filename)
        {
            try
            {
                using (StreamReader sr = File.OpenText(filename))
                    while (!sr.EndOfStream)
                    {
                        string ln = sr.ReadLine();

                        // strip comments
                        int commentI = ln.IndexOf("//");
                        if (commentI != -1)
                            ln = ln.Substring(0, commentI);

                        // strip 0x  hex prefix, if exist
                        for (; ; )
                        {
                            int hexI = ln.IndexOf("0x", StringComparison.InvariantCultureIgnoreCase);
                            if (hexI == -1)
                                break;

                            ln = ln.Remove(hexI, 2);
                        }

                        // final trim for fun
                        ln = ln.Trim();

                        // ignore empty lines
                        if (string.IsNullOrEmpty(ln))
                            continue;

                        // split line into directive | content
                        string directive, content;
                        int firstWhitespace = ln.IndexOfAny(new char[] { ' ', '\t' });
                        if (firstWhitespace == -1)
                        {
                            directive = ln;
                            content = string.Empty;
                        }
                        else
                        {
                            directive = ln.Substring(0, firstWhitespace);//shouldnt need to trim
                            content = ln.Substring(firstWhitespace).Trim();
                        }

                        // lets see what we got
                        switch (directive.ToLowerInvariant())
                        {
                            #region sound attribute directives
                            case "push":
                                SoundAttributeStack.Push(CurrentSound.Clone());
                                break;

                            case "pop":
                                SoundAttributeStack.Pop();
                                break;

                            case "file":
                                CurrentSound.file = content;
                                break;

                            case "vol":
                                GetDouble(content, ref CurrentSound.vol);
                                break;

                            case "mindist":
                                GetDouble(content, ref CurrentSound.mindist);
                                break;

                            case "maxdist":
                                GetDouble(content, ref CurrentSound.maxdist);
                                break;

                            case "sync":
                                CurrentSound.sync = IsOn(content);
                                break;

                            case "looping":
                                CurrentSound.looping = IsOn(content);
                                break;

                            case "randomstart":
                                CurrentSound.randomstart = IsOn(content);
                                break;

                            case "interval":
                                GetDouble(content, ref CurrentSound.interval);
                                break;

                            case "chance":
                                GetDouble(content, ref CurrentSound.chance);
                                break;

                            case "fade":
                                GetDouble(content, ref CurrentSound.fade);
                                break;

                            case "precache":
                                GetDouble(content, ref CurrentSound.precache);
                                break;

                            case "mode":
                                switch(content.ToLowerInvariant())
                                {
                                    case "song":
                                        CurrentSound.mode = SoundMode.Song;
                                        break;

                                    case "2d":
                                        CurrentSound.mode = SoundMode._2D;
                                        break;

                                    case "3d":
                                        CurrentSound.mode = SoundMode._3D;
                                        break;

                                    case "hybrid":
                                        CurrentSound.mode = SoundMode.Hybrid;
                                        break;
                                }
                                break;
                            #endregion

                            #region sound source directives
                            case "static":
                                {
                                    try
                                    {
                                        uint did = uint.Parse(content, System.Globalization.NumberStyles.HexNumber);

                                        int numadd = 0;
                                        foreach(PluginCore.StaticPosition staticPos in PluginCore.Instance.StaticPositions)
                                        {
                                            if (staticPos == null)
                                            {
                                                Log("framework is DUMB we got nulls");
                                                continue;
                                            }

                                            if (staticPos.ID != did)
                                                continue;

                                            numadd++;
                                        }

                                        Log($"NEED TO REGISTER {numadd} STATIC POSITIONS");// need to do stuff
                                    }
                                    catch(Exception ex)
                                    {
                                        throw new Exception($"failed to parse static from: {content}: {ex.Message}");
                                    }

                                }
                                break;

                            case "pos":
                                {
                                    // content better have 4 parts
                                    string[] parts = content.Split(',');
                                    if (parts.Length < 4)
                                        throw new Exception($"cant parse landblock,localX,localY,localZ from: {content}");


                                    Position pos;
                                    try
                                    {
                                        uint lb = uint.Parse(parts[0], System.Globalization.NumberStyles.HexNumber);
                                        double lx = double.Parse(parts[1]);
                                        double ly = double.Parse(parts[2]);
                                        double lz = double.Parse(parts[3]);

                                        pos = Position.FromLocal(lb, lx, ly, lz);
                                    }
                                    catch
                                    {
                                        throw new Exception($"cant parse landblock,localX,localY,localZ from: {content}");
                                    }


                                    Log($"NEED TO REGISTER POS {pos}");// need to do stuff
                                }
                                break;
                            #endregion

                            default:
                                Log($"config {filename} has unrecognized directive: {directive}");
                                break;
                        }
                    }
            }
            catch (Exception ex)
            {
                Log($"exception loading {filename}: {ex.Message}");
            }
        }

        private static void GetDouble(string s, ref double d)
        {
            if (!double.TryParse(s, out d))
                Log($"failed to parse \"{s}\" as double");
        }

        private static bool IsOn(string s)
        {
            // we'll accept   false/true    no/yes     off/on   0/1

            s = s.ToLowerInvariant();

            // just check the "off" state. anything else unrecognized defaults to true
            return
                s != "false" &&
                s != "no" &&
                s != "off" &&
                s != "0"
                ;
        }

        private static void Log(string s)
        {
            PluginCore.Log($"Config: {s}");
        }

    }
}
