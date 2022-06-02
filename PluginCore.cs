using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;

using Smith;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using VirindiViewService.Controls;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace ACAudio
{
    [WireUpBaseEvents]
    [FriendlyName(PluginName)]
    public class PluginCore : PluginBase
    {
        public const string PluginName = "ACAudio";//needs to match namespace or "embedded resources" wont work

        private static PluginCore _Instance = null;
        public static PluginCore Instance { get { return _Instance; } }

        public static int InstanceNumberGen = 0;
        public int InstanceNumber = 0;

        public static PluginHost PluginHost
        {
            get
            {
                return Instance.Host;
            }
        }

        public static CoreManager CoreManager
        {
            get
            {
                return Instance.Core;
            }
        }

        public VirindiViewService.HudView View;


        private static void _LogInfo(string s)
        {
            Log($"INFO: {s}");
        }

        private static void _LogWarning(string s)
        {
            Log($"WARNING: {s}");
        }

        private static void _LogError(string s)
        {
            Log($"ERROR: {s}");
        }


        public bool GetUserEnableAudio()
        {
            if (View == null)
                return false;

            HudCheckBox cb = View["Enable"] as HudCheckBox;
            if (cb == null)
                return false;

            return cb.Checked;
        }

        public bool GetUserEnableMusic()
        {
            if (View == null)
                return false;

            HudCheckBox cb = View["MusicEnable"] as HudCheckBox;
            if (cb == null)
                return false;

            return cb.Checked;
        }

        public bool IsPortaling = false;

        public double GetUserVolume()
        {
            if (View == null)
                return 0.0;

            HudHSlider slider = View["Volume"] as HudHSlider;
            if (slider == null)
                return 0.0;

            return (double)(slider.Position - slider.Min) / (double)(slider.Max - slider.Min);
        }

        public double GetUserMusicVolume()
        {
            if (View == null)
                return 0.0;

            HudHSlider slider = View["MusicVolume"] as HudHSlider;
            if (slider == null)
                return 0.0;

            return (double)(slider.Position - slider.Min) / (double)(slider.Max - slider.Min);
        }

        public bool GetUserAllowPortalMusic()
        {
            if (View == null)
                return false;

            HudCheckBox cb = View["PortalMusicEnable"] as HudCheckBox;
            if (cb == null)
                return false;

            return cb.Checked;
        }


        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup()
        {
            InstanceNumber = InstanceNumberGen++;
            _Instance = this;

            Smith.Log.InfoCallback = _LogInfo;
            Smith.Log.WarningCallback = _LogWarning;
            Smith.Log.ErrorCallback = _LogError;

            try
            {
                if (File.Exists(FinalLogFilepath))
                    File.Delete(FinalLogFilepath);

                Log("----------------------------------------------------------------------");
                Log("                            ACAudio Startup");
                Log("----------------------------------------------------------------------");


                Log("Generate virindi view");
                VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
                VirindiViewService.ViewProperties properties;
                VirindiViewService.ControlGroup controls;
                parser.ParseFromResource($"{PluginName}.mainView.xml", out properties, out controls);

                View = new VirindiViewService.HudView(properties, controls);



                Log("hook events");
                View.ThemeChanged += delegate (object sender, EventArgs e)
                {
                    RegenerateLogos();
                };



                View["Dump"].Hit += delegate (object sender, EventArgs e)
                {
                    WriteToChat("BLAH");


                    List<WorldObject> allobj = FilterByDistance(Core.WorldFilter.GetAll(), CameraPosition, 35.0);

                    // lol lets dump stuff to look at
                    Log("--------------- WE BE DUMPIN ------------");
                    foreach (WorldObject obj in allobj)
                    {
                        Log($"name:{obj.Name}  id:{obj.Id}  class:{obj.ObjectClass}  pos:{SmithInterop.Vector(obj.RawCoordinates())}");
                    }

                };

                View["Coords"].Hit += delegate (object sender, EventArgs e)
                {
                    WriteToChat("WEEE");

                    Log($"Position.FromLocal({Position.FromObject(Player)}),");
                };

                (View["Enable"] as HudCheckBox).Change += delegate (object sender, EventArgs e)
                {

                };

                View["Reload"].Hit += delegate (object sender, EventArgs e)
                {
                    ReloadConfig();
                };

                View["Defaults"].Hit += delegate (object sender, EventArgs e)
                {
                    (View["Enable"] as HudCheckBox).Checked = true;
                    (View["Volume"] as HudHSlider).Position = 75;

                    (View["MusicEnable"] as HudCheckBox).Checked = true;
                    (View["MusicVolume"] as HudHSlider).Position = 35;

                    (View["PortalMusicEnable"] as HudCheckBox).Checked = true;
                };

                View["FMOD"].Hit += delegate (object sender, EventArgs e)
                {
                    //System.Diagnostics.Process.Start("https://fmod.com/");
                };

                (View["FMOD_Credit"] as HudStaticText).FontHeight = 5;



                // create custom stuff
                HudFixedLayout debugLayout = View["DebugContent"] as HudFixedLayout;
                HudProxyMap hudmap = new HudProxyMap();
                hudmap.InternalName = "ProxyMap";
                Box2 fillRC = new Box2(debugLayout.ClipRegion);
                debugLayout.AddControl(hudmap, (Rectangle)(fillRC.Offsetted(-fillRC.UL).Inflated(Vec2.One * -fillRC.Size.Magnitude * 0.01)));




                using (INIFile ini = INIFile)
                {
                    (View["Enable"] as HudCheckBox).Checked = ini.GetKeyString("ACAudio", "Enable", "1") != "0";
                    (View["Volume"] as HudHSlider).Position = int.Parse(ini.GetKeyString("ACAudio", "Volume", "75"));

                    (View["MusicEnable"] as HudCheckBox).Checked = ini.GetKeyString("ACAudio", "MusicEnable", "1") != "0";
                    (View["MusicVolume"] as HudHSlider).Position = int.Parse(ini.GetKeyString("ACAudio", "MusicVolume", "35"));

                    (View["PortalMusicEnable"] as HudCheckBox).Checked = ini.GetKeyString("ACAudio", "PortalMusicEnable", "1") != "0";
                }


                Log("regen logos");
                RegenerateLogos();


                Log("init audio");
                if (!Audio.Init(1000, dopplerscale: 0.135f))
                    Log("Failed to initialize Audio");

                Music.Init();

                Log("hook render");
                Core.RenderFrame += _Process;


                Log("hook logoff");
                Core.CharacterFilter.Logoff += _CharacterFilter_Logoff;


                LoadStaticPositions();

                ReloadConfig();


                IsPortaling = true;
                StartPortalSong();// first time login portal deserves one
            }
            catch (Exception ex)
            {
                Log($"Startup exception: {ex.Message}");
            }
        }

        private void ReloadConfig()
        {
            Config.Load("master.aca");

            Log($"Parsed {Config.Sources.Count} sound sources from config");
        }

        public class StaticPosition
        {
            public readonly uint ID;
            public readonly Position Position;

            public StaticPosition(uint _ID, Position _Position)
            {
                ID = _ID;
                Position = _Position;
            }
        }

        public List<StaticPosition> StaticPositions = new List<StaticPosition>();

        private void LoadStaticPositions()
        {
            StaticPositions.Clear();

            ZipUtil zip = null;
            try
            {
                zip = new ZipUtil_File(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "static.dat"), ZipUtil_File.Mode.Open);

                int numEntries = zip.ReadInt();
                for (int x = 0; x < numEntries; x++)
                {
                    uint id = zip.ReadUInt();
                    Position pos = Position.FromStream(zip, true/*save filesize*/);

                    StaticPositions.Add(new StaticPosition(id, pos));
                }

                Log($"Loaded {StaticPositions.Count} from static.dat");
            }
            finally
            {
                if(zip != null)
                    zip.Close();
            }
            
        }

        private bool LogOff = false;
        private void _CharacterFilter_Logoff(object sender, LogoffEventArgs e)
        {
            if (LogOff)
                return;

            Log("LOGOFF LOL");
            IsPortaling = true;
            StartPortalSong();

            LogOff = true;


            using (INIFile ini = INIFile)
            {
                ini.WriteKey("ACAudio", "Enable", (View["Enable"] as HudCheckBox).Checked ? "1" : "0");
                ini.WriteKey("ACAudio", "Volume", (View["Volume"] as HudHSlider).Position.ToString());

                ini.WriteKey("ACAudio", "MusicEnable", (View["MusicEnable"] as HudCheckBox).Checked ? "1" : "0");
                ini.WriteKey("ACAudio", "MusicVolume", (View["MusicVolume"] as HudHSlider).Position.ToString());

                ini.WriteKey("ACAudio", "PortalMusicEnable", (View["PortalMusicEnable"] as HudCheckBox).Checked ? "1" : "0");
            }
        }

        public WorldObject Player
        {
            get
            {
                return Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First;
            }
        }

        public Vec3 CameraPosition
        {
            get
            {
                Position p;
                Mat4 m;
                GetCameraInfo(out p, out m);

                return p.Global;
            }
        }

        public INIFile INIFile
        {
            get
            {
                return new INIFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ACAudio.ini"));
            }
        }

        public const int BadWorldID = -1;

        long lastTimestamp = 0;
        private void _Process(object sender, EventArgs e)
        {
            if (lastTimestamp == 0)
            {
                lastTimestamp = PerfTimer.Timestamp;
                return;
            }

            long curtimestamp = PerfTimer.Timestamp;
            double truedt = PerfTimer.TimeBetween(lastTimestamp, curtimestamp);
            if (truedt <= 0.0)
                return;

            double dt;
            if (truedt > (1.0 / 20.0))
                dt = 1.0 / 20.0;
            else
                dt = truedt;
            lastTimestamp = curtimestamp;

            try
            {
                Process(dt, truedt);
            }
            catch (Exception ex)
            {
                Log($"Process exception: {ex.Message}");
            }
        }

        public void GetCameraInfo(out Position pos, out Mat4 mat)
        {
            UtilityBelt.Lib.Frame frame = UtilityBelt.Lib.Frame.Get(Host.Actions.Underlying.SmartboxPtr() + 8);//used with permission by trevis (UtilityBelt)

            pos = SmithInterop.Position(frame);
            mat = SmithInterop.Matrix(frame);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        private bool DoesACHaveFocus()
        {
            return (GetActiveWindow() == Host.Decal.Hwnd);
        }


        double lastProcessTime = 1.0;


        double sayStuff = 0.0;
        private void Process(double dt, double truedt)
        {
            PerfTimer pt_process = new PerfTimer();
            pt_process.Start();

            Audio.AllowSound = GetUserEnableAudio();

            if (DoesACHaveFocus())
                Audio.MasterVolume = GetUserVolume();
            else
                Audio.MasterVolume = 0.0;

            Music.EnableWorld = GetUserEnableMusic();
            Music.EnablePortal = GetUserAllowPortalMusic();
            Music.Volume = GetUserMusicVolume();

            Music.Process(dt);


            PortalSongHeat = Math.Max(0.0, PortalSongHeat - PortalSongHeatCooldown * dt);


            //Mat4 cameraMat = GetCameraMatrix();


            //Vec3 cameraPos = cameraMat.Position;//SmithInterop.Vector(Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First.RawCoordinates());

            Position cameraPos;
            Mat4 cameraMat;
            GetCameraInfo(out cameraPos, out cameraMat);


            //WorldObject w;
            //w.RawCoordinates
            //Vector3Object v;

            //Decal.Adapter.Wrappers.D3DObj d;
            //d.OrientToCamera


            sayStuff += dt;
            if (sayStuff >= 0.2)
            {
                sayStuff = 0.0;


                // only try to play ambient sounds if not portaling
                if (GetUserEnableAudio() && !IsPortaling)
                {


                    {

                        // dynamic objects


                        // now lets play
                        foreach (Config.SoundSourceDynamic src in Config.FindSoundSourcesDynamic())
                        {
                            List<WorldObject> finalObjects = new List<WorldObject>();

                            foreach(WorldObject obj in Core.WorldFilter.GetByObjectClass(src.ObjectClass))
                            {
                                Position? objPos = Position.FromObject(obj);
                                if (!objPos.HasValue || !objPos.Value.IsCompatibleWith(cameraPos))
                                    continue;

                                // ignore items that were previously on ground but now picked up
                                if (obj.Values(LongValueKey.Container) != 0)
                                    continue;

                                // optionally filter by name
                                if(!string.IsNullOrEmpty(src.Name))
                                {
                                    if (!obj.Name.Equals(src.Name))
                                        continue;
                                }

                                double dist = (cameraPos.Global - objPos.Value.Global).Magnitude;
                                if (dist > src.Sound.maxdist)
                                    continue;

                                finalObjects.Add(obj);
                            }

                            // if we have a cluster, reduce vol/dist
                            double volAdjust = 1.0;
                            double minDistAdjust = 1.0;
                            double maxDistAdjust = 1.0;

                            if (finalObjects.Count > 3)
                            {
                                volAdjust = 0.4;
                                minDistAdjust = 0.9;
                                maxDistAdjust = 0.5;
                            }

                            foreach (WorldObject obj in finalObjects)
                            {
                                PlayForObject(obj, src.Sound.file, src.Sound.vol * volAdjust, src.Sound.mindist * minDistAdjust, src.Sound.maxdist * maxDistAdjust);
                            }
                        }



                    }


                    // static positions
                    {



                        // build list of final candidates
                        List<StaticPosition> finalPositions = new List<StaticPosition>();
                        foreach (StaticPosition pos in StaticPositions)
                        {
                            if (!pos.Position.IsCompatibleWith(cameraPos))
                                continue;


#if false
                            double dist = (cameraPos.Global - pos.Position.Global).Magnitude;

                            if (dist > AudioSearchDist/*needs a pos.MaxDist*/)
                            {
                                //Log($"bad dist  {dist} > {maxDist}     cam:{cameraPos.Global}  VS pos:{pos.Position.Global}  ");
                                continue;
                            }
#endif

                            //Log("WE KEEP");

                            finalPositions.Add(pos);
                        }




                        // dispatch
                        foreach (StaticPosition pos in finalPositions)
                        {
                            Config.SoundSourceStatic src = Config.FindSoundSourceStatic(pos.ID);
                            if (src == null)
                                continue;
                            
                            double dist = (cameraPos.Global - pos.Position.Global).Magnitude;
                            if (dist >= src.Sound.maxdist)
                                continue;

                            PlayForPosition(pos.Position, src.Sound.file, src.Sound.vol, src.Sound.mindist, src.Sound.maxdist);
                        }




                        // dispatch static positions
                        foreach (Config.SoundSourcePosition src in Config.FindSoundSourcesPosition(cameraPos))
                        {
                            double dist = (cameraPos.Global - src.Position.Global).Magnitude;
                            if (dist >= src.Sound.maxdist)
                                continue;

                            PlayForPosition(src.Position, src.Sound.file, src.Sound.vol, src.Sound.mindist, src.Sound.maxdist);
                        }
                    }
                }


                (View["Info"] as HudStaticText).Text =
                    $"fps:{(int)(1.0/dt)}  process:{(int)(lastProcessTime * 1000.0 * 1000.0)}usec  mem:{((double)Audio.MemoryUsageBytes/1024.0/1024.0).ToString("#0.0")}mb\n" +
                    $"ambs:{ActiveAmbients.Count}  channels:{Audio.ChannelCount}  sounds:{Audio.SoundCount}\n" +
                    $"cam:{cameraPos.Global}  lb:{cameraPos.Landblock.ToString("X8")}\n" +
                    $"portalsongheat:{(MathLib.Clamp(PortalSongHeat / PortalSongHeatMax) * 100.0).ToString("0")}%  {PortalSongHeat.ToString(MathLib.ScalarFormattingString)}";
            }



            // kill/forget sounds for objects out of range?
            for (int x = 0; x < ActiveAmbients.Count; x++)
            {
                Ambient a = ActiveAmbients[x];

                string discardReason = null;

                // kill all ambients if disabled
                if (discardReason == null)
                {
                    if (!GetUserEnableAudio())
                        discardReason = "no audio";
                }

                // kill all ambients if portaling
                if (discardReason == null)
                {
                    if (IsPortaling)
                        discardReason = "portaling";
                }

                if (discardReason == null)
                {
                    // cull bad object references
                    if (a is ObjectAmbient)
                    {
                        ObjectAmbient oa = a as ObjectAmbient;

                        if (oa.WorldObject == null)
                            discardReason = "bad object ref";
                    }

                }

                if(discardReason == null)
                {
                    // cull incompatible dungeon id
                    if (!cameraPos.IsCompatibleWith(a.Position))
                        discardReason = "wrong dungeon";
                }

                if (discardReason == null)
                {
                    float minDist, maxDist;
                    a.Channel.channel.get3DMinMaxDistance(out minDist, out maxDist);

                    double dist = (cameraPos.Global - a.Position.Global).Magnitude;

                    // fudge dist a bit?
                    maxDist += 2.0f;

                    if (dist > (double)maxDist)
                        discardReason = $"bad dist {dist} > {maxDist}";
                }


                if(discardReason == null)
                {
                    ObjectAmbient oa = a as ObjectAmbient;
                    if(oa != null)
                    {
                        WorldObject wo = oa.WorldObject;
                        if (wo == null)
                            discardReason = "object disappeared";
                        else if (wo.Values(LongValueKey.Container) != 0)
                            discardReason = "object in container";
                    }
                }


                // decide whether to remove or update
                if (discardReason != null)
                {

                    if (a is ObjectAmbient)
                        Log($"channel ({a.Channel.ID}): removing for weenie {(a as ObjectAmbient).WeenieID}: {discardReason}");
                    else if (a is StaticAmbient)
                        Log($"channel ({a.Channel.ID}): removing for static {(a as StaticAmbient).Position}: {discardReason}");



                    a.Channel.Stop();

                    ActiveAmbients.RemoveAt(x);
                    x--;
                }
                else
                {
                    // update position
                    a.Channel.SetPosition(a.Position.Global, Vec3.Zero);
                }
            }




            // lets sync time positions for active ambient loopables that want to
            List<string> alreadyDone = new List<string>();
            for (int x = 0; x < ActiveAmbients.Count - 1; x++)
            {
                Ambient aa = ActiveAmbients[x];
                if (!aa.Channel.IsPlaying)
                    continue;

                if (alreadyDone.Contains(aa.Channel.Sound.Name))
                    continue;

                alreadyDone.Add(aa.Channel.Sound.Name);


                for (int y = 1; y < ActiveAmbients.Count; y++)
                {
                    Ambient ab = ActiveAmbients[y];
                    if (!ab.Channel.IsPlaying)
                        continue;

                    // if not playing same sound, skip
                    if (!aa.Channel.Sound.Name.Equals(ab.Channel.Sound.Name, StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    // do we want to sync sound timestamps? skip now if not
                    //if (no)
                    //continue;


                    // copy A timestamp to B
                    uint posFmod;
                    aa.Channel.channel.getPosition(out posFmod, FMOD.TIMEUNIT.PCM);
                    ab.Channel.channel.setPosition(posFmod, FMOD.TIMEUNIT.PCM);
                }
            }



            (View["ProxyMap"] as HudProxyMap).Invalidate();// draw every frame.. realtime map



            // Z is up

            Audio.Process(dt, truedt, cameraPos.Global, Vec3.Zero, cameraMat.Up, cameraMat.Forward);


            pt_process.Stop();
            lastProcessTime = pt_process.Duration;
        }

        public static Audio.Sound GetOrLoadSound(string name, Audio.DimensionMode mode, bool looping)
        {
            Audio.Sound snd = Audio.GetSound(name, mode, looping);
            if (snd != null)
                return snd;

            if (PluginCore.Instance == null)
                return null;

            try
            {
                Log($"Loading file to RAM: {name}");
                byte[] buf = PluginCore.ReadDataFile(name);
                if (buf == null || buf.Length == 0)
                    return null;

                return Audio.GetSound(name, buf, mode, looping);
            }
            catch(Exception ex)
            {
                Log($"GetOrLoadSound() Cant load {name}: {ex.Message}");

                return null;
            }
        }

        public abstract class Ambient
        {
            public Audio.Channel Channel;

            public abstract Position Position
            {
                get;
            }
        }

        public class ObjectAmbient : Ambient
        {
            public int WeenieID;

            public override Position Position
            {
                get
                {
                    return Position.FromObject(WorldObject) ?? Position.Invalid;
                }
            }

            public WorldObject WorldObject
            {
                get
                {
                    return Instance.Core.WorldFilter[WeenieID];
                }
            }
        }

        public class StaticAmbient : Ambient
        {
            public Position StaticPosition;

            public override Position Position
            {
                get
                {
                    return StaticPosition;
                }
            }
        }

        public List<Ambient> ActiveAmbients = new List<Ambient>();

        public void PlayForPosition(Position pos, string filename, double vol, double minDist, double maxDist)
        {
            //Log($"playforposition {pos} | {filename}");

            // check if playing already
            foreach (Ambient a in ActiveAmbients)
            {
                StaticAmbient sa = a as StaticAmbient;
                if (sa == null)
                    continue;

                if (sa != null)
                {
                    // check reference
                    double dist = (sa.Position.Global - pos.Global).Magnitude;
                    if (dist > float.Epsilon)
                        continue;

                    //Log("checking");

                    // if same name, bail
                    if (sa.Channel.Sound.Name.Equals(filename, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //Log("playforposition bailed on same filename");
                        return;
                    }


                    // changing sounds? kill existing
                    sa.Channel.Stop();

                    ActiveAmbients.Remove(sa);

                    //Log("playforposition removed an ambient");

                    break;
                }
            }

            // get sound
            Audio.Sound snd = GetOrLoadSound(filename, Audio.DimensionMode._3DPositional, true);
            if (snd == null)
                return;


            // start new sound
            StaticAmbient newsa = new StaticAmbient();

            newsa.StaticPosition = pos;

            newsa.Channel = Audio.PlaySound(snd, true);
            newsa.Channel.SetPosition(pos.Global, Vec3.Zero);
            newsa.Channel.Volume = vol;
            newsa.Channel.SetMinMaxDistance(minDist, maxDist);
            newsa.Channel.Play();

            ActiveAmbients.Add(newsa);

            Log($"channel ({newsa.Channel.ID}): added static play {filename} at {pos}");
        }

        public void PlayForObject(WorldObject obj, string filename, double vol, double minDist, double maxDist)
        {
            // check if playing already
            foreach (Ambient a in ActiveAmbients)
            {
                ObjectAmbient oa = a as ObjectAmbient;
                if (oa == null)
                    continue;

                if (oa != null)
                {
                    // check reference
                    if (oa.WeenieID != obj.Id)
                        continue;

                    // if same name, bail
                    if (oa.Channel.Sound.Name.Equals(filename, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // do we want to update properties?
                        oa.Channel.Volume = vol;
                        oa.Channel.SetMinMaxDistance(minDist, maxDist);

                        return;
                    }

                    // changing sounds? kill existing
                    oa.Channel.Stop();

                    ActiveAmbients.Remove(oa);

                    break;
                }
            }

            // get sound
            Audio.Sound snd = GetOrLoadSound(filename, Audio.DimensionMode._3DPositional, true);
            if (snd == null)
                return;


            // start new sound
            ObjectAmbient newoa = new ObjectAmbient();

            newoa.WeenieID = obj.Id;

            newoa.Channel = Audio.PlaySound(snd, true);
            newoa.Channel.SetPosition(SmithInterop.Vector(obj.RawCoordinates()), Vec3.Zero);
            newoa.Channel.Volume = 0.0;
            newoa.Channel.SetTargetVolume(vol, 0.08);//slide fade-in; especially in case of portaling
            newoa.Channel.SetMinMaxDistance(minDist, maxDist);
            newoa.Channel.Play();

            ActiveAmbients.Add(newoa);

            Log($"channel ({newoa.Channel.ID}): added weenie play {filename} from ID {obj.Id}");
        }

        public static byte[] ReadDataFile(string filename)
        {
            return System.IO.File.ReadAllBytes(GenerateDataPath(filename));
        }

        public static string GenerateDataPath(string filename)
        {
            return System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "data"), filename);
        }

        public List<WorldObject> FilterByDistance(IEnumerable<WorldObject> objects, Vec3 pt, double dist)
        {
            List<WorldObject> objs = new List<WorldObject>();

            foreach (WorldObject obj in objects)
            {
                if ((pt - SmithInterop.ObjectGlobalPosition(obj)).Magnitude > dist)
                    continue;

                objs.Add(obj);
            }

            return objs;
        }

        private void RegenerateLogos()
        {
            Bitmap fmodBitmap = (Bitmap)Bitmap.FromStream(GetEmbeddedFile("FMOD_logo.png"));

            fmodBitmap = ReprocessLogo(fmodBitmap);

            VirindiViewService.ACImage fmodImage = new VirindiViewService.ACImage(fmodBitmap, VirindiViewService.ACImage.eACImageDrawOptions.DrawStretch);
            (View["FMOD"] as HudButton).Image = fmodImage;



        }


        // cant seem to get a transparent image to load into a HudButton.Image properly..
        // guess we have to fill in the background ourselves.. or better yet just subclass it.
        // but this'll do for most themes
        private Bitmap ReprocessLogo(Bitmap src)
        {
            int w = src.Width;
            int h = src.Height;

            Bitmap dst = new Bitmap(w, h, src.PixelFormat);

            using (Graphics gfx = Graphics.FromImage(dst))
                View.Theme.FloodFill(gfx, "ButtonBackground", new Rectangle(0, 0, w, h));

            Color fill = View.Theme.GetColor("ButtonText");

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color clr = src.GetPixel(x, y);

                    if (clr.A > 0)
                        dst.SetPixel(x, y, fill);
                }
            }

            return dst;
        }


        public double PortalSongHeat = 0.0;
        public const double PortalSongHeatMax = 1.55;
        public const double PortalSongHeatCooldown = 0.0175;

        public static string PortalSongFilename
        {
            get
            {
                return Config.PortalSound?.file;
            }
        }
        private void StartPortalSong()
        {
            if (PortalSongHeat >= PortalSongHeatMax)
            {
                Music.Stop();// ensure existing music is stopped even if we decide not to play
                return;
            }

            Music.Play(PortalSongFilename, true);
            PortalSongHeat += 1.0;
        }

        [BaseEvent("ChangePortalMode", "CharacterFilter")]
        private void CharacterFilter_ChangePortalMode(object sender, ChangePortalModeEventArgs e)
        {
            if(e.Type == PortalEventType.EnterPortal)
            {
                // start sound
                Log("changeportalmode START");

                IsPortaling = true;
                StartPortalSong();
            } else
            {
                Log("changeportalmode DONE");


                bool handled = false;

                // bg music?
                if (GetUserEnableMusic())
                {
                    Position? plrPos = Position.FromObject(Player);
                    if (plrPos.HasValue)
                    {

                        // for now (testing) just start playing a song depending on terrain or inside lol
                        if (plrPos.Value.IsTerrain)
                        {
                            Music.Play("ac_anotherorch.mp3", false);
                            handled = true;
                        }
                        else
                        {
                            // check for dungeon music?
                            Config.SoundSourceDungeon src = Config.FindSoundSourceDungeonSong(plrPos.Value.DungeonID);
                            if (src != null)
                            {
                                Music.Play(src.Sound.file, false);

                                handled = true;
                            }
                            else
                            {
                                Music.Play("ac_someoffbeat.mp3", false);

                                handled = true;
                            }

                        }


                    }
                }

                // we must ensure to stop portal music even of something else didnt decide to transition
                if(!handled)
                    Music.Stop();


                IsPortaling = false;
            }
        }

        [BaseEvent("LoginComplete", "CharacterFilter")]
        private void CharacterFilter_LoginComplete(object sender, EventArgs e)
        {
            WriteToChat($"Startup");


            // dont play music here cause its probably within portal music


        }

        /// <summary>
        /// This is called when the plugin is shut down. This happens only once.
        /// </summary>
        protected override void Shutdown()
        {
            Core.CharacterFilter.Logoff -= _CharacterFilter_Logoff;

            Core.RenderFrame -= _Process;

            Music.Shutdown();
            Audio.Shutdown();


            Log("----------------------------------------------------------------------");
            Log("                           ACAudio Shutdown");
            Log("----------------------------------------------------------------------");
        }

        private void FilterCore_CommandLineText(object sender, ChatParserInterceptEventArgs e)
        {
            if (false)
            {

                //Do not execute as AC command.
                e.Eat = true;
            }

        }

        public static Stream GetEmbeddedFile(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream($"{PluginName}.{name}");
        }

        public static void WriteToChat(string message)
        {
            try
            {
                Instance.Host.Actions.AddChatText($"[{PluginName}] {message}", 5);
                Log($"WriteToChat: {message}");
            }
            catch (Exception)
            {

            }
        }

        private static string FinalLogFilepath
        {
            get
            {
                return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $"{PluginName}.log");
            }
        }

        public static void Log(string ln)
        {
            using (StreamWriter logFile = File.AppendText(FinalLogFilepath))
                logFile.WriteLine($"{DateTime.Now.ToLongTimeString()}: {ln}");
        }


        [DllImport("Decal.dll")]
        static extern int DispatchOnChatCommand(ref IntPtr str, [MarshalAs(UnmanagedType.U4)] int target);

        static bool Decal_DispatchOnChatCommand(string cmd)
        {
            IntPtr bstr = Marshal.StringToBSTR(cmd);

            try
            {
                bool eaten = (DispatchOnChatCommand(ref bstr, 1) & 0x1) > 0;

                return eaten;
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }

        /// <summary>
        /// This will first attempt to send the messages to all plugins. If no plugins set e.Eat to true on the message, it will then simply call InvokeChatParser.
        /// </summary>
        /// <param name="cmd"></param>
        public static void DispatchChatToBoxWithPluginIntercept(string cmd)
        {
            if (string.IsNullOrEmpty(cmd))
                return;

            if (!Decal_DispatchOnChatCommand(cmd))
                CoreManager.Current.Actions.InvokeChatParser(cmd);
        }

    }
}
