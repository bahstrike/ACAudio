using System;
using System.Collections.Generic;
using System.Text;
using Smith;
using System.IO;
using Decal.Adapter.Wrappers;

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
                o.mode = mode;

                return o;
            }
        }

        public class SoundSource
        {
            public readonly SoundAttributes Sound;

            protected SoundSource(SoundAttributes _Sound)
            {
                Sound = _Sound.Clone();// clone this in current state! attribute stack may modify _Sound's properties later on
            }
        }

        public class SoundSourceStatic : SoundSource
        {
            public readonly uint DID;

            public SoundSourceStatic(SoundAttributes _Sound, uint _DID)
                : base(_Sound)
            {
                DID = _DID;
            }
        }

        public class SoundSourceDynamic : SoundSource
        {
            public class TagValue
            {
                public readonly string Tag;
                public readonly string SubTag;
                public readonly string Value;

                public TagValue(string _Tag, string _SubTag, string _Value)
                {
                    Tag = _Tag;
                    SubTag = _SubTag;
                    Value = _Value;
                }
            }

            public readonly TagValue[] TagValues;

            public string FriendlyDescription
            {
                get
                {
                    string str = string.Empty;

                    foreach (TagValue tv in TagValues)
                        str += $"{tv.Tag}({tv.SubTag})={tv.Value}, ";

                    return str;
                }
            }

            public SoundSourceDynamic(SoundAttributes _Sound, TagValue[] _TagValues)
                : base(_Sound)
            {
                if (_TagValues.Length == 0)
                    throw new Exception("CANT HAVE A DYNAMIC SOURCE WITH NO FILTER");

                TagValues = _TagValues;
            }

            public bool CheckObject(ShadowObject obj)
            {
                if (obj == null)
                    return false;

                foreach(TagValue tv in TagValues)
                {
                    switch (tv.Tag)
                    {
                        case "class":
                            {
                                ObjectClass oc;

                                int _oc;
                                if (int.TryParse(tv.Value, out _oc))
                                    oc = (ObjectClass)_oc;
                                else
                                    oc = (ObjectClass)Enum.Parse(typeof(ObjectClass), tv.Value, true);

                                if (obj.ObjectClass != oc)
                                    return false;
                            }
                            break;

                        case "string":
                            {
                                StringValueKey key;

                                int _key;
                                if (int.TryParse(tv.SubTag, out _key))
                                    key = (StringValueKey)_key;
                                else
                                    key = (StringValueKey)Enum.Parse(typeof(StringValueKey), tv.SubTag, true);


                                // if object doesnt have key, attempt to query but say we dont match yet
                                if (!obj.StringKeys.Contains((int)key))
                                {
                                    PluginCore.Instance.QueryForIdInfo(obj.Object);
                                    return false;
                                }

                                if (!obj.Values(key).Equals(tv.Value))// i guess we'll leave it case sensitive since user is putting it into quote marks
                                    return false;
                            }
                            break;

                        case "long":
                            {
                                LongValueKey key;

                                int _key;
                                if (int.TryParse(tv.SubTag, out _key))
                                    key = (LongValueKey)_key;
                                else
                                    key = (LongValueKey)Enum.Parse(typeof(LongValueKey), tv.SubTag, true);

                                int val;
                                if (!int.TryParse(tv.Value, out val))
                                    return false;//if failed to parse then never succeed


                                // if object doesnt have key, attempt to query but say we dont match yet
                                if (!obj.LongKeys.Contains((int)key))
                                {
                                    PluginCore.Instance.QueryForIdInfo(obj.Object);
                                    return false;
                                }

                                if (obj.Values(key) != val)
                                    return false;
                            }
                            break;
                    }

                }

                return true;
            }
        }

        public class SoundSourcePosition : SoundSource
        {
            public readonly Position Position;

            public SoundSourcePosition(SoundAttributes _Sound, Position _Position)
                : base(_Sound)
            {
                Position = _Position;
            }
        }

        public class SoundSourceDungeon : SoundSource
        {
            public readonly int DungeonID;

            public SoundSourceDungeon(SoundAttributes _Sound, int _DungeonID)
                : base(_Sound)
            {
                DungeonID = _DungeonID;
            }
        }

        public class SoundSourceText : SoundSource
        {
            public readonly string Text;

            public SoundSourceText(SoundAttributes _Sound, string _Text)
                : base(_Sound)
            {
                Text = _Text;
            }
        }

        public static List<SoundSource> Sources = new List<SoundSource>();
        public static SoundAttributes PortalSound = null;


        public static SoundSourceText FindSoundSourceText(string txt)
        {
            if (string.IsNullOrEmpty(txt))
                return null;

            string[] lines = txt.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);// should be at least 1

            foreach (SoundSource src in Sources)
            {
                SoundSourceText txtSrc = src as SoundSourceText;
                if (txtSrc == null)
                    continue;

#if false
                // need to handle multiline in config
#else
                if (!lines[0].Equals(txtSrc.Text))
                    continue;

                Log("found text source!!");
#endif

                return txtSrc;
            }

            return null;
        }

        public static SoundSourceStatic FindSoundSourceStatic(uint did)
        {
            foreach(SoundSource src in Sources)
            {
                SoundSourceStatic staticSrc = src as SoundSourceStatic;
                if (staticSrc == null)
                    continue;

                if (staticSrc.DID != did)
                    continue;

                return staticSrc;
            }

            return null;
        }

        // does not search distance; just compatible landblock
        public static SoundSourcePosition[] FindSoundSourcesPosition(Position compatiblePos)
        {
            List<SoundSourcePosition> ret = new List<SoundSourcePosition>();

            foreach(SoundSource src in Sources)
            {
                SoundSourcePosition posSrc = src as SoundSourcePosition;
                if (posSrc == null)
                    continue;

                if (!posSrc.Position.IsCompatibleWith(compatiblePos))
                    continue;

                ret.Add(posSrc);
            }

            return ret.ToArray();
        }

        public static SoundSourceDynamic[] FindSoundSourcesDynamic()
        {
            List<SoundSourceDynamic> ret = new List<SoundSourceDynamic>();

            foreach(SoundSource src in Sources)
            {
                SoundSourceDynamic dynSrc = src as SoundSourceDynamic;
                if (dynSrc == null)
                    continue;

                ret.Add(dynSrc);
            }

            return ret.ToArray();
        }

        public static SoundSourceDungeon FindSoundSourceDungeonSong(int dungeonID)
        {
            foreach(SoundSource src in Sources)
            {
                SoundSourceDungeon dungeonSrc = src as SoundSourceDungeon;
                if (dungeonSrc == null)
                    continue;

                if (dungeonSrc.DungeonID != dungeonID)
                    continue;

                if (dungeonSrc.Sound.mode != SoundMode.Song)
                    continue;

                return dungeonSrc;
            }

            return null;
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
            Sources.Clear();
            PortalSound = null;

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
                Log($"Parsing {filename}...");
                using (StreamReader sr = File.OpenText(PluginCore.GenerateDataPath(filename)))
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
#region special directives
                            case "include":
                                _Load(content);
                                break;
#endregion

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

                                        //Log($"NEED TO REGISTER {numadd} STATIC POSITIONS");// need to do stuff
                                        Sources.Add(new SoundSourceStatic(CurrentSound, did));
                                    }
                                    catch(Exception ex)
                                    {
                                        throw new Exception($"failed to parse static from: {content}: {ex.Message}");
                                    }

                                }
                                break;

                            case "dynamic":
                                {
                                    // split content into  tag=value  pairs
                                    List<SoundSourceDynamic.TagValue> tagvalues = new List<SoundSourceDynamic.TagValue>();

                                    string curcontent = content;
                                    while (!string.IsNullOrEmpty(curcontent))
                                    {
                                        int eqI = curcontent.IndexOf('=');
                                        if (eqI == -1)
                                            throw new Exception($"dynamic content must be in form of tag=value: {content}");
                                        
                                        string left = curcontent.Substring(0, eqI).Trim().ToLowerInvariant();
                                        curcontent = curcontent.Substring(eqI + 1).Trim();

                                        string right;
                                        if (left.StartsWith("string"))
                                        {
                                            int startQuote = curcontent.IndexOf('"');
                                            if (startQuote == -1)
                                                throw new Exception($"bad string in dynamic: {content}");

                                            int endQuote = curcontent.IndexOf('"', startQuote + 1);
                                            if (endQuote == -1)
                                                throw new Exception($"bad string in dynamic: {content}");


                                            right = curcontent.Substring(startQuote + 1, endQuote - startQuote - 1);

                                            curcontent = curcontent.Substring(endQuote + 1).Trim();
                                        }
                                        else
                                        {
                                            int spaceI = curcontent.IndexOfAny(new char[] { ' ', '\t' });
                                            if (spaceI == -1)
                                                spaceI = curcontent.Length;//end of line?

                                            right = curcontent.Substring(0, spaceI);
                                            curcontent = curcontent.Substring(spaceI).Trim();
                                        }



                                        // postprocess left to extract possible subtag
                                        string subtag = string.Empty;
                                        int parenI = left.IndexOf('(');
                                        if(parenI != -1)
                                        {
                                            subtag = left.Substring(parenI + 1, left.Length - parenI - 2).Trim();
                                            left = left.Substring(0, parenI).Trim();
                                        }

                                        //Log($"GOT STUFF   {left}({subtag})={right}");


                                        tagvalues.Add(new SoundSourceDynamic.TagValue(left, subtag, right));
                                    }


                                    Sources.Add(new SoundSourceDynamic(CurrentSound, tagvalues.ToArray()));
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


                                    //Log($"NEED TO REGISTER POS {pos}");// need to do stuff
                                    Sources.Add(new SoundSourcePosition(CurrentSound, pos));
                                }
                                break;

                            case "dungeon":
                                {
                                    // content should be 4 hex characters (upper 16-bit of landblock)
                                    // or 8 hex characters (full landblock)
                                    ushort dungeonID;
                                    if (content.Length == 4)
                                        dungeonID = ushort.Parse(content, System.Globalization.NumberStyles.HexNumber);
                                    else if (content.Length == 8)
                                    {
                                        uint lb = uint.Parse(content, System.Globalization.NumberStyles.HexNumber);
                                        dungeonID = (ushort)(lb >> 16);
                                    }
                                    else
                                        throw new Exception($"dungeonID must be 4 or 8 hex characters: {content}");


                                    //Log($"NEED TO REGISTER DUNGEONID {dungeonID.ToString("X4")}");
                                    Sources.Add(new SoundSourceDungeon(CurrentSound, dungeonID));
                                }
                                break;

                            case "portal":
                                {

                                    //Log($"NEED TO REGISTER PORTAL");
                                    PortalSound = CurrentSound.Clone();// be sure to clone since attributes stack may modify later
                                }
                                break;

                            case "text":
                                {
                                    string txt = content.Replace("\"", "");

                                    Log($"adding text source: {txt}");
                                    Sources.Add(new SoundSourceText(CurrentSound, txt));
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
