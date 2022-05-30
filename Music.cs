﻿using System;
using System.Collections.Generic;
using System.Text;
using Smith;

namespace ACAudio
{
    public static class Music
    {
        public static bool EnableWorld = true;
        public static bool EnablePortal = true;
        public static double Volume = 0.35;

        public class MusicChannel
        {
            public bool IsPortal;
            public Audio.Channel Channel;

            public MusicChannel(bool _IsPortal, Audio.Channel _Channel)
            {
                IsPortal = _IsPortal;
                Channel = _Channel;
            }
        }

        public static MusicChannel Channel = null;

        public static bool IsPlaying
        {
            get
            {
                return (Channel != null && Channel.Channel.IsPlaying);
            }
        }

        public static bool IsPlayingName(string name)
        {
            if (Channel == null || !Channel.Channel.IsPlaying)
                return false;

            if (!Channel.Channel.Sound.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                return false;

            return true;
        }

        private static void Log(string s)
        {
            PluginCore.Log($"MUSIC: {s}");
        }

        public static void Play(string filename, bool isPortal, double fadeTime=0.575)
        {
            // if playing something and preferences differ than ignore
            if (!EnablePortal && isPortal)
            {
                Log("prevent playing portal song");
                return;
            }

            if (!EnableWorld && !isPortal)
            {
                Log("prevent playing world song");
                return;
            }



            // if already playing something; stop it
            Stop();


            try
            {
                Audio.Sound snd = Audio.GetSound(filename, PluginCore.Instance.ReadDataFile(filename), Audio.DimensionMode._2D, true);
                if (snd == null)
                    Log("cant get music sound");
                else
                {

                    Channel = new MusicChannel(isPortal, Audio.PlaySound(snd, true));
                    if (Channel == null)
                        Log("cant make sound channel");
                    else
                    {
                        Channel.Channel.Volume = 0.0;
                        Channel.Channel.SetTargetVolume(Volume, fadeTime);

                        Channel.Channel.Play();

                        Log($"we be playin {filename}");
                    }
                }

            }
            catch (Exception ex)
            {
                Log($"portal music play BAD: {ex.Message}");
            }
        }

        public static void Stop(double fadeTime=0.575)
        {
            if (Channel != null)
            {
                Log($"beginning fadeout stop for {Channel.Channel.Sound.Name}");

                Channel.Channel.FadeToStop(fadeTime);

                // pre-forget about it?
                Channel = null;
            }
        }

        public static void Process(double dt)
        {
            if(Channel != null)
            {
                if((Channel.IsPortal && !EnablePortal) ||
                    (!Channel.IsPortal && !EnableWorld))
                {
                    Channel.Channel.Stop();
                    Channel = null;
                }

            }



            if (Channel != null)
            {
                Channel.Channel.Volume = Volume;
            }

        }

        public static void Init()
        {
            Shutdown();

            
        }

        public static void Shutdown()
        {

            if (Channel != null)
            {
                Channel.Channel.Stop();
                Channel = null;
            }
        }
    }
}