﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;

using Smith;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using VirindiHotkeySystem;
using VirindiViewService.Controls;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Diagnostics;

using ACACommon;
using System.Text;

namespace ACAudio
{
    [WireUpBaseEvents]
    [FriendlyName(PluginName)]
    public class PluginCore : Decal.Adapter.PluginBase
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

        public static string ACServerHost = null;

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


                string[] args = Environment.GetCommandLineArgs();
                for (int x = 0; x < args.Length - 1; x++)
                {
                    if (args[x].Equals("-h", StringComparison.InvariantCultureIgnoreCase))
                    {
                        ACServerHost = args[x + 1];
                        break;
                    }
                }




                //Log("init audio");
                if (!Audio.Init(128, dopplerscale: 0.135f))
                    Log("Failed to initialize Audio");



                // HAXXX select different output device
                /*{
                    int numDrivers;
                    Audio.fmod.getNumDrivers(out numDrivers);
                    for (int x = 0; x < numDrivers; x++)
                    {
                        StringBuilder sb = new StringBuilder(512);
                        Guid guid;
                        int systemrate;
                        FMOD.SPEAKERMODE speakermode;
                        int speakermodechannels;
                        Audio.fmod.getDriverInfo(x, sb, 512, out guid, out systemrate, out speakermode, out speakermodechannels);

                        speakermodechannels = speakermodechannels;

                        if (sb.ToString() == "VoiceMeeter Aux Input (VB-Audio VoiceMeeter AUX VAIO)")
                        {
                            Audio.fmod.setDriver(x);
                            break;
                        }
                    }


                }*/



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
                    WriteToChat("Check ACAudio.log");

                    List<ShadowObject> allobj = FilterByDistance(WorldObjects, CameraPosition, 35.0);

                    // lol lets dump stuff to look at
                    Log("--------------- WE BE DUMPIN ------------");
                    foreach (ShadowObject obj in allobj)
                    {
                        string stringkeys = string.Empty;
                        foreach (int i in obj.StringKeys)
                            stringkeys += $"{(StringValueKey)i}=\"{obj.Values((StringValueKey)i)}\", ";

                        string longkeys = string.Empty;
                        foreach (int i in obj.LongKeys)
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

                        Log($"class:{obj.ObjectClass}   id:{obj.Id}   name:{obj.Object.Name}  stringkeys:{{{stringkeys}}}  longkeys:{{{longkeys}}}  pos:{SmithInterop.Vector(obj.Object.RawCoordinates())}");
                    }

                };

                View["Coords"].Hit += delegate (object sender, EventArgs e)
                {
                    string output = $"pos {SmithInterop.Position(Player).ToString().Replace(" ", "")/*condense*/}";

                    WriteToChat(output);

                    // the log message is intended to be copy&paste to the .ACA files
                    Log(output);
                };

                (View["Enable"] as HudCheckBox).Change += delegate (object sender, EventArgs e)
                {

                };

                View["Reload"].Hit += delegate (object sender, EventArgs e)
                {
                    // kill ambients; let reloaded config generate them again
                    foreach (Ambient amb in ActiveAmbients)
                        amb.Stop();
                    ActiveAmbients.Clear();

                    ReloadConfig();
                };

                View["Defaults"].Hit += delegate (object sender, EventArgs e)
                {
                    (View["Enable"] as HudCheckBox).Checked = true;
                    (View["Volume"] as HudHSlider).Position = 100;

                    (View["MusicEnable"] as HudCheckBox).Checked = true;
                    (View["MusicVolume"] as HudHSlider).Position = 75;

                    (View["PortalMusicEnable"] as HudCheckBox).Checked = true;
                };

                View["NearestDID"].Hit += delegate (object sender, EventArgs e)
                {
                    List<StaticPosition> list = LoadStaticPositions(true);

                    Position? pos = SmithInterop.Position(Player);
                    if (pos.HasValue)
                    {
                        List<uint> nearDIDs = new List<uint>();

                        double nearestDist = MathLib.Infinity;
                        uint nearestDid = 0;

                        foreach (StaticPosition sp in list)
                        {
                            if (!sp.Position.IsCompatibleWith(pos.Value))
                                continue;

                            double dist = (sp.Position.Global - pos.Value.Global).Magnitude;
                            if (dist < nearestDist)
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


                        string nearbytxt = string.Empty;
                        for (int x = 0; x < nearDIDs.Count; x++)
                        {
                            // we'll list nearest one seperately
                            if (nearDIDs[x] == nearestDid)
                                continue;

                            nearbytxt += $"{nearDIDs[x].ToString("X8")}";
                            if (x < (nearDIDs.Count - 1))
                                nearbytxt += ", ";
                        }

                        WriteToChat($"Nearest DID: {nearestDid.ToString("X8")}    more: {nearbytxt}");
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




                #region VoiceChat stuff
                VCClient.LogCallback = VCClientLog;
                VCClient.GetWeeniePosition = VCClientGetWeeniePosition;
                VCClient.CreateSpeakingIcon = VCClientCreateSpeakingIcon;
                VCClient.DestroySpeakingIcon = VCClientDestroySpeakingIcon;
                VCClient.CheckForMute = VCClientCheckForMute;


                (View["VCServerAutoCheck"] as HudCheckBox).Change += delegate (object sender, EventArgs e)
                {
                    if ((sender as HudCheckBox).Checked)
                    {
                        (View["VCServerCustomCheck"] as HudCheckBox).Checked = false;
                        (View["VCServerBotCheck"] as HudCheckBox).Checked = false;
                    }
                };

                (View["VCServerCustomCheck"] as HudCheckBox).Change += delegate (object sender, EventArgs e)
                {
                    if ((sender as HudCheckBox).Checked)
                    {
                        (View["VCServerAutoCheck"] as HudCheckBox).Checked = false;
                        (View["VCServerBotCheck"] as HudCheckBox).Checked = false;
                    }
                };

                (View["VCServerBotCheck"] as HudCheckBox).Change += delegate (object sender, EventArgs e)
                {
                    if ((sender as HudCheckBox).Checked)
                    {
                        (View["VCServerAutoCheck"] as HudCheckBox).Checked = false;
                        (View["VCServerCustomCheck"] as HudCheckBox).Checked = false;
                    }
                };

                (View["VCServerAutoHost"] as HudStaticText).Text = ACServerHost ?? "-failed to query-";


                HudCombo recordDeviceCombo = View["RecordDevice"] as HudCombo;
                recordDeviceCombo.Clear();

                // safe to do without initialization
                AvailableRecordDevices = VCClient.QueryRecordDevices();
                int defaultIndex = -1;
                for (int x = 0; x < AvailableRecordDevices.Length; x++)
                {
                    VCClient.RecordDeviceEntry rde = AvailableRecordDevices[x];

                    recordDeviceCombo.AddItem(rde.Name, null);

                    if ((rde.DriverState & FMOD.DRIVER_STATE.DEFAULT) != 0)
                        defaultIndex = x;
                }


                HudCombo channelCombo = (View["MicChannel"] as HudCombo);
                channelCombo.Clear();
                channelCombo.AddItem("Proximity 3D", null);
                channelCombo.AddItem("Allegiance", null);
                channelCombo.AddItem("Fellowship", null);
                channelCombo.Current = 0;


                using (INIFile ini = INIFile)
                {
                    (View["Enable"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "Enable", "1") != "0";
                    (View["Volume"] as HudHSlider).Position = int.Parse(ini.GetKeyString(Core.CharacterFilter.AccountName, "Volume", "100"));

                    (View["MusicEnable"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "MusicEnable", "1") != "0";
                    (View["MusicVolume"] as HudHSlider).Position = int.Parse(ini.GetKeyString(Core.CharacterFilter.AccountName, "MusicVolume", "75"));

                    (View["PortalMusicEnable"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "PortalMusicEnable", "1") != "0";


                    int i;
                    if (!int.TryParse(ini.GetKeyString(Core.CharacterFilter.AccountName, "VCServer", "0"), out i))
                        i = 0;
                    switch (i)
                    {
                        case 0://auto
                            (View["VCServerAutoCheck"] as HudCheckBox).Checked = true;
                            (View["VCServerCustomCheck"] as HudCheckBox).Checked = false;
                            (View["VCServerBotCheck"] as HudCheckBox).Checked = false;
                            break;

                        case 1://custom
                            (View["VCServerAutoCheck"] as HudCheckBox).Checked = false;
                            (View["VCServerCustomCheck"] as HudCheckBox).Checked = true;
                            (View["VCServerBotCheck"] as HudCheckBox).Checked = false;
                            break;

                        case 2://bot
                            (View["VCServerAutoCheck"] as HudCheckBox).Checked = false;
                            (View["VCServerCustomCheck"] as HudCheckBox).Checked = false;
                            (View["VCServerBotCheck"] as HudCheckBox).Checked = true;
                            break;
                    }

                    (View["VCServerCustomHost"] as HudTextBox).Text = ini.GetKeyString(Core.CharacterFilter.AccountName, "VCServerCustomHost", string.Empty);
                    (View["VCServerBotHost"] as HudTextBox).Text = ini.GetKeyString(Core.CharacterFilter.AccountName, "VCServerBotHost", string.Empty);


                    (View["SoundsConnect"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "SoundsConnect", "1") != "0";
                    (View["SoundsJoin"] as HudCheckBox).Checked = ini.GetKeyString(Core.CharacterFilter.AccountName, "SoundsJoin", "0") != "0";


                    string preferredMic = ini.GetKeyString(Core.CharacterFilter.AccountName, "RecordDevice", string.Empty);
                    for (int x = 0; x < AvailableRecordDevices.Length; x++)
                    {
                        VCClient.RecordDeviceEntry rde = AvailableRecordDevices[x];

                        if (rde.Name.Trim() == preferredMic)
                        {
                            defaultIndex = x;
                            break;
                        }
                    }



                    // try to get current list of muted players from config
                    mutedPlayerNames.Clear();
                    try
                    {
                        int numMuted = int.Parse(ini.GetKeyString("Mute", "Num", "0"));
                        for (int x = 0; x < numMuted; x++)
                        {
                            string playerName = ini.GetKeyString("Mute", $"Player{x}", string.Empty);
                            if (string.IsNullOrEmpty(playerName))
                                continue;

                            mutedPlayerNames.Add(playerName);
                        }
                    }
                    catch(Exception ex)
                    {
                        Log($"Bad times parsing [Mute] from INI: {ex.Message}");
                    }
                }



                recordDeviceCombo.Current = defaultIndex;// set microphone input
                #endregion


                //Log("regen logos");
                RegenerateLogos();


                //Log("hook stuff");
                Core.RenderFrame += _Process;
                Core.WorldFilter.CreateObject += _CreateObject;
                Core.WorldFilter.ReleaseObject += _ReleaseObject;
                Core.CharacterFilter.Logoff += _CharacterFilter_Logoff;
                Core.ChatBoxMessage += _ChatBoxMessage;
                Core.CommandLineText += _CommandLineText;
                Core.CharacterFilter.ChangeFellowship += _CharacterFilter_ChangeFellowship;


                Music.Init();


                ReloadConfig();



                IsPortaling = true;
                StartPortalSong();// first time login portal deserves one
            }
            catch (Exception ex)
            {
                Log($"Startup exception: {ex.Message}");
            }
        }

        public int FellowshipID = StreamInfo.InvalidFellowshipID;

        private void _CharacterFilter_ChangeFellowship(object sender, ChangeFellowshipEventArgs e)
        {
            Log($"FELLOWSHIP MESSAGE:    type:{e.Type}   id:{e.Id.ToString("X8")}");

            int? targetFellowship = null;

            switch (e.Type)
            {
                case FellowshipEventType.Recruit:
                    // if recruiting a player, the ID is just weenieID of the player recruited. we dont need that. we'll get a Create message when we join one
                    break;

                case FellowshipEventType.Create:
                    targetFellowship = e.Id;// we're in this one now
                    break;

                case FellowshipEventType.Disband:
                    targetFellowship = e.Id;// should be 0.. same as InvalidFellowshipID
                    break;

                case FellowshipEventType.Quit:
                case FellowshipEventType.Dismiss:
                    // if it was us, then clear our target fellowship
                    if (e.Id == Player.Id)
                        targetFellowship = StreamInfo.InvalidFellowshipID;

                    break;
            }

            // assign new value if we have one
            if (targetFellowship.HasValue)
                FellowshipID = targetFellowship.Value;
        }

        bool ForcePerfDump = false;

        public List<ShadowObject> WorldObjects = new List<ShadowObject>();

        private ShadowObject GetByWorldObject(int id)
        {
            foreach (ShadowObject so in WorldObjects)
                if (so.Id == id)
                    return so;

            return null;
        }

        private ShadowObject GetByWorldObject(WorldObject wo)
        {
            if (wo == null)
                return null;

            return GetByWorldObject(wo.Id);
        }

        private void _CreateObject(object sender, CreateObjectEventArgs e)
        {
            if (GetByWorldObject(e.New) == null)
                WorldObjects.Add(new ShadowObject(e.New));
        }

        private void _ReleaseObject(object sender, ReleaseObjectEventArgs e)
        {
            ShadowObject so = GetByWorldObject(e.Released);
            if (so != null)
                WorldObjects.Remove(so);
        }

        private void _ChatBoxMessage(object sender, ChatTextInterceptEventArgs e)
        {
            //Log($"CHAT: {e.Text}");

            Config.SoundSource snd = Config.FindSoundSourceText(e.Text);
            if (snd != null)
            {
                // we have no position data.. its a 2d effect
                Log($"wanna play something from text: {e.Text}");
                PlayFor2DNow(snd);
                return;
            }


            if (e.Text.Contains("You tell"))
            {
                int i = e.Text.IndexOf(',');
                if (i != -1)
                {
                    string content = e.Text.Substring(i + 3);
                    if (content.StartsWith(TellPacket.prefix))
                    {
                        if (!ShowTellProtocol)
                        {
                            // dont show to user if its good
                            e.Eat = true;
                            return;
                        }
                    }
                }
            }


            // try to handle voicechat server bot protocol
            ACAUtils.ChatMessage cm = ACAUtils.InterpretChatMessage(e.Text);
            if (cm != null)
            {
                //Log($"CHAT ({e.Target}): [{cm.Channel}][{cm.ID.ToString("X8")}][{cm.PlayerName}][{cm.Mode}]:[{cm.Content}]");

                if (cm.Mode == "tells you")
                {
                    // tell protocol?
                    if (cm.Content.StartsWith(TellPacket.prefix))
                    {
                        if (!ShowTellProtocol)
                            // dont show to user if its good
                            e.Eat = true;

                        // dont accept any unless we initiated the conversation
                        if (!cm.PlayerName.Equals(lastBotJoinAttemptName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // just ignore the text; dont reply back or AC server might squelch us
                            Log($"we are receiving a packet from unexpected player {cm.PlayerName} !!!");
                            //ChatTell(cm.PlayerName, "Whatchoo talkin bout willis");
                        }
                        else
                        {
                            TellPacket p = TellPacket.FromString(cm.Content);
                            if (p != null)
                            {
                                if (p.Message == TellPacket.MessageType.RequestInfo)
                                {
                                    // bot server has recognized our request and wants our data before it will send IP
                                    TellPacket info = new TellPacket(TellPacket.MessageType.ClientInfo);

                                    info.WriteString(Player.Name);
                                    info.WriteInt(Player.Id);

                                    // reply
                                    ChatTell(cm.PlayerName, info.GenerateString());
                                }

                                if (p.Message == TellPacket.MessageType.ServerInfo)
                                {
                                    byte i0 = p.ReadByte();
                                    byte i1 = p.ReadByte();
                                    byte i2 = p.ReadByte();
                                    byte i3 = p.ReadByte();
                                    int port = p.ReadBits(16);

                                    VCClient.ServerIP = $"{i0}.{i1}.{i2}.{i3}";


                                    // remember this bot as our preferred host
                                    (View["VCServerAutoCheck"] as HudCheckBox).Checked = false;
                                    (View["VCServerCustomCheck"] as HudCheckBox).Checked = false;
                                    (View["VCServerBotCheck"] as HudCheckBox).Checked = true;
                                    (View["VCServerBotHost"] as HudTextBox).Text = cm.PlayerName;
                                }
                            }
                        }
                    }

                }
            }
        }

        public static bool ShowTellProtocol = false;

        void ChatTell(string targetName, string s)
        {
            CoreManager.Current.Actions.InvokeChatParser($"/tell {targetName},{s}");
        }

        private static void VCClientLog(string s)
        {
            Log("[VCClient] " + s);
        }

        Vec3? VCClientGetWeeniePosition(int weenieID)
        {
            WorldObject obj = Core.WorldFilter[weenieID];
            if (obj == null)
                return null;

            Position? pos = SmithInterop.Position(obj);
            if (pos == null)
                return null;

            return pos.Value.Global;
        }

        private Dictionary<int, D3DObj> ActiveSpeakingIcons = new Dictionary<int, D3DObj>();

        void VCClientCreateSpeakingIcon(int weenieID)
        {
            CreateSpeakingIcon(weenieID, false);
        }

        void CreateSpeakingIcon(int weenieID, bool microphone)
        {
            if (ActiveSpeakingIcons.ContainsKey(weenieID))
                return;

            D3DObj obj = Core.D3DService.NewD3DObj();

            obj.SetIcon(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), microphone ? "microphone.png" : "speaking.png"));
            obj.Anchor(weenieID, microphone ? 1.165f : 1.3f, 0.0f, 0.0f, 0.0f);
            obj.OrientToCamera(false);
            obj.Visible = true;
            obj.Autoscale = false;
            obj.Scale(microphone ? 0.2f : 0.3f);
            obj.Color = microphone ? unchecked((int)0x80FFFFFF) : unchecked((int)0xFFFFFFFF);

            ActiveSpeakingIcons.Add(weenieID, obj);
        }

        void VCClientDestroySpeakingIcon(int weenieID)
        {
            DestroySpeakingIcon(weenieID);
        }

        void DestroySpeakingIcon(int weenieID)
        {
            if (!ActiveSpeakingIcons.TryGetValue(weenieID, out D3DObj obj))
                return;

            obj.Visible = false;// set as invisible since i guess we're not allowed to dispose manually (wait for GC)
            ActiveSpeakingIcons.Remove(weenieID);
        }

        bool VCClientCheckForMute(string characterName, int weenieID)
        {
            //Log("UH OH, SOMEONE DIDN'T FINISH THE MUTE LOGIC");
            foreach(string plrName in mutedPlayerNames)
            {
                if (plrName.Equals(characterName, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            // not muted
            return false;
        }

        private VCClient.RecordDeviceEntry[] AvailableRecordDevices;

        private void ReloadConfig()
        {
            PerfTimer pt = new PerfTimer();
            pt.Start();

            BVH.Reset();

            Config.Load("master.aca");

            Log($"Parsed {Config.Sources.Count} sound sources from configs");


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

#if DEBUG
            {
                int numBVHs, numNodes, numThings;
                BVH.GetTreeInfo(out numBVHs, out numNodes, out numThings);
                Log($"BVH INFO: numBVHs:{numBVHs}  numNodes:{numNodes}  numThings:{numThings}");//report so far lol
            }
#endif


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
                if (zip != null)
                    zip.Close();
            }

            return list;
        }

        private bool LogOff = false;
        private void _CharacterFilter_Logoff(object sender, LogoffEventArgs e)
        {
            if (LogOff)
                return;

#if DEBUG
            Log("LOGOFF LOL");
#endif
            IsPortaling = true;
            StartPortalSong();

            LogOff = true;



            // doesnt matter if we do another later for good measure;  when we log out then our weenie isnt good anymore so kill immediately
            VCClient.Shutdown();


            using (INIFile ini = INIFile)
            {
                ini.WriteKey(Core.CharacterFilter.AccountName, "Enable", (View["Enable"] as HudCheckBox).Checked ? "1" : "0");
                ini.WriteKey(Core.CharacterFilter.AccountName, "Volume", (View["Volume"] as HudHSlider).Position.ToString());

                ini.WriteKey(Core.CharacterFilter.AccountName, "MusicEnable", (View["MusicEnable"] as HudCheckBox).Checked ? "1" : "0");
                ini.WriteKey(Core.CharacterFilter.AccountName, "MusicVolume", (View["MusicVolume"] as HudHSlider).Position.ToString());

                ini.WriteKey(Core.CharacterFilter.AccountName, "PortalMusicEnable", (View["PortalMusicEnable"] as HudCheckBox).Checked ? "1" : "0");


                int i;
                if ((View["VCServerBotCheck"] as HudCheckBox).Checked)
                    i = 2;
                else if ((View["VCServerCustomCheck"] as HudCheckBox).Checked)
                    i = 1;
                else
                    i = 0;

                ini.WriteKey(Core.CharacterFilter.AccountName, "VCServer", i.ToString());
                ini.WriteKey(Core.CharacterFilter.AccountName, "VCServerCustomHost", (View["VCServerCustomHost"] as HudTextBox).Text);
                ini.WriteKey(Core.CharacterFilter.AccountName, "VCServerBotHost", (View["VCServerBotHost"] as HudTextBox).Text);

                ini.WriteKey(Core.CharacterFilter.AccountName, "SoundsConnect", (View["SoundsConnect"] as HudCheckBox).Checked ? "1" : "0");
                ini.WriteKey(Core.CharacterFilter.AccountName, "SoundsJoin", (View["SoundsJoin"] as HudCheckBox).Checked ? "1" : "0");

                ini.WriteKey(Core.CharacterFilter.AccountName, "RecordDevice", VCClient.CurrentRecordDevice == null ? string.Empty : VCClient.CurrentRecordDevice.Name.Trim());

                ini.WriteKey("Mute", "Num", mutedPlayerNames.Count.ToString());
                for(int x=0; x<mutedPlayerNames.Count; x++)
                    ini.WriteKey("Mute", $"Player{x}", mutedPlayerNames[x]);
            }


            //VHotkeySystem.InstanceReal.RemoveHotkey(Hotkey_PTT_P);
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
                if (LoginCompleteTimestamp == 0)
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
            if (!queryAttempts.TryGetValue(obj.Id, out q))
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


        static void PlaySimple2D(Config.SoundAttributes Sound, bool? looping=null)
        {
            if (Sound == null)
                return;

            Audio.Sound snd = GetOrLoadSound(Sound.file, Audio.DimensionMode._2D, looping ?? Sound.looping, false);
            if (snd == null)
                return;

            Audio.Channel channel = Audio.PlaySound(snd, true);

            channel.Volume = Sound.vol;

            channel.Play();
        }


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

            PlayerPos playerPos = PlayerPos.Create();



            {
                PerfTrack.Start("Internal Process");
                PerfTrack.Push();



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


#if true
                        DynamicObjectsTick(dt, playerPos);
#else
                        // lets pre-filter objects list to compatible landblock and container
                        PerfTrack.Start("prefilter objects");
                        List<ShadowObject> objects = new List<ShadowObject>();
                        foreach(ShadowObject obj in WorldObjects)
                        {
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

#endif
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
                        List<BVH.BVHEntry_StaticPosition> posList = new List<BVH.BVHEntry_StaticPosition>(BVH.QueryStaticPositions(playerPos.CameraPos));

                        // try to add ones recognized by player object position too
                        foreach (BVH.BVHEntry_StaticPosition pos in BVH.QueryStaticPositions(playerPos.ObjectPos))
                            if (!posList.Contains(pos))
                                posList.Add(pos);

                        foreach (BVH.BVHEntry_StaticPosition pos in posList)
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

                    }



                    // static positions
                    {
                        // dispatch static positions
                        PerfTrack.Start("Static positions");

                        foreach (Config.SoundSourcePosition src in Config.FindSoundSourcesPosition(playerPos))
                        {
                            double dist = (playerPos.Position(src).Global - src.Position.Global).Magnitude;
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
                    $"cam:{playerPos.CameraPos.Global}  lb:{playerPos.CameraPos.Landblock.ToString("X8")}\n" +
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

                if (discardReason == null)
                {
                    // cull incompatible dungeon id
                    if (!playerPos.Position(a.Source).IsCompatibleWith(a.Position))
                        discardReason = $"wrong dungeon  player:{playerPos.Position(a.Source).DungeonID}  vs  amb:{a.Position.DungeonID}";
                }

                if (discardReason == null)
                {
                    double dist = (playerPos.Position(a.Source).Global - a.Position.Global).Magnitude;
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


                if (discardReason == null)
                {
                    ObjectAmbient oa = a as ObjectAmbient;
                    if (oa != null)
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

#if DEBUG
                    if (a is ObjectAmbient)
                        Log($"removing for weenie {(a as ObjectAmbient).WeenieID}: {discardReason}");
                    else if (a is StaticAmbient)
                        Log($"removing for static {(a as StaticAmbient).Position}: {discardReason}");
#endif



                    a.Stop();

                    ActiveAmbients.RemoveAt(x);
                    x--;
                }
                else
                {

                }
            }



            // lets handle our "cluster" and "sync" logic here.  new sound ambients should not be playing until Process is called, so we
            // can affect volumes here without popping artifacts
            {
                PerfTrack.Start("Cluster ambients");
                List<string> alreadyDone = new List<string>();
                for (int x = 0; x < ActiveAmbients.Count - 1; x++)
                {
                    Ambient aa = ActiveAmbients[x];

                    // only do for sync??
                    if (!aa.Source.Sound.sync)
                        continue;

                    if (alreadyDone.Contains(aa.Source.Sound.file))
                        continue;

                    alreadyDone.Add(aa.Source.Sound.file);


                    List<Ambient> clusterAmbients = new List<Ambient>();
                    clusterAmbients.Add(aa);

                    for (int y = 1; y < ActiveAmbients.Count; y++)
                    {
                        Ambient ab = ActiveAmbients[y];

                        // if not playing same sound, skip
                        if (!aa.Source.Sound.file.Equals(ab.Source.Sound.file, StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        clusterAmbients.Add(ab);
                    }



                    // if we have a cluster, reduce vol/dist
                    double volAdjust = 1.0;
                    double minDistAdjust = 1.0;
                    double maxDistAdjust = 1.0;

                    if (clusterAmbients.Count > 3)
                    {
                        volAdjust = 0.4;
                        minDistAdjust = 0.9;
                        maxDistAdjust = 0.5;
                    }

                    foreach (Ambient a in clusterAmbients)
                    {
                        a.VolScale = volAdjust;
                        a.MinDistScale = minDistAdjust;
                        a.MaxDistScale = maxDistAdjust;
                    }

                    // should not need to manually SetTargetVolume here since that is currently happening within Ambient.Process()  anyway




                    // lets also sync timestamps
                    uint timestamp = uint.MaxValue;
                    foreach (Ambient a in clusterAmbients)
                    {
                        if (!a.IsPlaying)
                            continue;

                        // take first one as the timestamp source but dont touch it
                        if (timestamp == uint.MaxValue)
                        {
                            timestamp = a.SamplePosition;
                            continue;
                        }

                        // sync everything else
                        a.SamplePosition = timestamp;
                    }
                }
            }



            // process ambients
            PerfTrack.Start("Process ambients");
            PerfTrack.Push();
            foreach (Ambient a in ActiveAmbients)
                a.Process(dt);
            PerfTrack.Pop();




            // handle sound cache logic
            PerfTrack.Start("Cache");
            {
                // build list of all currently playing sounds
                List<Audio.Sound> activeSounds = new List<Audio.Sound>();
                foreach (Audio.Channel channel in Audio.GetAllChannels())
                    if (channel.IsPlaying && channel.Sound != null && !activeSounds.Contains(channel.Sound))
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
                foreach (Audio.Sound snd in Audio.GetAllSounds())
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
                foreach (Audio.Sound snd in soundsToUnload)
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


            PerfTrack.Start("Client.Process");
            try
            {
                // only change IP if on auto/custom.  if bot, it will be populated by /tell protocol
                if ((View["VCServerAutoCheck"] as HudCheckBox).Checked)
                    VCClient.ServerIP = (View["VCServerAutoHost"] as HudStaticText).Text;
                else if ((View["VCServerCustomCheck"] as HudCheckBox).Checked)
                    VCClient.ServerIP = (View["VCServerCustomHost"] as HudTextBox).Text;
                else if ((View["VCServerBotCheck"] as HudCheckBox).Checked)
                {
                    // only try /tell periodically and if we are legit in-game and not connected to voice server
                    if (!NeedFirstLoginPlayerWeenie && !VCClient.IsConnected && (DateTime.Now.Subtract(lastBotJoinAttemptTime).TotalMilliseconds + botJoinAttemptOffsetMsec) > 30000)
                    {
                        string botName = (View["VCServerBotHost"] as HudTextBox).Text;
                        if (!string.IsNullOrEmpty(botName))
                        {
                            //ChatTell(botName, "join");
                            TellPacket p = new TellPacket(TellPacket.MessageType.Join);
                            ChatTell(botName, p.GenerateString());

                            lastBotJoinAttemptName = botName;
                            lastBotJoinAttemptTime = DateTime.Now;
                            botJoinAttemptOffsetMsec = MathLib.random.Next(15000);//after our attempt, establish some extra delay to stagger many peoples' join attempts
                        }
                    }
                }


                bool isConnected = VCClient.IsConnected;
                if (isConnected != wasConnectedToVoice)
                {
                    bool wantSounds = (View["SoundsConnect"] as HudCheckBox).Checked;

                    if (isConnected)
                    {
                        WriteToChat($"Connected to voice chat server");
                        if(wantSounds)
                            PlaySimple2D(Config.VCConnectSound, false);
                    }
                    else
                    {
                        WriteToChat($"Disconnected from voice chat server");
                        if(wantSounds)
                            PlaySimple2D(Config.VCDisconnectSound, false);

                        // if we were connected to bot, forget server IP to force client to attempt "/tell join" again
                        if ((View["VCServerBotCheck"] as HudCheckBox).Checked)
                        {
                            VCClient.ServerIP = null;

                            lastBotJoinAttemptTime = DateTime.Now;// make sure we dont try again immediately and perhaps use our staggering logic
                        }
                    }

                    wasConnectedToVoice = isConnected;
                }


                if (VCClient.IsConnected)
                    (View["VoiceChatStatus"] as HudStaticText).Text = $"Status: Connected   Players: {VCClient.TotalConnectedPlayers}   Nearby: {(VCClient.AreThereNearbyPlayers ? "Yes" : "No")}";
                else if (VCClient.WaitingForConnect || DateTime.Now.Subtract(lastBotJoinAttemptTime).TotalMilliseconds < 2500)
                    (View["VoiceChatStatus"] as HudStaticText).Text = $"Status: Attempting to connect...";
                /*else if(VCClient.SentServerHandshake)
                    (View["VoiceChatStatus"] as HudStaticText).Text = $"Status: Logging in...";*/
                else
                    (View["VoiceChatStatus"] as HudStaticText).Text = $"Status: Disconnected";

                /*View["RecordDevice"].Visible = VCClient.IsConnected;
                View["MicLoopback"].Visible = VCClient.IsConnected;
                View["Mic3D"].Visible = VCClient.IsConnected;*/

                // update parameters
                VCClient.Loopback = (View["MicLoopback"] as HudCheckBox).Checked;
                //VCClient.Speak3D = (View["Mic3D"] as HudCheckBox).Checked;
                //                VCClient.SpeakChannel = (StreamInfo.VoiceChannel)(View["MicChannel"] as HudCombo).Current;

                int recordDeviceIndex = (View["RecordDevice"] as HudCombo).Current;
                if (recordDeviceIndex < 0 || recordDeviceIndex >= AvailableRecordDevices.Length)
                    VCClient.CurrentRecordDevice = null;
                else
                    VCClient.CurrentRecordDevice = AvailableRecordDevices[recordDeviceIndex];

                VCClient.PlayerPosition = playerPos.ObjectPos;
                /*if (!NeedFirstLoginPlayerWeenie)// core.characterfilter.allegiance is probably dead until we finish logging in
                {
                    AllegianceInfoWrapper allegianceInfo = Core.CharacterFilter.Allegiance;
                    if (allegianceInfo != null)
                        VCClient.PlayerAllegiance = new Allegiance(allegianceInfo.Id);
                    else
                        VCClient.PlayerAllegiance = Allegiance.Invalid;
                }*/
                VCClient.PlayerAllegianceID = GetByWorldObject(Player)?.Values(LongValueKey.Monarch) ?? StreamInfo.InvalidAllegianceID;
                VCClient.PlayerFellowshipID = FellowshipID;


                //(View["VoiceChatKey"] as HudStaticText).Text = "ACAudio Virindi Hotkeys:";
                (View["VoiceChatKey_PTT"] as HudStaticText).Text = $"{Hotkey_PTT_Desc}: {(GetValidHotkey(Hotkey_PTT)?.KeyString ?? "-not bound-")}";
                (View["VoiceChatKey_PTT_Allegiance"] as HudStaticText).Text = $"{Hotkey_PTT_Allegiance_Desc}: {(GetValidHotkey(Hotkey_PTT_Allegiance)?.KeyString ?? "-not bound-")}";
                (View["VoiceChatKey_PTT_Fellowship"] as HudStaticText).Text = $"{Hotkey_PTT_Fellowship_Desc}: {(GetValidHotkey(Hotkey_PTT_Fellowship)?.KeyString ?? "-not bound-")}";


                StreamInfo.VoiceChannel currentVoice = StreamInfo.VoiceChannel.Invalid;
                if (DoesACHaveFocus())
                {
                    if (IsHotkeyHeld(Hotkey_PTT_Allegiance))
                        currentVoice = StreamInfo.VoiceChannel.Allegiance;
                    else if (IsHotkeyHeld(Hotkey_PTT_Fellowship))
                        currentVoice = StreamInfo.VoiceChannel.Fellowship;
                    else if (IsHotkeyHeld(Hotkey_PTT))
                    {
                        currentVoice = (StreamInfo.VoiceChannel)(View["MicChannel"] as HudCombo).Current;
                        //currentVoice = StreamInfo.VoiceChannel.Proximity3D;
                    }
                    else
                    {
                        // no actively-held hotkey.  forget any that have been triggered
                        lastHotkey = null;
                    }
                }

                if (currentVoice != VCClient.CurrentVoice)
                {
                    VCClient.CurrentVoice = currentVoice;

                    // create/destroy our own speaking icon based on what state we're moving to  (unless we are in loopback;  we'll rely on server to issue ours)
                    WorldObject playerObj = Player;
                    if (playerObj != null && !VCClient.Loopback)
                    {
                        if (currentVoice == StreamInfo.VoiceChannel.Invalid)
                            DestroySpeakingIcon(playerObj.Id);
                        else
                            CreateSpeakingIcon(playerObj.Id, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"vcclient updates exception: {ex.Message}");
            }

            try
            { 
                // do whatever we gotta do
                VCClient.Process(dt);
            }
            catch (Exception ex)
            {
                Log($"VCClient.Process exception: {ex.Message}");
            }


            PerfTrack.Start("Audio.Process");
            {
                // do all this based on camera (listener MUST be camera for proper 3d audio without sounding weird)
                try
                {
                    Audio.Process(dt, truedt, playerPos.CameraPos.Global, Vec3.Zero, playerPos.CameraMat.Up, playerPos.CameraMat.Forward);
                }
                catch (Exception ex)
                {
                    Log($"Audio process exception: {ex.Message}");
                }
            }



            pt_process.Stop();
            lastProcessTime = pt_process.Duration;



            //PerfTrack.Pop();
            PerfTrack.StopLast();



            // dont do any more logic after this point that could influence framerate (should be tracked in performance)


            // anything that decreases frames below XX should be reported
            double fps = (1.0 / lastProcessTime);
#if DEBUG
            if (fps < 20.0 || ForcePerfDump)
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

                    str += $" {step.Name}: {(step.Duration * 1000.0).ToString("#0.00")}msec";

                    Log(str);
                }
                Log("------------");
            }
#endif



            // this should probably be like a checkbox or something;  a populated server probably shouldnt "DING DING DING" people join/leave the voice server.
            if(VCClient.TotalConnectedPlayers != lastKnownPlayerCount)
            {
                bool wantSounds = (View["SoundsJoin"] as HudCheckBox).Checked;

                if (VCClient.TotalConnectedPlayers > lastKnownPlayerCount)
                {
                    if(wantSounds)
                        PlaySimple2D(Config.VCJoinSound, false);
                } else
                {
                    if(wantSounds)
                        PlaySimple2D(Config.VCLeaveSound, false);
                }

                lastKnownPlayerCount = VCClient.TotalConnectedPlayers;
            }
        }

        int lastKnownPlayerCount = 0;

        private string lastBotJoinAttemptName = null;
        private DateTime lastBotJoinAttemptTime = new DateTime();
        private int botJoinAttemptOffsetMsec = 0;
        private bool wasConnectedToVoice = false;


        private uint _dynamicObjectIndex = 0;
        private void DynamicObjectsTick(double dt, PlayerPos playerPos)
        {
            int objectCount = WorldObjects.Count;
            if (objectCount == 0)
                return;

            // target 1 per 10fps...   max of i guess the whole set
            int numToCheck = Math.Min(objectCount, Math.Max(1, (int)Math.Ceiling((1.0 / dt) / 10.0)));
            //Log($"check OBJECTS {numToCheck}");
            {

                // lets pre-filter objects list to compatible landblock and container
                PerfTrack.Start("prefilter objects");
                List<ShadowObject> objects = new List<ShadowObject>();
                //foreach (ShadowObject obj in WorldObjects)
                for (int x = 0; x < numToCheck; x++)
                {
                    ShadowObject obj = WorldObjects[(int)unchecked(_dynamicObjectIndex++) % objectCount];

                    if (!obj.Position.IsCompatibleWith(playerPos.ObjectPos) && !obj.Position.IsCompatibleWith(playerPos.CameraPos))
                        continue;

                    // ignore items that were previously on ground but now picked up
                    if (obj.Values(LongValueKey.Container) != 0)
                        continue;

                    objects.Add(obj);
                }


                // any objects to scan?
                if (objects.Count == 0)
                    return;

                Config.SoundSourceDynamic[] dynamicSources = Config.FindSoundSourcesDynamic();

                foreach (Config.SoundSourceDynamic src in dynamicSources)
                {
                    List<WorldObject> finalObjects = new List<WorldObject>();

                    foreach (ShadowObject obj in objects)
                    {
                        double dist = (playerPos.Position(src).Global - obj.Position.Global).Magnitude;
                        if (dist > src.Sound.maxdist)
                            continue;

                        if (!src.CheckObject(obj))
                            continue;

                        finalObjects.Add(obj.Object);
                    }

                    foreach (WorldObject obj in finalObjects)
                        PlayForObject(obj, src);
                }
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
#if DEBUG
                        Log($"Creating file stream: {name}");
#endif
                        string filepath = GenerateDataPath(name);
                        if (!File.Exists(filepath))
                            return null;

                        snd = Audio.GetFileStream(name, filepath, mode, looping);
                    }
                    else
                    {
#if DEBUG
                        Log($"Loading file to RAM: {name}");
#endif
                        byte[] buf = PluginCore.ReadDataFile(name);
                        if (buf == null || buf.Length == 0)
                            return null;

                        snd = Audio.GetSound(name, buf, mode, looping);
                    }

                    pt.Stop();
#if DEBUG
                    Log($"loading took {(pt.Duration * 1000.0).ToString("#0.0")}msec");
#endif
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
            if (!SoundCache.TryGetValue(snd, out sce))
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
                if (IsSong)
                {
                    if (!_WantSongPlay)
                    {
#if DEBUG
                        Log("WE WANT SONG");
#endif
                        _WantSongPlay = true;
                    }

                    return;
                }


                Log($"PLAYING: {Source.Sound.file}");

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
                if (IsSong)
                {
                    if (_WantSongPlay)
                    {
#if DEBUG
                        Log("WE DONT WANT SONG");
#endif
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
                if (Channel != null)
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
                    return SmithInterop.Position(WorldObject) ?? Position.Invalid;
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

#if DEBUG
            Log($"added static ambient {src.Sound.file} at {pos}");
#endif
        }

        public void PlayForObject(WorldObject obj, Config.SoundSource src)
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
                        return;

                    // changing sounds? kill existing
                    //oa.Stop();

                    //ActiveAmbients.Remove(oa);

                    //break;
                }
            }


            // start new sound
            ObjectAmbient newoa = new ObjectAmbient(src, obj.Id);

            ActiveAmbients.Add(newoa);

#if DEBUG
            Log($"added weenie ambient {src.Sound.file} from ID {obj.Id}");
#endif
        }

        public static byte[] ReadDataFile(string filename)
        {
            return System.IO.File.ReadAllBytes(GenerateDataPath(filename));
        }

        public static string GenerateDataPath(string filename)
        {
            return System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "data"), filename);
        }

        public List<ShadowObject> FilterByDistance(IEnumerable<ShadowObject> objects, Vec3 pt, double dist)
        {
            List<ShadowObject> objs = new List<ShadowObject>();

            foreach (ShadowObject obj in objects)
            {
                if ((pt - obj.GlobalCoords).Magnitude > dist)
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
            if (e.Type == PortalEventType.EnterPortal)
            {
                // start sound
#if DEBUG
                Log("changeportalmode START");
#endif

                IsPortaling = true;
                StartPortalSong();
            }
            else
            {
#if DEBUG
                Log("changeportalmode DONE");
#endif

                // dont have to do here..  Process will pick it up
                //TryMusic();

                IsPortaling = false;


                if (NeedFirstLoginPlayerWeenie)
                {
                    NeedFirstLoginPlayerWeenie = false;

#if DEBUG
                    Log("INITIALIZE VOICECHAT");
#endif
                    VCClient.Init(Core.CharacterFilter.AccountName, Player.Name, Player.Id);
                }
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

                    if (dist < closestDist)
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
                if (musicSound != null)
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

        public bool NeedFirstLoginPlayerWeenie = true;

        [BaseEvent("LoginComplete", "CharacterFilter")]
        private void CharacterFilter_LoginComplete(object sender, EventArgs e)
        {
            WriteToChat($"Startup");


#if false
            for(int x=0; x<32; x++)
            {
                Instance.Host.Actions.AddChatText($"Color {x}", x);
            }
#endif


            // even though we're logged in, we havent spawned in-game yet.  set flag to do logic upon first gamespawn
            NeedFirstLoginPlayerWeenie = true;



            //Log($"hotkeysystem     running:{VHotkeySystem.Running}    realinstance:{VHotkeySystem.InstanceReal}");

            RegisterHotkey(Hotkey_PTT, Hotkey_PTT_Desc);
            RegisterHotkey(Hotkey_PTT_Allegiance, Hotkey_PTT_Allegiance_Desc);
            RegisterHotkey(Hotkey_PTT_Fellowship, Hotkey_PTT_Fellowship_Desc);


            // start our world timer
            LoginCompleteTimestamp = PerfTimer.Timestamp;
        }

        void RegisterHotkey(string name, string desc)
        {
            VHotkeySystem hksys = VHotkeySystem.InstanceReal;
            if (hksys == null)
                return;

            VHotkeyInfo hk = new VHotkeyInfo(name, desc, 0, false, false, false);
            hk.Fired2 += Hk_Fired2;

            hksys.AddHotkey(hk);
        }

        private static void Hk_Fired2(object sender, VHotkeyInfo.cEatableFiredEventArgs e)
        {
            //Log($"HOTKEY: {sender}   eat:{e.Eat}");
            //Log($"HOTKEY {DateTime.Now.Subtract(lastHotkeyTime).TotalMilliseconds}msec since last");

            /*if(!object.ReferenceEquals(lastHotkey, sender))
            {
                Log($"HOTKEY: {(sender as VHotkeyInfo).VirtualKey}");
            }*/

            lastHotkey = (sender as VHotkeyInfo);
        }

        static VHotkeyInfo lastHotkey = null;

        const string Hotkey_PTT = "PushToTalk";
        const string Hotkey_PTT_Desc = "Push-to-talk";

        const string Hotkey_PTT_Allegiance = "PushToTalk_Allegiance";
        const string Hotkey_PTT_Allegiance_Desc = "Push-to-talk (allegiance only)";

        const string Hotkey_PTT_Fellowship = "PushToTalk_Fellowship";
        const string Hotkey_PTT_Fellowship_Desc = "Push-to-talk (fellowship only)";


        // maybe remove if we use decal/virindi hotkey system
        [DllImport("User32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private static bool IsVirtualKeyHeld(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        VHotkeyInfo GetValidHotkey(string name)
        {
            VHotkeySystem hksys = VHotkeySystem.InstanceReal;
            if (hksys == null)
                return null;

            VHotkeyInfo hotkey = hksys.GetHotkeyByName(name);
            if (hotkey == null || !hotkey.Enabled || hotkey.VirtualKey == 0)
                return null;

            return hotkey;
        }

        bool IsHotkeyHeld(string name)
        {
#if true
            if (lastHotkey == null || lastHotkey.HotkeyName != name)
                return false;

            VHotkeyInfo hotkey = lastHotkey;
#else
            VHotkeyInfo hotkey = GetValidHotkey(name);
            if (hotkey == null)
                return false;
#endif

            if (
                hotkey.ShiftState != (IsVirtualKeyHeld(16/*shift*/) || IsVirtualKeyHeld(160/*left shift*/) || IsVirtualKeyHeld(161/*right shift*/)) ||
                hotkey.ControlState != (IsVirtualKeyHeld(17/*control*/) || IsVirtualKeyHeld(162/*left control*/) || IsVirtualKeyHeld(163/*right control*/)) ||
                hotkey.AltState != (IsVirtualKeyHeld(18/*alt/menu*/) || IsVirtualKeyHeld(164/*left alt/menu*/) || IsVirtualKeyHeld(165/*right alt/menu*/))
                )
                return false;

            // virindi says middlemouse button is -5  yet  VK_MBUTTON is 4
            if (hotkey.VirtualKey < 0)
                return IsVirtualKeyHeld(-hotkey.VirtualKey - 1);

            return IsVirtualKeyHeld(hotkey.VirtualKey);
        }

        /// <summary>
        /// This is called when the plugin is shut down. This happens only once.
        /// </summary>
        protected override void Shutdown()
        {
            VCClient.Shutdown();
            Music.Shutdown();
            Audio.Shutdown();

            SoundCache.Clear();



            //Log("unhook stuff");
            Core.ChatBoxMessage -= _ChatBoxMessage;
            Core.CommandLineText -= _CommandLineText;
            Core.CharacterFilter.Logoff -= _CharacterFilter_Logoff;
            Core.CharacterFilter.ChangeFellowship -= _CharacterFilter_ChangeFellowship;
            Core.RenderFrame -= _Process;


            Log("----------------------------------------------------------------------");
            Log("                           ACAudio Shutdown");
            Log("----------------------------------------------------------------------");
        }

        List<string> mutedPlayerNames = new List<string>();

        bool IsPlayerMuted(string plrName)
        {
            foreach (string s in mutedPlayerNames)
                if (s.Equals(plrName, StringComparison.InvariantCultureIgnoreCase))
                    return true;

            return false;
        }

        private void _CommandLineText(object sender, ChatParserInterceptEventArgs e)
        {
            /*if (false)
            {

                //Do not execute as AC command.
                e.Eat = true;
            }*/



            // acaudio commands
            if (e.Text.Length > 1 && (e.Text[0] == '/' || e.Text[0] == '@'))
            {
                string ln = e.Text.Substring(1);

                if(ln.StartsWith("mute", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (ln.StartsWith("mutelist", StringComparison.InvariantCultureIgnoreCase))
                    {
                        WriteToChat("Initiating /mutelist report:");
                        WriteToChat("Use '/mute playername' or '/unmute playername'  to manipulate this list.");
                        WriteToChat($"------ {mutedPlayerNames.Count} muted players ------");
                        foreach (string plrName in mutedPlayerNames)
                            WriteToChat(plrName);
                        WriteToChat("-----------------------------");

                        e.Eat = true;
                    }
                    else
                    {
                        // assume "add"
                        int i = ln.IndexOf(' ');
                        if (i != -1)
                        {
                            string plrName = ln.Substring(i + 1).Trim();

                            if (!string.IsNullOrEmpty(plrName))
                            {
                                if (!IsPlayerMuted(plrName))
                                {
                                    WriteToChat($"Muted {plrName}");
                                    mutedPlayerNames.Add(plrName);

                                    e.Eat = true;
                                }
                            }
                        }
                    }
                } else if(ln.StartsWith("unmute", StringComparison.InvariantCultureIgnoreCase))
                {
                    int i = ln.IndexOf(' ');
                    if(i != -1)
                    {
                        string plrName = ln.Substring(i + 1).Trim();

                        if (!string.IsNullOrEmpty(plrName))
                        {
                            for (int x = 0; x < mutedPlayerNames.Count; x++)
                                if (mutedPlayerNames[x].Equals(plrName, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    WriteToChat($"Unmuted {plrName}");
                                    mutedPlayerNames.RemoveAt(x);

                                    e.Eat = true;

                                    break;
                                }
                        }
                    }
                }

            }




            // we want to catch a manual outgoing /tell here rather than wait for server to issue a chat message just in case acavcserver manages
            // to give us a ACA* packet before we saw that we /tell'd.
            //
            // this will allow us to remember if the player actually wanted to  /tell join  in advance, to prevent possible haxx exploit
            // where a modified acavcserver could force players to join their voice servers automatically.


            string tellTarget = null;
            string tellMessage = null;

            foreach(string tellPrefix in new string[]
            {
                "tell",
                "t",
                "send",
                "whisper",
                "w"
            })
                if(e.Text.StartsWith("@" + tellPrefix, StringComparison.InvariantCultureIgnoreCase) ||
                    e.Text.StartsWith("/" + tellPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    string ln = e.Text.Substring(1 + tellPrefix.Length);

                    int i = ln.IndexOf(',');
                    if (i != -1)
                    {
                        tellTarget = ln.Substring(0, i).Trim();
                        tellMessage = ln.Substring(i + 1).Trim();
                        break;
                    }
                }

            foreach (string retellPrefix in new string[]
            {
                "retell",
                "rt",
            })
                if (e.Text.StartsWith("@" + retellPrefix, StringComparison.InvariantCultureIgnoreCase) ||
                    e.Text.StartsWith("/" + retellPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    tellTarget = lastTellTarget;
                    tellMessage = e.Text.Substring(1 + retellPrefix.Length).Trim();
                }


            if (!string.IsNullOrEmpty(tellTarget) && !string.IsNullOrEmpty(tellMessage))
            {
                //Log($"TELL {tellTarget}, {tellMessage}");

                if (tellMessage.Equals("join", StringComparison.InvariantCultureIgnoreCase))
                    lastBotJoinAttemptName = tellTarget;
                else
                    lastBotJoinAttemptName = null;//clear unless we specifically issued a 'join' request!

                lastTellTarget = tellTarget;
            }

        }

        private string lastTellTarget = null;// for   /retell join

        public static Stream GetEmbeddedFile(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream($"{PluginName}.{name}");
        }

        public static void WriteToChat(string message)
        {
            try
            {
                Instance.Host.Actions.AddChatText($"[{PluginName}] {message}", 18);
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
