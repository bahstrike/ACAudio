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

        public bool IsPlayingPortalMusic
        {
            get
            {
                return Music.IsPlayingName(PortalSongFilename);
            }
        }

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

                    /*string filepath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "data/ac_anotherorch.mp3");
                    Audio.Sound snd = Audio.GetSound("test", File.ReadAllBytes(filepath), Audio.DimensionMode._2D, false);

                    Audio.PlaySound(snd);*/
                };

                View["Coords"].Hit += delegate (object sender, EventArgs e)
                {
                    WriteToChat("WEEE");

                    Log($"Position.FromLocal({Position.FromObject(Player)}),");
                };

                (View["Enable"] as HudCheckBox).Change += delegate (object sender, EventArgs e)
                {

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



                StartPortalSong();// first time login portal deserves one

                LoadStaticPositions();
            }
            catch (Exception ex)
            {
                Log($"Startup exception: {ex.Message}");
            }
        }

        public class StaticPosition
        {
            public uint ID;
            public Position Position;
        }

        public List<StaticPosition> StaticPositions = new List<StaticPosition>();

        private void LoadStaticPositions()
        {
            StaticPositions.Clear();

            ZipUtil zip = new ZipUtil_Stream(GetEmbeddedFile("static.dat"));

            int numEntries = zip.ReadInt();
            for(int x=0; x<numEntries; x++)
            {
                StaticPosition entry = new StaticPosition();

                entry.ID = zip.ReadUInt();
                entry.Position = Position.FromStream(zip);

                StaticPositions.Add(entry);


                //break;//HAXXXXXXXXXXXX    ONLY TEST 1 FOR NOW
            }

            zip.Close();
        }

        private bool LogOff = false;
        private void _CharacterFilter_Logoff(object sender, LogoffEventArgs e)
        {
            if (LogOff)
                return;

            Log("LOGOFF LOL");
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


            WorldObject w;
            //w.RawCoordinates
            Vector3Object v;

            Decal.Adapter.Wrappers.D3DObj d;
            //d.OrientToCamera


            sayStuff += dt;
            if (sayStuff >= 0.2)
            {
                sayStuff = 0.0;

                //double maxDist = 35.0;

                /*List<WorldObject> players = FilterByDistance(Core.WorldFilter.GetByObjectClass(ObjectClass.Player), playerPos, maxDist);
                List<WorldObject> npcs = FilterByDistance(Core.WorldFilter.GetByObjectClass(ObjectClass.Npc), playerPos, maxDist);
                List<WorldObject> monsters = FilterByDistance(Core.WorldFilter.GetByObjectClass(ObjectClass.Monster), playerPos, maxDist);
                List<WorldObject> allobj = FilterByDistance(Core.WorldFilter.GetAll(), playerPos, maxDist);*/

                /*if(testObj != null)
                    WriteToChat($"dist to sound: {(SmithInterop.Vector(testObj.RawCoordinates()) - cameraMat.Position).Magnitude.ToString("0.00")}");*/




                WorldObject player = Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First;
                //Log($"behavior:{player.Behavior}  boolkeys:{player.BoolKeys.Count}   doublekeys:{player.DoubleKeys.Count}   gamedataflags1:{player.GameDataFlags1}   longkeys:{player.LongKeys.Count}   physicsdataflags:{player.PhysicsDataFlags}   stringkeys:{player.StringKeys.Count}   type:{player.Type}");
                /*Log("------------------- LONG KEYS ----------------");
                foreach(int i in player.DoubleKeys)
                {
                    Log($"{(DoubleValueKey)i} = {player.Values((DoubleValueKey)i)}");
                }*/

                //Log($"{Host.Actions.CombatMode}");


                //WriteToChat($"{InstanceNumber}  players:{players.Count}  npcs:{npcs.Count}  monsters:{monsters.Count}  playerPos:{playerPos}");

                //WriteToChat(cameraMat.ToString());

                //WriteToChat($"{InstanceNumber} blah {Core.WorldFilter.GetAll().Count} objects  {Core.WorldFilter.GetLandscape().Count} landscape   player: {coords.NorthSouth},{coords.EastWest}");



                // only try to play ambient sounds if not portaling
                if (GetUserEnableAudio() && !IsPlayingPortalMusic)
                {


                    {

                        // dynamic objects


                        // lifestone
                        {
                            double vol = 1.0;
                            double minDist = 5.0;
                            double maxDist = 35.0;
                            foreach (WorldObject obj in Core.WorldFilter.GetAll())
                            {
                                Position? objPos = Position.FromObject(obj);
                                if (!objPos.HasValue || !objPos.Value.IsCompatibleWith(cameraPos))
                                    continue;

                                double dist = (cameraPos.Global - objPos.Value.Global).Magnitude;
                                if (dist > maxDist)
                                    continue;

                                if (obj.ObjectClass == ObjectClass.Lifestone)
                                    PlayForObject(obj, "lifestone.ogg", vol, minDist, maxDist);
                            }
                        }


                        // portals (dynamic; in case too many are around)
                        {
                            double vol = 1.0;
                            double minDist = 5.0;
                            double maxDist = 35.0;

                            List<WorldObject> portals = new List<WorldObject>();
                            foreach (WorldObject obj in Core.WorldFilter.GetAll())
                            {
                                Position? objPos = Position.FromObject(obj);
                                if (!objPos.HasValue || !objPos.Value.IsCompatibleWith(cameraPos))
                                    continue;

                                double dist = (cameraPos.Global - objPos.Value.Global).Magnitude;
                                if (dist > maxDist)
                                    continue;

                                if (obj.ObjectClass == ObjectClass.Portal)
                                    portals.Add(obj);
                            }


                            // if we got more than like 3 then tone em back
                            if (portals.Count > 3)
                            {
                                vol *= 0.4;
                                minDist *= 0.9;
                                maxDist *= 0.5;
                            }

                            foreach (WorldObject obj in portals)
                            {
                                // re-check with updated maxDist
                                Position? objPos = Position.FromObject(obj);
                                if (!objPos.HasValue || !objPos.Value.IsCompatibleWith(cameraPos))
                                    continue;

                                double dist = (cameraPos.Global - objPos.Value.Global).Magnitude;
                                if (dist > maxDist)
                                    continue;

                                PlayForObject(obj, "portal.ogg", vol, minDist, maxDist);
                            }

                        }


                    }


                    // static positions
                    {

                        double vol = 0.125;
                        double minDist = 5.0;
                        double maxDist = 15.0;

                        // candle sounds lol

#if false
                        // add direct coordinates if we can pass inside object..  if standing on top, add to list below
                        List<Position> positions = new List<Position>(new Position[] {
                        // town network
                        Position.FromLocal(0x00070157, 74.9212, -83.2331, 0.0050),
                        Position.FromLocal(0x00070157, 83.3264, -75.0515, 0.0050),
                        Position.FromLocal(0x00070157, 83.2375, -64.9197, 0.0050),
                        Position.FromLocal(0x00070157, 74.9133, -56.6920, 0.0050),
                        Position.FromLocal(0x00070157, 64.8490, -56.8161, 0.0050),
                        Position.FromLocal(0x00070157, 56.6511, -65.0491, 0.0050),
                        Position.FromLocal(0x00070157, 56.7121, -74.9459, 0.0050),
                        Position.FromLocal(0x00070157, 65.0106, -83.3552, 0.0050),

                        // town network annex
                        Position.FromLocal(0x00070157, 67.6610, -166.0082, -5.9950),
                        Position.FromLocal(0x00070157, 72.3911, -166.0279, -5.9950),
                        Position.FromLocal(0x00070157, 75.9901, -162.3565, -5.9950),
                        Position.FromLocal(0x00070157, 75.9803, -157.6755, -5.9950),
                        Position.FromLocal(0x00070157, 64.0190, -157.6426, -5.9950),
                        Position.FromLocal(0x00070157, 64.0199, -162.3036, -5.9950),
                        });


                        // i was prolly on top of candle post for these; might wanna subtract some Z...
                        // assuming around 6-foot human toon..  a chest (origin)->foot is estimated at perhaps
                        // 50" or 1.27 meters
                        Vec3 adjust = new Vec3(0.0, 0.0, -1.27);
                        foreach (Position pos in new Position[]
                        {
                        // cragstone
                        /*new Vec3(175.1502, 113.1925, 34.2100),// in town
                        new Vec3(158.3660, 150.9660, 34.2100),// in town
                        new Vec3(162.6672, 63.5380, 34.2100), // in town
                        new Vec3(160.2169, 36.2300, 34.2100), // in town
                        new Vec3(149.8169, 21.0506, 34.2100), // in town
                        new Vec3(19.2658, 101.0283, 72.2100), // in town
                        new Vec3(41.4031, 61.4495, 56.2100), // in town

                        new Vec3(81.0427, 89.3640, 56.2100),//town up hill

                        new Vec3(151.4200, 172.2459, 36.2050),// meeting hall
                        new Vec3(148.5136, 175.7152, 36.2184),// meeting hall*/


                        // holtburg
                        Position.FromLocal(0xAAB4001C, 83.1785, 80.5087, 57.2859),
                        Position.FromLocal(0xA9B40035, 155.8313, 114.7828, 68.2100),
                        Position.FromLocal(0xA9B4002E, 133.4774, 136.2814, 68.2100),
                        Position.FromLocal(0xA9B4001F, 89.4780, 152.0241, 66.7978),
                        Position.FromLocal(0xA9B4000E, 41.6327, 132.6815, 68.2100),
                        Position.FromLocal(0xA9B40006, 18.5117, 126.5471, 68.2100),
                        Position.FromLocal(0xA9B40021, 105.9395, 17.0463, 96.2100),
                        Position.FromLocal(0xA9B4002A, 139.0259, 28.3847, 96.2100),
                        Position.FromLocal(0xA9B40032, 156.8629, 32.3431, 98.0754),
                        Position.FromLocal(0xA9B40032, 153.2320, 35.5311, 98.1161),
                        })
                        {
                            Position tmp = pos;
                            tmp.Local += adjust;

                            positions.Add(tmp);
                        }
#endif


                        // build list of final candidates
                        List<StaticPosition> finalPositions = new List<StaticPosition>();
                        foreach (StaticPosition pos in StaticPositions)
                        {
                            if (!pos.Position.IsCompatibleWith(cameraPos))
                                continue;

                            double dist = (cameraPos.Global - pos.Position.Global).Magnitude;

                            if (dist > maxDist)
                            {
                                //Log($"bad dist  {dist} > {maxDist}     cam:{cameraPos.Global}  VS pos:{pos.Position.Global}  ");
                                continue;
                            }

                            //Log("WE KEEP");

                            finalPositions.Add(pos);
                        }

                        
                        // if we got more than like 3 then tone em back
                        if (finalPositions.Count > 3)
                        {
                            vol *= 0.4;
                            minDist *= 0.9;
                            maxDist *= 0.5;
                        }


                        // dispatch
                        foreach (StaticPosition pos in finalPositions)
                            // hardcoded
                            PlayForPosition(pos.Position, "candle.ogg", vol, minDist, maxDist);
                    }
                }

#if true

                (View["Info"] as HudStaticText).Text =
                    $"fps:{(int)(1.0/dt)}  process:{(int)(lastProcessTime * 1000.0)}msec  \n" +
                    $"ambs:{ActiveAmbients.Count}  channels:{Audio.ChannelCount}  cam:{cameraPos.Global}  lb:{cameraPos.Landblock.ToString("X8")}\n" +
                    $"portalsongheat:{(MathLib.Clamp(PortalSongHeat / PortalSongHeatMax) * 100.0).ToString("0")}%  {PortalSongHeat.ToString(MathLib.ScalarFormattingString)}";
#else
                UtilityBelt.Lib.Frame frame = UtilityBelt.Lib.Frame.Get(Host.Actions.Underlying.SmartboxPtr() + 8);
                uint lb = frame.landblock;//(uint)player.Values(LongValueKey.Landblock);

                int lbX = (int)((lb & 0xFF000000) >> 24);
                int lbY = (int)((lb & 0x00FF0000) >> 16);
                int area = (int)((lb & 0x0000FF00) >> 8);
                int cell = (int)(lb & 0x000000FF);  // 1-based
                int cellX = (cell - 1) / 8;
                int cellY = (cell - 1) % 8;




                const double cellScale = 24.0;  // terrain landblock is 8x8 -> 192x192   therefore cellX * 24.0  is local coord in landblock
                Vec2 cellLL = new Vec2((double)cellX * cellScale, (double)cellY * cellScale);

                Vec2 landblockLL = new Vec2((double)lbX * 192.0, (double)lbY * 192.0); // terrain landblock is 192x192

                Vec2 globalLL = landblockLL;// + cellLL;


                Vec3 globalCamPos = globalLL.At3DZ(0.0) + cameraPos;

                // SW = lower left = negative
                // NE = upper right = positive
                Vec2 cornerSW = new Vec2(-101.95, -101.95); // landblock 0,0
                Vec2 cornerNE = new Vec2(102.05, 102.05); // landblock 256,256   (note may need to subtract landblock size to get SW corner of last landblock)




                //Log($"activeobjs:{ActiveObjectAmbients.Count}  audiochannels:{Audio.ChannelCount}");
                (View["Info"] as HudStaticText).Text =
                    $"ambs:{ActiveAmbients.Count}  channels:{Audio.ChannelCount}  cam:{cameraPos}  lb({(LandblockInside(lb) ? "I" : "T")}):0x{lb.ToString("X8")}\n" +
                    $"lb:{lbX},{lbY}  area:{area}  cell:{cellX},{cellY}\n" +
                    $"landblockLL:{landblockLL}\ncellLL:{cellLL}\nglobalLL:{globalLL}\n" +
                    $"globalCamPos:{globalCamPos}";//    camDist:{camDist}\n" +
                    //$"quat:{frame.qw},{frame.qx},{frame.qy},{frame.qz}";
#endif
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
                    if (IsPlayingPortalMusic)
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
                    if (dist > 2.4)// erm.. magic numbers for preventing sound source overlap
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
            Audio.Sound snd = Audio.GetSound(filename, ReadDataFile(filename), Audio.DimensionMode._3DPositional, true);
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
            Audio.Sound snd = Audio.GetSound(filename, ReadDataFile(filename), Audio.DimensionMode._3DPositional, true);
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

        public byte[] ReadDataFile(string filename)
        {
            return System.IO.File.ReadAllBytes(GenerateDataPath(filename));
        }

        public string GenerateDataPath(string filename)
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

        public const string PortalSongFilename = "ac_dnbpor.mp3";
        private void StartPortalSong()
        {
            if (PortalSongHeat >= PortalSongHeatMax)
                return;

            Music.Play(PortalSongFilename, true);
            PortalSongHeat += 1.0;
        }

        [BaseEvent("ChangePortalMode", "CharacterFilter")]
        private void CharacterFilter_ChangePortalMode(object sender, ChangePortalModeEventArgs e)
        {
            if(e.Type == PortalEventType.EnterPortal)
            {
                Music.Stop();// kill existing music if present

                // start sound
                Log("changeportalmode START");

                StartPortalSong();
            } else
            {
                Log("changeportalmode DONE");

                Music.Stop();// STOP the portal music


                // bg music?
                if (true)
                {
                    // for now (testing) just start playing a song depending on terrain or inside lol
                    if (Position.FromObject(Player)?.IsTerrain ?? false)
                        Music.Play("ac_anotherorch.mp3", false);
                    else
                        Music.Play("ac_someoffbeat.mp3", false);
                }
            }
        }

        [BaseEvent("LoginComplete", "CharacterFilter")]
        private void CharacterFilter_LoginComplete(object sender, EventArgs e)
        {
            WriteToChat("Startup");


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
