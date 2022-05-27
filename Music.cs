using System;
using System.Collections.Generic;
using System.Text;
using Smith;

namespace ACAudio
{
    public static class Music
    {
        public static bool Enable = true;
        public static double Volume = 0.35;

        public static Audio.Channel Channel = null;

        public static bool IsPlaying
        {
            get
            {
                return (Channel != null && Channel.IsPlaying);
            }
        }

        public static bool IsPlayingName(string name)
        {
            if (Channel == null || !Channel.IsPlaying)
                return false;

            if (!Channel.Sound.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                return false;

            return true;
        }

        private static void Log(string s)
        {
            PluginCore.Log($"MUSIC: {s}");
        }

        public static void Play(string filename, double fadeTime=0.575)
        {
            // if already playing something; stop it
            Stop();


            try
            {
                Audio.Sound snd = Audio.GetSound(filename, PluginCore.Instance.ReadDataFile(filename), Audio.DimensionMode._2D, true);
                if (snd == null)
                    Log("cant get music sound");
                else
                {

                    Channel = Audio.PlaySound(snd, true);
                    if (Channel == null)
                        Log("cant make sound channel");
                    else
                    {
                        Channel.Volume = 0.0;
                        Channel.SetTargetVolume(Volume, fadeTime);

                        Channel.Play();

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
                Log($"beginning fadeout stop for {Channel.Sound.Name}");

                Channel.FadeToStop(fadeTime);

                // pre-forget about it?
                Channel = null;
            }
        }

        public static void Process(double dt)
        {
            if (!Enable && Channel != null)
            {
                Channel.Stop();
                Channel = null;
            }



            if (Channel != null)
            {
                Channel.Volume = Volume;
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
                Channel.Stop();
                Channel = null;
            }
        }
    }
}
