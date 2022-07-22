using System;
using System.Collections.Generic;
using System.Text;
using Smith;
using Decal.Adapter.Wrappers;

namespace ACAudio
{
    public class PlayerPos
    {
        public enum Mode
        {
            Object,
            Camera
        }

        public readonly Position ObjectPos;

        public readonly Position CameraPos;
        public readonly Mat4 CameraMat;

        public Position Position(Mode mode)
        {
            if (mode == Mode.Camera)
                return CameraPos;
            else
                return ObjectPos;
        }

        public Position Position(Config.SoundMode mode)
        {
            return Position(DetermineMode(mode));
        }

        public static bool UsePlayerAs3DListener = false;

        public static Mode DetermineMode(Config.SoundMode mode)
        {
            switch (mode)
            {
                case Config.SoundMode.Song:
                case Config.SoundMode._2D:
                case Config.SoundMode.Hybrid:// should be some special logic for hybrid?  or just consider based off player anyway (cheap hack)
                    return Mode.Object;

                default:
                    if (UsePlayerAs3DListener)
                        return Mode.Object;

                    return Mode.Camera;
            }
        }

        public Position Position(Config.SoundAttributes attr)
        {
            return Position(attr.mode);
        }

        public Position Position(Config.SoundSource src)
        {
            return Position(src.Sound);
        }

        private PlayerPos(Position _ObjectPos, Position _CameraPos, Mat4 _CameraMat)
        {
            ObjectPos = _ObjectPos;

            CameraPos = _CameraPos;
            CameraMat = _CameraMat;
        }

        public static PlayerPos Create()
        {
            Position objectPos = SmithInterop.Position(PluginCore.Instance.Player) ?? ACAudio.Position.Invalid;

            Position cameraPos;
            Mat4 cameraMat;
            SmithInterop.GetCameraInfo(out cameraPos, out cameraMat);

            return new PlayerPos(objectPos, cameraPos, cameraMat);
        }
    }
}
