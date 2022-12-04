using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Decal.Adapter.Wrappers;
using VirindiViewService;
using VirindiViewService.Controls;
using Smith;
using ACACommon;

namespace ACAudio
{
    public class HudProxyMap : HudControl
    {

        long lastDrawTimestamp = 0;

        Bitmap backbuffer = null;

        private void DrawSource(Graphics gfx, double drawScale, Box2 drawRC, Vec2 pt, double maxdist, Config.SoundSource src, bool isAmbient)
        {
            // provide a fill for "2d" sounds like songs or ambience to really show their triggers
            if (PlayerPos.DetermineMode(src.Sound.mode) == PlayerPos.Mode.Object)
                gfx.FillEllipse(new SolidBrush(Color.FromArgb(15, isAmbient ? Color.Blue: Color.FromArgb(60, 60, 60))), (Rectangle)Box2.Around(drawRC.Center + pt, Vec2.One * maxdist * 2.0 * drawScale));

            gfx.FillRectangle(new SolidBrush(isAmbient ? Color.SpringGreen : Color.FromArgb(60, 60, 60)), (Rectangle)Box2.Around(drawRC.Center + pt, Vec2.One * 3.0));
            gfx.DrawEllipse(new Pen(Color.FromArgb(25, isAmbient ? Color.SpringGreen : Color.FromArgb(60, 60, 60)), 1.0f), (Rectangle)Box2.Around(drawRC.Center + pt, Vec2.One * maxdist * 2.0 * drawScale));
        }

        public override void DrawNow(DxTexture iSavedTarget)
        {
            long curDrawTimestamp = PerfTimer.Timestamp;
            double dt = Math.Min(1.0 / 20.0, Math.Max(1.0, PerfTimer.TimeBetween(lastDrawTimestamp, curDrawTimestamp)));
            lastDrawTimestamp = curDrawTimestamp;

            Box2 controlRC = new Box2(ClipRegion);

#if true
            Color transparent = Color.FromArgb(255, 0, 255);
            int rcWidth = (int)Math.Ceiling(controlRC.Width);
            int rcHeight = (int)Math.Ceiling(controlRC.Height);

            if (backbuffer == null || backbuffer.Width != rcWidth || backbuffer.Height != rcHeight)
            {
                if(backbuffer != null)
                {
                    backbuffer.Dispose();
                    backbuffer = null;
                }

                backbuffer = new Bitmap(rcWidth, rcHeight);
            }

            Box2 drawRC = Box2.At(Vec2.Zero, controlRC.Size);
            using (Graphics gfx = Graphics.FromImage(backbuffer))
            {
                // clear
                gfx.FillRectangle(new SolidBrush(Color.FromArgb(16, 16, 16)), (Rectangle)drawRC);


                PluginCore pc = PluginCore.Instance;
                if (pc == null)
                    return;


                WorldObject player = pc.Player;
                int playerID = player.Id;

                Position? _playerPos = SmithInterop.Position(player);
                if (!_playerPos.HasValue)
                    return;


#if true
                Position camPos;
                Mat4 camMat;
                SmithInterop.GetCameraInfo(out camPos, out camMat);

                Vec3 viewPos = camPos.Global;
                Vec2 viewVec = camMat.Forward.XY;

                viewVec.y *= -1.0;
#else
            Vec3 playerPos = _playerPos.Value.Global;
            Vec2 playerLookVec = new Vec2(MathLib.ToRad(pc.Player.Values(DoubleValueKey.Heading) - 90.0));
#endif


#if true
                gfx.Transform = Mat3.RotationAround(drawRC.Center, camMat.Forward.XY.Angle + MathLib.ToRad(-90.0));
#else
                // lol lets set up an inverse rotation matrix from player (not camera) heading
                gfx.Transform = Mat3.RotationAround(rc.Center, MathLib.ToRad(pc.Player.Values(DoubleValueKey.Heading) - 90.0)).Inverse;
#endif

                double drawScale = 13.0 / ((_playerPos.Value.Global - camPos.Global).Magnitude);


                double searchDist = 200.0;// make artifically high since proxymap should show sounds even out of range (should really calculate this based on zoom level and control size)




                // draw "player" in center UI
                //iSavedTarget.Fill((Rectangle)Box2.Around(rc.Center, Vec2.One * 8.0), Color.Orange);
                gfx.FillEllipse(new SolidBrush(Color.Orange), (Rectangle)Box2.Around(drawRC.Center, Vec2.One * 8.0));

                //gfx.FillEllipse(new SolidBrush(Color.Yellow), (Rectangle)Box2.Around((playerPos - camPos.Global).XY.MultComps(1.0, -1.0) * drawScale, Vec2.One * 4.0));

                //iSavedTarget.DrawLine(rc.Center, rc.Center + playerLookVec * 12.0, Color.Orange, 2.0f);
#if true
                gfx.FillEllipse(new SolidBrush(Color.Yellow), (Rectangle)Box2.Around(drawRC.Center + (_playerPos.Value.Global - viewPos).XY.MultComps(1.0, -1.0) * drawScale, Vec2.One * 4.0));
                gfx.DrawLine(new Pen(Color.Orange, 2.0f), drawRC.Center, drawRC.Center + (_playerPos.Value.Global - viewPos).XY.MultComps(1.0, -1.0) * drawScale);
#else
                gfx.DrawLine(new Pen(Color.Orange, 2.0f), rc.Center, rc.Center + viewVec * 12.0);
#endif




                // static ambients
                if (true)
                    foreach (PluginCore.StaticPosition sp in PluginCore.Instance.StaticPositions)
                    {
                        if (!sp.Position.IsCompatibleWith(camPos))
                            continue;


                        Config.SoundSourceStatic src = Config.FindSoundSourceStatic(sp.ID);
                        if (src == null)
                            continue;


                        Vec3 offset = (sp.Position.Global - viewPos);

                        if (offset.Magnitude > searchDist)//src.Sound.maxdist)
                            continue;


                        Vec2 pt = offset.XY * drawScale;

                        // up is down, down is up :P
                        pt.y *= -1.0;

                        //PluginCore.Log($"lol {pt}");


                        bool isAmbient = false;
                        double maxdist = src.Sound.maxdist;
                        foreach (PluginCore.Ambient amb in pc.ActiveAmbients)
                        {
                            PluginCore.StaticAmbient stAmb = amb as PluginCore.StaticAmbient;
                            if (stAmb != null)
                            {
                                if (stAmb.Position.Equals(sp.Position))
                                {
                                    maxdist = stAmb.FinalMaxDist;
                                    isAmbient = true;
                                    break;
                                }

                            }

                        }

                        DrawSource(gfx, drawScale, drawRC, pt, maxdist, src, isAmbient);
                    }



                // static positions
                if (true)
                    foreach (Config.SoundSourcePosition src in Config.FindSoundSourcesPosition(PlayerPos.Create()))
                    {
                        Vec3 offset = (src.Position.Global - viewPos);

                        if (offset.Magnitude > searchDist)//src.Sound.maxdist)
                            continue;


                        Vec2 pt = offset.XY * drawScale;

                        // up is down, down is up :P
                        pt.y *= -1.0;

                        //PluginCore.Log($"lol {pt}");


                        bool isAmbient = false;
                        double maxdist = src.Sound.maxdist;
                        foreach (PluginCore.Ambient amb in pc.ActiveAmbients)
                        {
                            PluginCore.StaticAmbient stAmb = amb as PluginCore.StaticAmbient;
                            if (stAmb != null)
                            {
                                if (stAmb.Position.Equals(src.Position))
                                {
                                    maxdist = stAmb.FinalMaxDist;
                                    isAmbient = true;
                                    break;
                                }

                            }

                        }

                        DrawSource(gfx, drawScale, drawRC, pt, maxdist, src, isAmbient);
                    }


                // dynamic ambients
                if (true)
                    foreach (ShadowObject obj in PluginCore.Instance.WorldObjects)
                    {
                        if (!obj.Position.IsCompatibleWith(camPos))
                            continue;

                        Vec3 offset = (obj.GlobalCoords - viewPos);

                        if (offset.Magnitude > searchDist)//src.Sound.maxdist)
                            continue;




                        Config.SoundSourceDynamic src = null;
                        foreach (Config.SoundSourceDynamic _src in Config.FindSoundSourcesDynamic())
                            if (_src.CheckObject(obj))
                            {
                                src = _src;
                                break;
                            }

                        if (src == null)
                            continue;




                        Vec2 pt = offset.XY * drawScale;

                        // up is down, down is up :P
                        pt.y *= -1.0;

                        //PluginCore.Log($"lol {pt}");


                        bool isAmbient = false;
                        double maxdist = src.Sound.maxdist;
                        foreach (PluginCore.Ambient amb in pc.ActiveAmbients)
                        {
                            PluginCore.ObjectAmbient objAmb = amb as PluginCore.ObjectAmbient;
                            if (objAmb != null)
                            {
                                if (objAmb.WeenieID == obj.Id)
                                {
                                    maxdist = objAmb.FinalMaxDist;
                                    isAmbient = true;
                                    break;
                                }

                            }

                        }

                        DrawSource(gfx, drawScale, drawRC, pt, maxdist, src, isAmbient);
                    }



            }

            iSavedTarget.DrawImage(backbuffer, (Rectangle)controlRC, transparent);
#else



            /*iSavedTarget.BeginText(Theme.GetVal<string>("DefaultTextFontFace"), (float)Theme.GetVal<int>("DefaultTextFontSize"), Theme.GetVal<int>("DefaultTextFontWeight"), false, Theme.GetVal<int>("DefaultTextFontShadowSize"), Theme.GetVal<int>("DefaultTextFontShadowAlpha"));
            iSavedTarget.WriteText($"fps:{(1.0 / dt).ToString("0.0")}", Theme.GetColor("ButtonText"), Theme.GetVal<Color>("DefaultTextFontShadowColor"), VirindiViewService.WriteTextFormats.None, ClipRegion);
            iSavedTarget.EndText();*/


#endif
                // borders
                Color borderClrLt = Color.FromArgb(200, 200, 200);
            Color borderClrDk = Color.FromArgb(32, 32, 32);
            float borderThick = 2.0f;
            iSavedTarget.DrawLine(controlRC.UL, controlRC.UR, borderClrLt, borderThick);
            iSavedTarget.DrawLine(controlRC.UR, controlRC.LR, borderClrDk, borderThick);
            iSavedTarget.DrawLine(controlRC.LR, controlRC.LL, borderClrDk, borderThick);
            iSavedTarget.DrawLine(controlRC.LL, controlRC.UL, borderClrLt, borderThick);

        }
    }
}
