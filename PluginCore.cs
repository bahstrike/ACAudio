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

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup()
		{
            InstanceNumber = InstanceNumberGen++;
            _Instance = this;

            try
            {
                if (File.Exists(FinalLogFilepath))
                    File.Delete(FinalLogFilepath);

                Log("----------------------------------------------------------------------");
                Log("                            ACAudio Startup");
                Log("----------------------------------------------------------------------");


                VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
                VirindiViewService.ViewProperties properties;
                VirindiViewService.ControlGroup controls;
                parser.ParseFromResource($"{PluginName}.mainView.xml", out properties, out controls);
                
                View = new VirindiViewService.HudView(properties, controls);




                View.ThemeChanged += delegate (object sender, EventArgs e)
                {
                    RegenerateLogos();
                };


            
                View["Test"].Hit += delegate(object sender, EventArgs e)
                {
                    WriteToChat("BLAH");

                    /*string filepath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "data/ac_anotherorch.mp3");
                    Audio.Sound snd = Audio.GetSound("test", File.ReadAllBytes(filepath), Audio.DimensionMode._2D, false);

                    Audio.PlaySound(snd);*/
                };

                View["FMOD"].Hit += delegate (object sender, EventArgs e)
                {
                    //System.Diagnostics.Process.Start("https://fmod.com/");
                };

                (View["FMOD_Credit"] as HudStaticText).FontHeight = 5;


                RegenerateLogos();


                Audio.Init(1000, dopplerscale:0.135f);

                Core.RenderFrame += _Process;
            }
            catch
            {

            }
        }

        public const int BadWorldID = -1;

        Audio.Channel testChannel = null;
        int testObjID = BadWorldID;

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

            Process(dt, truedt);
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

            WorldObject testObj = Core.WorldFilter[testObjID];
            if (testChannel != null && testChannel.IsPlaying && testObj != null)
                testChannel.SetPosition(SmithInterop.Vector(testObj.RawCoordinates()), Vec3.Zero);
            

            sayStuff += dt;
            if(sayStuff >= 0.2)
            {
                sayStuff = 0.0;

                double maxDist = 35.0;

                List<WorldObject> players = FilterByDistance(Core.WorldFilter.GetByObjectClass(ObjectClass.Player), playerPos, maxDist);
                List<WorldObject> npcs = FilterByDistance(Core.WorldFilter.GetByObjectClass(ObjectClass.Npc), playerPos, maxDist);
                List<WorldObject> monsters = FilterByDistance(Core.WorldFilter.GetByObjectClass(ObjectClass.Monster), playerPos, maxDist);
                List<WorldObject> allobj = FilterByDistance(Core.WorldFilter.GetAll(), playerPos, maxDist);

                if(testObj != null)
                    WriteToChat($"dist to sound: {(SmithInterop.Vector(testObj.RawCoordinates()) - cameraMat.Position).Magnitude.ToString("0.00")}");



                // lol lets dump stuff to look at
                Log("--------------- WE BE DUMPIN ------------");
                foreach (WorldObject obj in allobj)
                {
                    Log($"name:{obj.Name}  id:{obj.Id}  class:{obj.ObjectClass}  pos:{SmithInterop.Vector(obj.RawCoordinates())}");
                }

                //WriteToChat($"{InstanceNumber}  players:{players.Count}  npcs:{npcs.Count}  monsters:{monsters.Count}  playerPos:{playerPos}");

                //WriteToChat(cameraMat.ToString());

                //WriteToChat($"{InstanceNumber} blah {Core.WorldFilter.GetAll().Count} objects  {Core.WorldFilter.GetLandscape().Count} landscape   player: {coords.NorthSouth},{coords.EastWest}");



                {

                    // find closest NPC to make a sound source
                    WorldObject closestObj = null;
                    double closestDist = 99999.0;
                    foreach (WorldObject obj in Core.WorldFilter.GetByObjectClass(ObjectClass.Npc))
                    {
                        Vec3 objPos = SmithInterop.Vector(obj.RawCoordinates());
                        double dist = (objPos - playerPos).Magnitude;

                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestObj = obj;
                        }
                    }


                    if (closestObj != null)
                    {
                        // only switch if its diff from existing
                        if (closestObj.Id != testObjID)
                        {
                            if (testChannel != null)
                            {
                                testChannel.Stop();
                                testChannel = null;
                            }



                            testObj = closestObj;//lol dumb reusing local var.. all test haxx for now
                            testObjID = testObj.Id;

                            string playfile;
                            if (MathLib.random.NextDouble() >= 0.5)
                                playfile = "data/questrecieved.mp3";
                            else
                                playfile = "data/questcomplete.mp3";


                            string filepath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), playfile);
                            Audio.Sound snd = Audio.GetSound("test", File.ReadAllBytes(filepath), Audio.DimensionMode._3DPositional, true);

                            testChannel = Audio.PlaySound(snd, true);

                            testChannel.SetPosition(SmithInterop.Vector(testObj.RawCoordinates()), Vec3.Zero);

                            testChannel.Volume = 0.6;

                            testChannel.SetMinMaxDistance(5.0, 35.0);


                            testChannel.Play();
                        }
                    }

                }



            }


            // Z is up

            Audio.Process(dt, truedt, cameraMat.Position, Vec3.Zero, cameraMat.Up, cameraMat.Forward);
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
            {
                string filepath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "data/ac_anotherorch.mp3");
                Audio.Sound snd = Audio.GetSound("test", File.ReadAllBytes(filepath), Audio.DimensionMode._2D, true);

                Audio.Channel song = Audio.PlaySound(snd, true);

                song.Volume = 0.3;
                song.Play();

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
