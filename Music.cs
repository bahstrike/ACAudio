using System;
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

        public static double DesiredVolume = 1.0;//scale factor
        public static double FinalVolume
        {
            get
            {
                return Volume * DesiredVolume;//scale
            }
        }

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

        private static void Log(string s)
        {
            PluginCore.Log($"MUSIC: {s}");
        }

        public static void Play(Config.SoundAttributes sound, bool isPortal)
        {
            Play(sound.file, isPortal, sound.vol, sound.fade);
        }

        private static void Play(string filename, bool isPortal, double vol, double fadeTime)
        {
            if (string.IsNullOrEmpty(filename))
            {
                Log("wanted to play nothing; stopping music");
                Stop();
                return;
            }

            // if playing something and preferences differ than ignore
            if (!EnablePortal && isPortal)
            {
                Log("prevent playing portal song");
                Stop();
                return;
            }

            if (!EnableWorld && !isPortal)
            {
                Log("prevent playing world song");
                Stop();
                return;
            }


            // if playing same filename just bail (though we can update the isPortal flag)
            if (Channel != null && Channel.Channel != null && Channel.Channel.IsPlaying &&
                Channel.Channel.Sound != null &&    // had issue where this could be null when sharing channel references and was stopped elsewhere
                Channel.Channel.Sound.Name.Equals(filename, StringComparison.InvariantCultureIgnoreCase))
            {
                Channel.IsPortal = isPortal;
                return;
            }


            // kill old music
            Stop();


            try
            {
                Audio.Sound snd = PluginCore.GetOrLoadSound(filename, Audio.DimensionMode._2D, true);
                if (snd == null)
                    Log("cant get music sound");
                else
                {

                    Channel = new MusicChannel(isPortal, Audio.PlaySound(snd, true));
                    if (Channel == null)
                        Log("cant make sound channel");
                    else
                    {
                        // store desired scale factor before we use FinalVolume
                        DesiredVolume = vol;

                        Log($"STARTING music to finalvol:{FinalVolume.ToString("#0.0")} = musicvol:{Volume.ToString("#0.0")} * desiredvol:{DesiredVolume.ToString("#0.0")}");

                        Channel.Channel.Volume = 0.0;
                        Channel.Channel.SetTargetVolume(FinalVolume, fadeTime);

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
                Log($"beginning fadeout stop for {Channel.Channel.Sound?.Name}");

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
                Channel.Channel.ChangeTargetVolume(FinalVolume);
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
