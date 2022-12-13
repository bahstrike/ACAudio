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
using System.Text.RegularExpressions;
using System.Diagnostics;

using ACACommon;

namespace ACAVCServer
{
    [WireUpBaseEvents]
    [FriendlyName(PluginName)]
    public class PluginCore : Decal.Adapter.PluginBase
    {
        public const string PluginName = "ACAVCServer";//needs to match namespace or "embedded resources" wont work

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
                Log("                       ACAVCServer Startup");
                Log("----------------------------------------------------------------------");



                //Log("Generate virindi view");
                VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
                VirindiViewService.ViewProperties properties;
                VirindiViewService.ControlGroup controls;
                parser.ParseFromResource($"{PluginName}.mainView.xml", out properties, out controls);

                View = new VirindiViewService.HudView(properties, controls);



                try
                {
                    Server.LogCallback = Log;

                    Server.Init();
                }
                catch(Exception ex)
                {
                    Log($"server init exception: {ex.Message}");
                }



                //Log("hook stuff");
                Core.RenderFrame += _Process;
                Core.CharacterFilter.Logoff += _CharacterFilter_Logoff;
                Core.ChatBoxMessage += _ChatBoxMessage;




            }
            catch (Exception ex)
            {
                Log($"Startup exception: {ex.Message}");
            }
        }

        private void _ChatBoxMessage(object sender, ChatTextInterceptEventArgs e)
        {
            //Log($"CHAT: {e.Text}");


        }


        private bool LogOff = false;
        private void _CharacterFilter_Logoff(object sender, LogoffEventArgs e)
        {
            if (LogOff)
                return;

            Log("LOGOFF LOL");


            LogOff = true;




        }

        public WorldObject Player
        {
            get
            {
                return Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First;
            }
        }

        public INIFile INIFile
        {
            get
            {
                return new INIFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ACAVCServer.ini"));
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


        private void Process(double dt, double truedt)
        {
            (View["Status"] as HudStaticText).Text = $"Players:{Server.GetPlayers().Length}";
        }
       

        [BaseEvent("LoginComplete", "CharacterFilter")]
        private void CharacterFilter_LoginComplete(object sender, EventArgs e)
        {
            WriteToChat($"Startup");


        }


        /// <summary>
        /// This is called when the plugin is shut down. This happens only once.
        /// </summary>
        protected override void Shutdown()
        {
            //Log("unhook stuff");
            Core.ChatBoxMessage -= _ChatBoxMessage;
            Core.CharacterFilter.Logoff -= _CharacterFilter_Logoff;
            Core.RenderFrame -= _Process;


            Server.Shutdown();


            Log("----------------------------------------------------------------------");
            Log("                       ACAVCServer Shutdown");
            Log("----------------------------------------------------------------------");
        }

        private void FilterCore_CommandLineText(object sender, ChatParserInterceptEventArgs e)
        {
            /*if (false)
            {

                //Do not execute as AC command.
                e.Eat = true;
            }*/

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

        private static CritSect LogCrit = new CritSect();
        public static void Log(string ln)
        {
            using (LogCrit.Lock)
            {
                try
                {
                    using (StreamWriter logFile = File.AppendText(FinalLogFilepath))
                        logFile.WriteLine($"{DateTime.Now.ToLongTimeString()}: {ln}");
                }
                catch
                {

                }
            }
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
