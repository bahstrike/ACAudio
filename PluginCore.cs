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

        public static bool PluginEnable;

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


            using (INIFile ini = INIFile)
                PluginEnable = ini.GetKeyString(Core.CharacterFilter.AccountName, "PluginEnable", "1") != "0";

            try
            {
                if (PluginEnable && File.Exists(FinalLogFilepath))
                    File.Delete(FinalLogFilepath);

                Log("----------------------------------------------------------------------");
                Log("                            ACAudio Startup");
                Log("----------------------------------------------------------------------");


                //Log("Generate virindi view");
                VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
                VirindiViewService.ViewProperties properties;
                VirindiViewService.ControlGroup controls;
                parser.ParseFromResource($"{PluginName}.mainView.xml", out properties, out controls);

                View = new VirindiViewService.HudView(properties, controls);



                //Log("hook events");
                View.ThemeChanged += delegate (object sender, EventArgs e)
                {
                    RegenerateLogos();
                };



                View["Dump"].Hit += delegate (object sender, EventArgs e)
                {
                    WriteToChat("BLAH");

                    List<WorldObject> allobj = FilterByDistance(FrameObjects, CameraPosition, 35.0);

                    // lol lets dump stuff to look at
                    Log("--------------- WE BE DUMPIN ------------");
                    foreach (WorldObject obj in allobj)
                    {
                        string stringkeys = string.Empty;
                        foreach (int i in obj.StringKeys)
                            stringkeys += $"{(StringValueKey)i}=\"{obj.Values((StringValueKey)i)}\", ";
                        
                        string longkeys = string.Empty;
                        foreach(int i in obj.LongKeys)
                            longkeys += $"{(LongValueKey)i}={obj.Values((LongValueKey)i)}, ";

                        /*
                         NOTE:  i found LongValueKey(134) has the following value on [identified] players
                            134=2		normal
                            134=64		pk lite
                            134=4		pk
                         */

                        /*
                        "flags:{obj.Values(LongValueKey.Flags)}  type:{obj.Values(LongValueKey.Type)}   behavior:{obj.Values(LongValueKey.Behavior)}  category:{obj.Values(LongValueKey.Category)}   longkeys:{longkeys}"
                        */

                        Log($"class:{obj.ObjectClass}   id:{obj.Id}   name:{obj.Name}   stringkeys:{{{stringkeys}}}  longkeys:{{{longkeys}}}  pos:{SmithInterop.Vector(obj.RawCoordinates())}");
                    }

                };

                View["Coords"].Hit += delegate (object sender, EventArgs e)
                {
                    WriteToChat("WEEE");

                    // the log message is intended to be copy&paste to the .ACA files
                    Log($"pos {Position.FromObject(Player).ToString().Replace(" ", "")/*condense*/}");
                };

                (View["Enable"] as HudCheckBox).Change += delegate (object sender, EventArgs e)
                {

                };

                View["Reload"].Hit += delegate (object sender, EventArgs e)
                {
                    // kill ambients; let reloaded config generate them again
                    foreach(Ambient amb in ActiveAmbients)
                        amb.Stop();
                    ActiveAmbients.Clear();
                    
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

                View["NearestDID"].Hit += delegate (object sender, EventArgs e)
                {
                    List<StaticPosition> list = LoadStaticPositions(true);

                    Position? pos = Position.FromObject(Player);
                    if(pos.HasValue)
                    {
                        List<uint> nearDIDs = new List<uint>();

                        double nearestDist = MathLib.Infinity;
                        uint nearestDid = 0;

                        foreach(StaticPosition sp in list)
                        {
                            if (!sp.Position.IsCompatibleWith(pos.Value))
                                continue;

                            double dist = (sp.Position.Global - pos.Value.Global).Magnitude;
                            if(dist < nearestDist)
                            {
                                nearestDist = dist;
                                nearestDid = sp.ID;
                            }

                            if (dist < 5.0)
                            {
                                if (!nearDIDs.Contains(sp.ID))
                                    nearDIDs.Add(sp.ID);
                            }
                        }

#if false
                        string nearbytxt = string.Empty;
                        for(int x=0; x<nearDIDs.Count; x++)
                        {
                            nearbytxt += $"{nearDIDs[x].ToString("X8")}";
                            if (x < (nearDIDs.Count - 1))
                                nearbytxt += ", ";
                        }

                        WriteToChat($"Nearest DIDs: {nearbytxt}");
#else
                        WriteToChat($"Nearest DID: {nearestDid.ToString("X8")}");
#endif
                    }
                };

                View["PerfDump"].Hit += delegate (object sender, EventArgs e)
                {
                    // flag next frame to dump a performance report anyway
                    ForcePerfDump = true;
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
                    (View["Enable"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "Enable", "1") != "0";
                    (View["Volume"] as HudHSlider).Position = int.Parse(ini.GetKeyString(Core.CharacterFilter.AccountName, "Volume", "75"));

                    (View["MusicEnable"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "MusicEnable", "1") != "0";
                    (View["MusicVolume"] as HudHSlider).Position = int.Parse(ini.GetKeyString(Core.CharacterFilter.AccountName, "MusicVolume", "35"));

                    (View["PortalMusicEnable"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "PortalMusicEnable", "1") != "0";
                }


                //Log("regen logos");
                RegenerateLogos();


                //Log("hook stuff");
                Core.RenderFrame += _Process;
                Core.CharacterFilter.Logoff += _CharacterFilter_Logoff;
                Core.ChatBoxMessage += _ChatBoxMessage;


                Startup_Internal();
            }
            catch (Exception ex)
            {
                Log($"Startup exception: {ex.Message}");
            }
        }

        bool ForcePerfDump = false;

        private void _ChatBoxMessage(object sender, ChatTextInterceptEventArgs e)
        {
            //Log($"CHAT: {e.Text}");

            Config.SoundSource snd = Config.FindSoundSourceText(e.Text);
            if(snd != null)
            {
                // we have no position data.. its a 2d effect
                Log($"wanna play something from text: {e.Text}");
                PlayFor2DNow(snd);
            }
        }

        private void Startup_Internal()
        {
            Shutdown_Internal();

            if (!PluginEnable)
                return;

            //Log("init audio");
            if (!Audio.Init(1000, dopplerscale: 0.135f))
                Log("Failed to initialize Audio");

            Music.Init();


            ReloadConfig();


            IsPortaling = true;
            StartPortalSong();// first time login portal deserves one
        }

        private void ReloadConfig()
        {
            PerfTimer pt = new PerfTimer();
            pt.Start();

            BVH.Reset();

            Config.Load("master.aca");

            Log($"Parsed {Config.Sources.Count} sound sources from master.aca");


            // reload static positions;  we will only keep what we registered from configs
            StaticPositions = LoadStaticPositions(false);
            Log($"Loaded {StaticPositions.Count} positions from static.dat");


            // flush static positions to BVH (there may be hundreds of thousands)
            foreach (StaticPosition sp in StaticPositions)
            {
                Config.SoundSourceStatic src = Config.FindSoundSourceStatic(sp.ID);
                if (src == null)
                    continue;

                BVH.Add(sp, src);
            }

            BVH.Process(0.00001);//flush with a nonzero dt?

            {
                int numBVHs, numNodes, numThings;
                BVH.GetTreeInfo(out numBVHs, out numNodes, out numThings);
                Log($"BVH INFO: numBVHs:{numBVHs}  numNodes:{numNodes}  numThings:{numThings}");//report so far lol
            }


            pt.Stop();
            Log($"config reload took {pt.Duration.ToString("#0.000")} sec");
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

        private static List<StaticPosition> LoadStaticPositions(bool forceAll)
        {
            List<StaticPosition> list = new List<StaticPosition>();

            ZipUtil zip = null;
            try
            {
                string filepath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "static.dat");
                using (Stream stream = new System.IO.Compression.GZipStream(File.OpenRead(filepath), System.IO.Compression.CompressionMode.Decompress))
                {
                    zip = new ZipUtil_Stream(stream);

                    int numEntries = zip.ReadInt();
                    for (int x = 0; x < numEntries; x++)
                    {
                        uint id = zip.ReadUInt();
                        Position pos = Position.FromStream(zip, true/*save filesize*/);

                        // if config doesnt reference then dont bother keeping
                        if (!forceAll && Config.FindSoundSourceStatic(id) == null)
                            continue;

                        list.Add(new StaticPosition(id, pos));
                    }
                }
            }
            finally
            {
                if(zip != null)
                    zip.Close();
            }

            return list;
        }

        private bool LogOff = false;
        private void _CharacterFilter_Logoff(object sender, LogoffEventArgs e)
        {
            if (PluginEnable)
            {
                if (LogOff)
                    return;

                Log("LOGOFF LOL");
                IsPortaling = true;
                StartPortalSong();

                LogOff = true;
            }


            using (INIFile ini = INIFile)
            {
                ini.WriteKey(Core.CharacterFilter.AccountName, "PluginEnable", (View["PluginEnable"] as HudCheckBox).Checked ? "1" : "0");

                ini.WriteKey(Core.CharacterFilter.AccountName, "Enable", (View["Enable"] as HudCheckBox).Checked ? "1" : "0");
                ini.WriteKey(Core.CharacterFilter.AccountName, "Volume", (View["Volume"] as HudHSlider).Position.ToString());

                ini.WriteKey(Core.CharacterFilter.AccountName, "MusicEnable", (View["MusicEnable"] as HudCheckBox).Checked ? "1" : "0");
                ini.WriteKey(Core.CharacterFilter.AccountName, "MusicVolume", (View["MusicVolume"] as HudHSlider).Position.ToString());

                ini.WriteKey(Core.CharacterFilter.AccountName, "PortalMusicEnable", (View["PortalMusicEnable"] as HudCheckBox).Checked ? "1" : "0");
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
                SmithInterop.GetCameraInfo(out p, out m);

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

        private long LoginCompleteTimestamp = 0;
        public double WorldTime
        {
            get
            {
                if(LoginCompleteTimestamp == 0)
                {
                    //Log("logincompletetimestamp is 0 so the answer is no");
                    return 0.0;
                }

                return PerfTimer.TimeBetween(LoginCompleteTimestamp, PerfTimer.Timestamp);
            }
        }

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

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        private bool DoesACHaveFocus()
        {
            return (GetActiveWindow() == Host.Decal.Hwnd);
        }


        double lastProcessTime = 1.0;

        private double lastRequestIdWorldTime = 0.0;


        private class QueryIdAttempt
        {
            public int tries = 0;
            public double lastTryWorldTime = MathLib.Infinity;
        }

        private Dictionary<int, QueryIdAttempt> queryAttempts = new Dictionary<int, QueryIdAttempt>();

        public void QueryForIdInfo(WorldObject obj)
        {
            // already have? nothing to do
            if (obj.HasIdData)
                return;

            // limit frequency of queries
            if ((WorldTime - lastRequestIdWorldTime) < 0.2)
                return;

            QueryIdAttempt q;
            if(!queryAttempts.TryGetValue(obj.Id, out q))
            {
                q = new QueryIdAttempt();
                queryAttempts.Add(obj.Id, q);
            }


#if true
            // if we have already tried a lot, reduce frequency
            double reqTime = 0.0;
            if (q.tries > 20)
                reqTime = 3.0;
            else if (q.tries > 5)
                reqTime = 1.0;

            if ((WorldTime - q.lastTryWorldTime) >= reqTime)
                return;
#endif


            Log($"REQUESTING INFORMATIONS FOR OBJ {obj.Id}  class:{obj.ObjectClass}  name:{obj.Name}  tries:{q.tries}");
            Host.Actions.RequestId(obj.Id);

            lastRequestIdWorldTime = WorldTime;

            q.tries++;
            q.lastTryWorldTime = WorldTime;
        }

        public WorldObject[] FrameObjects;

        double sayStuff = 0.0;
        private void Process(double dt, double truedt)
        {
            PerfTimer pt_process = new PerfTimer();
            pt_process.Start();


            //PerfTrack.StepReport[] lastFramePerfReport = PerfTrack.GetReport();

            PerfTrack.Reset();


            Audio.AllowSound = GetUserEnableAudio();

            if (DoesACHaveFocus())
                Audio.MasterVolume = GetUserVolume();
            else
                Audio.MasterVolume = 0.0;

            Music.EnableWorld = GetUserEnableMusic();
            Music.EnablePortal = GetUserAllowPortalMusic();
            Music.Volume = GetUserMusicVolume();


            PerfTrack.Start("Music");
            PerfTrack.Push();
            Music.Process(dt);
            PerfTrack.Pop();


            PortalSongHeat = Math.Max(0.0, PortalSongHeat - PortalSongHeatCooldown * dt);


            //Mat4 cameraMat = GetCameraMatrix();


            //Vec3 cameraPos = cameraMat.Position;//SmithInterop.Vector(Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First.RawCoordinates());

            Position cameraPos;
            Mat4 cameraMat;
            SmithInterop.GetCameraInfo(out cameraPos, out cameraMat);


            //WorldObject w;
            //w.RawCoordinates
            //Vector3Object v;

            //Decal.Adapter.Wrappers.D3DObj d;
            //d.OrientToCamera

            List<string> perfmsgs = new List<string>();

            
            {
                PerfTrack.Start("Internal Process");
                PerfTrack.Push();




                PerfTrack.Start("Get frame objects");
                FrameObjects = new List<WorldObject>(Core.WorldFilter.GetAll()).ToArray();



                // as long as we did a single Process after registering static objects, we shouldnt need to update anymore since nothing changes.
                // its a dynamic BVH but used in a static way.
#if false
                // this might be the WRONG PLACE to process BVH; just stuffin it in for now
                PerfTrack.Start("Process BVH");
                BVH.Process(dt);
#endif




                // only try to play ambient sounds if not portaling
                if (GetUserEnableAudio() && !IsPortaling)
                {


                    {

                        // dynamic objects


                        // now lets play
                        PerfTrack.Start("Sound sources dynamic");
                        PerfTrack.Push();


                        
                        // lets pre-filter objects list to compatible landblock and container
                        PerfTrack.Start("prefilter objects");
                        List<ShadowObject> objects = new List<ShadowObject>();
                        foreach(WorldObject _obj in FrameObjects)
                        {
                            ShadowObject obj = new ShadowObject(_obj);

                            if (!obj.Position.IsCompatibleWith(cameraPos))
                                continue;

                            // ignore items that were previously on ground but now picked up
                            if (obj.Values(LongValueKey.Container) != 0)
                                continue;

                            objects.Add(obj);
                        }


                        PerfTrack.Start("dynamic sources");
                        Config.SoundSourceDynamic[] dynamicSources = Config.FindSoundSourcesDynamic();
                        string perfmsg = $"Looping for {dynamicSources.Length} dynamic sources upon {objects.Count} objects  ({dynamicSources.Length * objects.Count} iterations)";
                        PerfTimer pt = new PerfTimer();
                        pt.Start();
                        PerfTrack.Start(perfmsg);

                        double time_distanceCheck = 0.0; int count_distanceCheck = 0;
                        double time_checkObject = 0.0; int count_checkObject = 0;

                        foreach (Config.SoundSourceDynamic src in dynamicSources)
                        {
                            List<WorldObject> finalObjects = new List<WorldObject>();

                            PerfTrack.Start($"Scan: {src.FriendlyDescription}");
                            PerfTrack.Push();

                            foreach(ShadowObject obj in objects)
                            {
                                long tm;

                                PerfTrack.Start("Distance check");
                                tm = PerfTimer.Timestamp;
                                count_distanceCheck++;
                                double dist = (cameraPos.Global - obj.Position.Global).Magnitude;
                                if (dist > src.Sound.maxdist)
                                {
                                    time_distanceCheck += PerfTimer.TimeBetween(tm, PerfTimer.Timestamp);
                                    continue;
                                }
                                time_distanceCheck += PerfTimer.TimeBetween(tm, PerfTimer.Timestamp);


                                PerfTrack.Start("CheckObject");
                                tm = PerfTimer.Timestamp;
                                count_checkObject++;
                                if (!src.CheckObject(obj))
                                {
                                    time_checkObject += PerfTimer.TimeBetween(tm, PerfTimer.Timestamp);
                                    continue;
                                }
                                time_checkObject += PerfTimer.TimeBetween(tm, PerfTimer.Timestamp);


#if false
                                if (obj.ObjectClass == ObjectClass.Npc)
                                {
                                    // filter by gender?  apparently we need to "appraise" first, if the key is not there
                                    if (!obj.HasIdData)//!obj.LongKeys.Contains(7))//(int)LongValueKey.Species))
                                    {
                                        if ((WorldTime - lastRequestIdWorldTime) > 0.2)// limit our queries
                                        {
                                            Log($"REQUESTING INFORMATIONS FOR OBJ {obj.Id}");
                                            Host.Actions.RequestId(obj.Id);

                                            lastRequestIdWorldTime = WorldTime;
                                        }

                                        // we should skip for now since the filter data we need is not yet present..
                                        // but just proceed for now since npc gender check isnt in config yet
                                        //continue;
                                    }


                                }

#endif


                                finalObjects.Add(obj.Object);
                            }
                            PerfTrack.Pop();


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

                            PerfTrack.Start($"PlayForObject for {finalObjects.Count} objs");
                            foreach (WorldObject obj in finalObjects)
                            {
                                PlayForObject(obj, src, volAdjust, minDistAdjust, maxDistAdjust);
                            }

                            PerfTrack.StopLast();
                        }


                        pt.Stop();
                        perfmsgs.Add($"{perfmsg}: {(pt.Duration*1000.0).ToString("#0.000")}msec");

                        //perfmsgs.Add($"initial ({count_initialCheck}): {(time_initialCheck*1000.0).ToString("#0")}msec total,   {(time_initialCheck/(double)count_initialCheck* 1000.0 * 1000.0).ToString("#0")}usec avg");
                        //perfmsgs.Add($"container ({count_containerCheck}): {(time_containerCheck * 1000.0).ToString("#0")}msec total,   {(time_containerCheck / (double)count_containerCheck * 1000.0 * 1000.0).ToString("#0")}usec avg");
                        perfmsgs.Add($"distance ({count_distanceCheck}): {(time_distanceCheck * 1000.0).ToString("#0")}msec total,   {(time_distanceCheck / (double)count_distanceCheck * 1000.0 * 1000.0).ToString("#0")}usec avg");
                        perfmsgs.Add($"checkObject ({count_checkObject}): {(time_checkObject * 1000.0).ToString("#0")}msec total,   {(time_checkObject / (double)count_checkObject * 1000.0 * 1000.0).ToString("#0")}usec avg");


                        PerfTrack.Pop();
                    }




                    // static objects
                    {
                        long tm_staticobject = PerfTimer.Timestamp;

                        PerfTrack.Start("Static objects");
                        PerfTrack.Push();

#if true
                        // BVH METHOD


                        // dispatch
                        PerfTrack.Start("Dispatch");
                        foreach (BVH.BVHEntry_StaticPosition pos in BVH.QueryStaticPositions(cameraPos))
                            PlayForPosition(pos.StaticPosition.Position, pos.Source);
#else
                        // OLD BRUTE FORCE METHOD

                        // build list of final candidates
                        PerfTrack.Start("Build candidates");
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
                        PerfTrack.Start("Dispatch");
                        foreach (StaticPosition pos in finalPositions)
                        {
                            Config.SoundSourceStatic src = Config.FindSoundSourceStatic(pos.ID);
                            if (src == null)
                                continue;
                            
                            double dist = (cameraPos.Global - pos.Position.Global).Magnitude;
                            if (dist >= src.Sound.maxdist)
                                continue;

                            PlayForPosition(pos.Position, src);
                        }

#endif
                        PerfTrack.Pop();


                        perfmsgs.Add($"static objects: {(PerfTimer.TimeBetween(tm_staticobject, PerfTimer.Timestamp)*1000.0).ToString("#0.00")}msec");
                    }



                    // static positions
                    {
                        // dispatch static positions
                        PerfTrack.Start("Static positions");

                        foreach (Config.SoundSourcePosition src in Config.FindSoundSourcesPosition(cameraPos))
                        {
                            double dist = (cameraPos.Global - src.Position.Global).Magnitude;
                            if (dist >= src.Sound.maxdist)
                                continue;

                            PlayForPosition(src.Position, src);
                        }
                    }
                }

                PerfTrack.Pop();
            }



            sayStuff += dt;
            if (sayStuff >= 0.2)
            {
                sayStuff = 0.0;

                (View["Info"] as HudStaticText).Text =
                    $"fps:{(int)(1.0 / dt)}  cpu:{(int)(lastProcessTime / dt * 100.0)}%  process:{(int)(lastProcessTime * 1000.0)}msec  mem:{((double)Audio.MemoryUsageBytes / 1024.0 / 1024.0).ToString("#0.0")}mb   worldtime:{WorldTime.ToString(MathLib.ScalarFormattingString)}\n" +
                    $"ambs:{ActiveAmbients.Count}  channels:{Audio.ChannelCount}  sounds(RAM):{Audio.SoundCount_RAM}  sounds(stream):{Audio.SoundCount_Stream}\n" +
                    $"cam:{cameraPos.Global}  lb:{cameraPos.Landblock.ToString("X8")}\n" +
                    $"portalsongheat:{(MathLib.Clamp(PortalSongHeat / PortalSongHeatMax) * 100.0).ToString("0")}%  {PortalSongHeat.ToString(MathLib.ScalarFormattingString)}";
            }


            // kill/forget sounds for objects out of range?
            PerfTrack.Start("Reject ambients");
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
                        discardReason = $"wrong dungeon  camera:{cameraPos.DungeonID}  vs  amb:{a.Position.DungeonID}";
                }

                if (discardReason == null)
                {
                    double dist = (cameraPos.Global - a.Position.Global).Magnitude;
#if true
                    // if FinalMaxDist is adjusted to be lower than what's in Source, then engine will keep recreating ambients every frame
                    double maxDist = Math.Max(a.FinalMaxDist, a.Source.Sound.maxdist);

                    // fudge dist a bit?
                    maxDist += 2.0;

                    if (dist > maxDist)
                        discardReason = $"bad dist {dist} > {maxDist}";
#else
                    float minDist, maxDist;
                    a.Channel.channel.get3DMinMaxDistance(out minDist, out maxDist);

                    // fudge dist a bit?
                    maxDist += 2.0f;

                    if (dist > (double)maxDist)
                        discardReason = $"bad dist {dist} > {maxDist}";
#endif
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
                        Log($"removing for weenie {(a as ObjectAmbient).WeenieID}: {discardReason}");
                    else if (a is StaticAmbient)
                        Log($"removing for static {(a as StaticAmbient).Position}: {discardReason}");



                    a.Stop();

                    ActiveAmbients.RemoveAt(x);
                    x--;
                }
                else
                {

                }
            }


            // process ambients
            PerfTrack.Start("Process ambients");
            PerfTrack.Push();
            foreach (Ambient a in ActiveAmbients)
                a.Process(dt);
            PerfTrack.Pop();


            // lets sync time positions for active ambient loopables that want to
            PerfTrack.Start("Sync ambients");
            List<string> alreadyDone = new List<string>();
            for (int x = 0; x < ActiveAmbients.Count - 1; x++)
            {
                Ambient aa = ActiveAmbients[x];
                if (!aa.IsPlaying)
                    continue;

                if (!aa.Source.Sound.sync)
                    continue;

                if (alreadyDone.Contains(aa.Source.Sound.file))
                    continue;

                alreadyDone.Add(aa.Source.Sound.file);


                for (int y = 1; y < ActiveAmbients.Count; y++)
                {
                    Ambient ab = ActiveAmbients[y];
                    if (!ab.IsPlaying)
                        continue;

                    // if not playing same sound, skip
                    if (!aa.Source.Sound.file.Equals(ab.Source.Sound.file, StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    // do we want to sync sound timestamps? skip now if not
                    //if (no)
                    //continue;


                    // copy A timestamp to B
                    /*uint posFmod;
                    aa.Channel.channel.getPosition(out posFmod, FMOD.TIMEUNIT.PCM);
                    ab.Channel.channel.setPosition(posFmod, FMOD.TIMEUNIT.PCM);*/
                    ab.SamplePosition = aa.SamplePosition;
                }
            }




            // handle sound cache logic
            PerfTrack.Start("Cache");
            {
                // build list of all currently playing sounds
                List<Audio.Sound> activeSounds = new List<Audio.Sound>();
                foreach (Audio.Channel channel in Audio.GetAllChannels())
                    if (channel.IsPlaying && !activeSounds.Contains(channel.Sound))
                        activeSounds.Add(channel.Sound);


                // refresh request time for all active sounds (cache timeout should only start when stop playing)
                foreach (Audio.Sound snd in activeSounds)
                {
                    SoundCacheEntry sce;
                    if (!SoundCache.TryGetValue(snd, out sce))
                    {
                        Log($"uh oh, no cache entry for an active sound");
                        continue;
                    }

                    sce.RequestTime = WorldTime;
                }

                // check all loaded sounds to see if they should be unloaded
                List<Audio.Sound> soundsToUnload = new List<Audio.Sound>();
                foreach(Audio.Sound snd in Audio.GetAllSounds())
                {
                    // if active, skip logic
                    if (activeSounds.Contains(snd))
                        continue;

                    SoundCacheEntry sce;
                    if (!SoundCache.TryGetValue(snd, out sce))
                    {
                        Log($"uh oh, no cache entry for an existing sound");
                        continue;
                    }

                    // if we want perma-cached then skip
                    //if (sce.Cache < 0.0)
                        //continue;

                    // if we havent been requested past our desired cache time then add to unload list
                    double timeSincePlay = WorldTime - sce.RequestTime;
                    if (timeSincePlay > 5.0)//sce.Cache)
                        soundsToUnload.Add(snd);
                }

                // wipe out old sounds
                foreach(Audio.Sound snd in soundsToUnload)
                {
                    Audio.CleanupSound(snd);
                    SoundCache.Remove(snd);
                }
            }




            (View["ProxyMap"] as HudProxyMap).Invalidate();// draw every frame.. realtime map



            // always look for what should be playing
            PerfTrack.Start("Try music");
            TryMusic();



            // Z is up

            PerfTrack.Start("Audio.Process");
            Audio.Process(dt, truedt, cameraPos.Global, Vec3.Zero, cameraMat.Up, cameraMat.Forward);


            pt_process.Stop();
            lastProcessTime = pt_process.Duration;



            //PerfTrack.Pop();
            PerfTrack.StopLast();



            // dont do any more logic after this point that could influence framerate (should be tracked in performance)


            // anything that decreases frames below XX should be reported
            double fps = (1.0 / lastProcessTime);
            if(fps < 20.0 || ForcePerfDump)
            {
                string title;
                if (ForcePerfDump)
                    title = $"process report";
                else
                    title = $"process is SLOW";

                ForcePerfDump = false;


                // dump perf report?
                Log($"------------ {title}: {(lastProcessTime * 1000.0).ToString("#0.0")}msec");
                foreach (PerfTrack.StepReport step in PerfTrack.GetReport())
                {
                    // only report above X microseconds
                    if ((step.Duration * 1000.0 * 1000.0) < 10.0)
                        continue;

                    string str = string.Empty;
                    for (int x = 0; x < step.Level; x++)
                        str += "--";

                    str += $" {step.Name}: {(step.Duration*1000.0).ToString("#0.00")}msec";

                    Log(str);
                }
                Log("------------");



#if true
                foreach (string str in perfmsgs)
                    Log(str);

                Log("------------");
#endif
            }
        }

        private static Dictionary<Audio.Sound, SoundCacheEntry> SoundCache = new Dictionary<Audio.Sound, SoundCacheEntry>();

        private class SoundCacheEntry
        {
            public double RequestTime;
        }

        public static Audio.Sound GetOrLoadSound(string name, Audio.DimensionMode mode, bool looping, bool filestream)
        {
            if (Instance == null)
                return null;

            Audio.Sound snd = Audio.GetSound(name, mode, looping);

            if (snd == null)
            {
                PerfTrack.Start($"Load sound {name}");

                try
                {
                    PerfTimer pt = new PerfTimer();
                    pt.Start();

                    if (filestream)
                    {
                        Log($"Creating file stream: {name}");
                        string filepath = GenerateDataPath(name);
                        if (!File.Exists(filepath))
                            return null;

                        snd = Audio.GetFileStream(name, filepath, mode, looping);
                    }
                    else
                    {
                        Log($"Loading file to RAM: {name}");
                        byte[] buf = PluginCore.ReadDataFile(name);
                        if (buf == null || buf.Length == 0)
                            return null;

                        snd = Audio.GetSound(name, buf, mode, looping);
                    }

                    pt.Stop();
                    Log($"loading took {(pt.Duration*1000.0).ToString("#0.0")}msec");
                }
                catch (Exception ex)
                {
                    Log($"GetOrLoadSound() Cant load {name}: {ex.Message}");

                    return null;
                }
            }

            if (snd == null)
                return null;

            SoundCacheEntry sce;
            if(!SoundCache.TryGetValue(snd, out sce))
            {
                sce = new SoundCacheEntry();
                SoundCache.Add(snd, sce);
            }

            sce.RequestTime = Instance.WorldTime;

            return snd;
        }

        public abstract class Ambient
        {
            protected Audio.Channel Channel = null;
            public readonly Config.SoundSource Source;

            public double VolScale = 1.0;
            public double MinDistScale = 1.0;
            public double MaxDistScale = 1.0;

            public double FinalVolume
            {
                get
                {
                    return Source.Sound.vol * VolScale;
                }
            }

            public double FinalMinDist
            {
                get
                {
                    return Source.Sound.mindist * MinDistScale;
                }
            }

            public double FinalMaxDist
            {
                get
                {
                    return Source.Sound.maxdist * MaxDistScale;
                }
            }

            public bool IsSong
            {
                get
                {
                    return (Source.Sound.mode == Config.SoundMode.Song);
                }
            }

            public int ChannelID
            {
                get
                {
                    if (Channel == null)
                        return -1;

                    return Channel.ID;
                }
            }

            public bool IsPlaying
            {
                get
                {
                    return (Channel != null && Channel.IsPlaying);
                }
            }

            public uint SamplePosition
            {
                get
                {
                    if (Channel == null)
                        return 0;

                    uint pcm;
                    Channel.channel.getPosition(out pcm, FMOD.TIMEUNIT.PCM);

                    return pcm;
                }

                set
                {
                    if (Channel == null)
                        return;

                    Channel.channel.setPosition(value, FMOD.TIMEUNIT.PCM);
                }
            }

            public bool Is3D
            {
                get
                {
                    return (Source.Sound.mode == Config.SoundMode._3D ||
                        Source.Sound.mode == Config.SoundMode.Hybrid);
                }
            }

            private bool _WantSongPlay = false;
            public bool WantSongPlay
            {
                get
                {
                    return _WantSongPlay;
                }
            }

            protected void Play()
            {
                // if we are song, this is handled elsewhere. just track if we WANT to be playing
                if(IsSong)
                {
                    if (!_WantSongPlay)
                    {
                        Log("WE WANT SONG");
                        _WantSongPlay = true;
                    }

                    return;
                }


                Log($"we wanna play {Source.Sound.file}");

                // if channel exists, kill it?
                if (Channel != null)
                {
                    Log($"killing internal ambient channel for another play");
                    Channel.Stop();
                    Channel = null;
                }


                {

                    // get sound

                    Audio.DimensionMode mode;
                    if (Source.Sound.mode == Config.SoundMode._3D || Source.Sound.mode == Config.SoundMode.Hybrid)
                        mode = Audio.DimensionMode._3DPositional;
                    else
                        mode = Audio.DimensionMode._2D;

                    Audio.Sound snd = GetOrLoadSound(Source.Sound.file, mode, Source.Sound.looping, false);
                    if (snd == null)
                        return;

                    Channel = Audio.PlaySound(snd, true);
                        
                    Channel.Volume = 0.0;
                    Channel.SetTargetVolume(FinalVolume, Source.Sound.fade);

                    if (Source.Sound.randomstart)
                        Channel.Time = MathLib.random.NextDouble() * snd.Length;


                    if (Is3D)
                    {
                        Channel.SetPosition(Position.Global, Vec3.Zero);
                        Channel.SetMinMaxDistance(FinalMinDist, FinalMaxDist);
                    }

                    Channel.Play();
                }


            }

            // should we support fade-out?  was going to be cant really see an existing situation where it would be desired
            public void Stop()
            {
                // if we are song, this is handled elsewhere. just track if we WANT to be playing
                if(IsSong)
                {
                    if (_WantSongPlay)
                    {
                        Log("WE DONT WANT SONG");
                        _WantSongPlay = false;
                    }

                    return;
                }


                if (Channel != null)
                {
                    Channel.Stop();
                    Channel = null;
                }
            }

            private double TimeUntilCheck;

            protected Ambient(Config.SoundSource _Source)
            {
                Source = _Source;

                TimeUntilCheck = MathLib.random.NextDouble() * Source.Sound.interval;
            }

            public abstract Position Position
            {
                get;
            }

            public virtual void Process(double dt)
            {
                // NOTE:   Channel is null for Song types.  there are overrides to handle that via Music class.
                //         but we still use the Ambient  Play/Stop   for logic to trigger music.


                // clear possible dangling channel ref
                if (Channel != null && !Channel.IsPlaying)
                {
                    _WantSongPlay = false;
                    Channel = null;
                }


                // should we be playing?
                if (Channel == null)
                {
                    TimeUntilCheck -= dt;
                    if (TimeUntilCheck <= 0.0)
                    {
                        TimeUntilCheck = Source.Sound.interval;


                        if (MathLib.random.NextDouble() <= Source.Sound.chance)
                        {
                            Play();
                        }
                    }
                }


                // if playing, update values
                if(Channel != null)
                {
                    Channel.SetTargetVolume(FinalVolume, Source.Sound.fade);

                    if (Is3D)
                    {
                        Channel.SetPosition(Position.Global, Vec3.Zero);
                        Channel.SetMinMaxDistance(FinalMinDist, FinalMaxDist);
                    }
                }
            }
        }

        public class ObjectAmbient : Ambient
        {
            public readonly int WeenieID;

            public ObjectAmbient(Config.SoundSource _Source, int _WeenieID)
                : base(_Source)
            {
                WeenieID = _WeenieID;
            }

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
            public readonly Position StaticPosition;

            public StaticAmbient(Config.SoundSource _Source, Position _StaticPosition)
                : base(_Source)
            {
                StaticPosition = _StaticPosition;
            }

            public override Position Position
            {
                get
                {
                    return StaticPosition;
                }
            }

        }

        public List<Ambient> ActiveAmbients = new List<Ambient>();


        // this is a 1-shot event.. probably triggered via text
        public void PlayFor2DNow(Config.SoundSource src)
        {
#if false
            // if its a song then divert
            if(src.Sound.mode == Config.SoundMode.Song)
            {
                Music.Play(src.Sound, false/*probably not!*/);
                return;
            }
#endif


            Audio.Sound snd = GetOrLoadSound(src.Sound.file, Audio.DimensionMode._2D, false/*src.Sound.looping*/, false);
            if (snd == null)
                return;

            Audio.Channel channel = Audio.PlaySound(snd, true);

            channel.Volume = src.Sound.vol;

            channel.Play();

            // i think our audio engine GC will clean up :)
        }

        public void PlayForPosition(Position pos, Config.SoundSource src)
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
                    if (sa.Source.Sound.file.Equals(src.Sound.file, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //Log("playforposition bailed on same filename");
                        return;
                    }


                    // changing sounds? kill existing
                    //sa.Stop();

                    //ActiveAmbients.Remove(sa);

                    //Log("playforposition removed an ambient");

                    //break;
                }
            }



            // start new sound
            StaticAmbient newsa = new StaticAmbient(src, pos);

            ActiveAmbients.Add(newsa);


            Log($"added static ambient {src.Sound.file} at {pos}");
        }

        public void PlayForObject(WorldObject obj, Config.SoundSource src, double volScale, double mindistScale, double maxdistScale)
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
                    if (oa.Source.Sound.file.Equals(src.Sound.file, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // do we want to update properties?
                        oa.VolScale = volScale;
                        oa.MinDistScale = mindistScale;
                        oa.MaxDistScale = maxdistScale;

                        return;
                    }

                    // changing sounds? kill existing
                    //oa.Stop();

                    //ActiveAmbients.Remove(oa);

                    //break;
                }
            }


            // start new sound
            ObjectAmbient newoa = new ObjectAmbient(src, obj.Id);

            newoa.VolScale = volScale;
            newoa.MinDistScale = mindistScale;
            newoa.MaxDistScale = maxdistScale;

            ActiveAmbients.Add(newoa);

            Log($"added weenie ambient {src.Sound.file} from ID {obj.Id}");
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

        /*public static string PortalSongFilename
        {
            get
            {
                return Config.PortalSound?.file;
            }
        }*/
        private void StartPortalSong()
        {
            if (PortalSongHeat >= PortalSongHeatMax)
            {
                Music.Stop();// ensure existing music is stopped even if we decide not to play
                return;
            }

            Music.Play(Config.PortalSound, true);//Music.Play(PortalSongFilename, true);
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

                // dont have to do here..  Process will pick it up
                //TryMusic();

                IsPortaling = false;
            }
        }

        private void TryMusic()
        {
            // try to play something.. if nothing desired then kill it
            if (!_TryMusic())
                Music.Stop();
        }

        private bool _TryMusic()
        {
            if (!GetUserEnableMusic())
                return false;

            //Position? plrPos = Position.FromObject(Player);
            //if (plrPos.HasValue)
            Position camPos;
            Mat4 camMat;
            SmithInterop.GetCameraInfo(out camPos, out camMat);
            {

                // "placeable" song triggers should take priority over dungeon or default sources
                double closestDist = MathLib.Infinity;
                Ambient closestAmb = null;
                foreach (Ambient a in ActiveAmbients)
                {
                    if (!a.IsSong || !a.WantSongPlay)
                        continue;

                    double dist = (a.Position.Global - camPos.Global).Magnitude;

                    if(dist < closestDist)
                    {
                        closestDist = dist;
                        closestAmb = a;
                    }
                }


                Config.SoundAttributes musicSound = null;

                // if we found a placed trigger, select it straight away
                if (closestAmb != null)
                    musicSound = closestAmb.Source.Sound;
                else
                {
                    // check for defaults?


                    // for now (testing) just start playing a song depending on terrain or inside lol
                    if (camPos.IsTerrain)
                    {
                        //Music.Play("ac_anotherorch.mp3", false);
                        //return true;

                    }
                    else
                    {
                        // check for dungeon music?
                        Config.SoundSourceDungeon src = Config.FindSoundSourceDungeonSong(camPos.DungeonID);
                        if (src != null)
                        {
                            musicSound = src.Sound;
                            //Music.Play(src.Sound, false);

                            //return true;
                        }
                        else
                        {
                            //Music.Play("ac_someoffbeat.mp3", false);

                            //return true;
                        }

                    }
                }


                // if we found something to use, fire it off
                if(musicSound != null)
                {
                    Music.Play(musicSound, false);
                    return true;
                }

            }


            // if we are portaling, keep whatever might be active, active..
            if (IsPortaling)
                return true;


            // nothing played
            return false;
        }

        [BaseEvent("LoginComplete", "CharacterFilter")]
        private void CharacterFilter_LoginComplete(object sender, EventArgs e)
        {
            WriteToChat($"Startup");


            // start our world timer
            LoginCompleteTimestamp = PerfTimer.Timestamp;
        }

        /// <summary>
        /// This is called when the plugin is shut down. This happens only once.
        /// </summary>
        protected override void Shutdown()
        {
            Shutdown_Internal();

            //Log("unhook stuff");
            Core.ChatBoxMessage -= _ChatBoxMessage;
            Core.CharacterFilter.Logoff -= _CharacterFilter_Logoff;
            Core.RenderFrame -= _Process;


            Log("----------------------------------------------------------------------");
            Log("                           ACAudio Shutdown");
            Log("----------------------------------------------------------------------");
        }

        private void Shutdown_Internal()
        {
            Music.Shutdown();
            Audio.Shutdown();

            SoundCache.Clear();
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
            if (!PluginEnable)
                return;

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
