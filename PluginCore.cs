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


            
                View["Test"].Hit += delegate(object sender, EventArgs e)
                {
                    WriteToChat("BLAH");


                    List<WorldObject> allobj = FilterByDistance(Core.WorldFilter.GetAll(), SmithInterop.Vector(Player.RawCoordinates()), 35.0);

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

                View["FMOD"].Hit += delegate (object sender, EventArgs e)
                {
                    //System.Diagnostics.Process.Start("https://fmod.com/");
                };

                (View["FMOD_Credit"] as HudStaticText).FontHeight = 5;


                Log("regen logos");
                RegenerateLogos();


                Log("init audio");
                if (!Audio.Init(1000, dopplerscale: 0.135f))
                    Log("Failed to initialize Audio");

                Log("hook render");
                Core.RenderFrame += _Process;
            }
            catch(Exception ex)
            {
                Log($"Startup exception: {ex.Message}");
            }
        }

        public WorldObject Player
        {
            get
            {
                return Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First;
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
            catch(Exception ex)
            {
                Log($"Process exception: {ex.Message}");
            }
        }

        private Mat4 GetCameraMatrix()
        {
            UtilityBelt.Lib.Frame frame = UtilityBelt.Lib.Frame.Get(Host.Actions.Underlying.SmartboxPtr() + 8);//used with permission by trevis (UtilityBelt)

            return SmithInterop.Matrix(frame);
        }

        double sayStuff = 0.0;
        private void Process(double dt, double truedt)
        {
            Mat4 cameraMat = GetCameraMatrix();


            Vec3 playerPos = SmithInterop.Vector(Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First.RawCoordinates());


            WorldObject w;
            //w.RawCoordinates
            Vector3Object v;

            Decal.Adapter.Wrappers.D3DObj d;
            //d.OrientToCamera
            

            sayStuff += dt;
            if (sayStuff >= 0.2)
            {
                sayStuff = 0.0;

                double maxDist = 35.0;

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



                {

                    // find closest NPC to make a sound source
                    foreach (WorldObject obj in Core.WorldFilter.GetAll())
                    {
                        double dist = (playerPos - SmithInterop.Vector(obj.RawCoordinates())).Magnitude;

                        if (dist > 35.0)
                            continue;


                        if (obj.ObjectClass == ObjectClass.Portal)
                            PlayForObject(obj, "portal.ogg");
                        else if (obj.ObjectClass == ObjectClass.Lifestone)
                            PlayForObject(obj, "lifestone.ogg");
                    }


                }



                //Log($"activeobjs:{ActiveObjectAmbients.Count}  audiochannels:{Audio.ChannelCount}");
                (View["Info"] as HudStaticText).Text = $"activeobjs:{ActiveObjectAmbients.Count}  audiochannels:{Audio.ChannelCount}";

            }



            // kill/forget sounds for objects out of range?
            for (int x=0; x<ActiveObjectAmbients.Count; x++)
            {
                ObjectAmbient oa = ActiveObjectAmbients[x];

                bool keep = true;

                WorldObject obj = oa.WorldObject;
                if (obj == null)
                    keep = false;
                else
                {
                    float minDist, maxDist;
                    oa.Channel.channel.get3DMinMaxDistance(out minDist, out maxDist);

                    double dist = (playerPos - SmithInterop.Vector(obj.RawCoordinates())).Magnitude;

                    // fudge dist a bit?
                    maxDist += 2.0f;

                    if (dist > (double)maxDist)
                        keep = false;
                }


                if(!keep)
                {
                    oa.Channel.Stop();
                    Audio.ForgetChannel(oa.Channel);

                    ActiveObjectAmbients.RemoveAt(x);
                    x--;
                } else
                {
                    // update position
                    oa.Channel.SetPosition(SmithInterop.Vector(obj.RawCoordinates()), Vec3.Zero);
                }
            }


            // Z is up

            Audio.Process(dt, truedt, cameraMat.Position, Vec3.Zero, cameraMat.Up, cameraMat.Forward);
        }

        public class ObjectAmbient
        {
            public int WeenieID;
            public Audio.Channel Channel;

            public WorldObject WorldObject
            {
                get
                {
                    return Instance.Core.WorldFilter[WeenieID];
                }
            }
        }

        public List<ObjectAmbient> ActiveObjectAmbients = new List<ObjectAmbient>();

        public void PlayForObject(WorldObject obj, string filename)
        {
            // check if playing already
            foreach(ObjectAmbient oa in ActiveObjectAmbients)
                if(oa.WeenieID == obj.Id)
                {
                    // if same name, bail
                    if (oa.Channel.Sound.Name.Equals(filename, StringComparison.InvariantCultureIgnoreCase))
                        return;

                    // changing sounds? kill existing
                    oa.Channel.Stop();
                    Audio.ForgetChannel(oa.Channel);

                    ActiveObjectAmbients.Remove(oa);

                    break;
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
            newoa.Channel.Volume = 0.6;
            newoa.Channel.SetMinMaxDistance(5.0, 35.0);
            newoa.Channel.Play();

            ActiveObjectAmbients.Add(newoa);
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

            foreach(WorldObject obj in objects)
            {
                Vec3 objPt = SmithInterop.Vector(obj.RawCoordinates());
                if ((pt - objPt).Magnitude > dist)
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

            for(int y=0; y<h; y++)
            {
                for(int x=0; x<w; x++)
                {
                    Color clr = src.GetPixel(x, y);

                    if (clr.A > 0)
                        dst.SetPixel(x, y, fill);
                }
            }

            return dst;
        }


        [BaseEvent("LoginComplete", "CharacterFilter")]
        private void CharacterFilter_LoginComplete(object sender, EventArgs e)
        {
            WriteToChat("Startup");



            // bg music test
            if(true)
            {
                try
                {
                    //string filepath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), /*"data/ac_anotherorch.mp3"*/"data/ac_someoffbeat.mp3");
                    Audio.Sound snd = Audio.GetSound("test", ReadDataFile("ac_anotherorch.mp3"), Audio.DimensionMode._2D, true);
                    if (snd == null)
                        Log("cant get music sound");
                    else
                    {

                        Audio.Channel song = Audio.PlaySound(snd, true);
                        if (song == null)
                            Log("cant make sound channel");
                        else
                        {

                            song.Volume = 0.3;
                            song.Play();
                        }
                    }

                }
                catch(Exception ex)
                {
                    Log($"failed to play music: {ex.Message}");
                }
            }


        }

        /// <summary>
        /// This is called when the plugin is shut down. This happens only once.
        /// </summary>
        protected override void Shutdown()
        {
            Core.RenderFrame -= _Process;

            Audio.Shutdown();


            Log("----------------------------------------------------------------------");
            Log("                           ACAudio Shutdown");
            Log("----------------------------------------------------------------------");
        }

        private void FilterCore_CommandLineText(object sender, ChatParserInterceptEventArgs e)
        {
            if(false)
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
