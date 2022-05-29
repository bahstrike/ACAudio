using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Decal.Adapter.Wrappers;
using VirindiViewService;
using VirindiViewService.Controls;
using Smith;

namespace ACAudio
{
    public class HudProxyMap : HudControl
    {
        public override void DrawNow(DxTexture iSavedTarget)
        {
            Box2 rc = new Box2(ClipRegion);

            iSavedTarget.Fill((Rectangle)rc, Color.FromArgb(16, 16, 16));


            PluginCore pc = PluginCore.Instance;
            if (pc == null)
                return;


            iSavedTarget.Fill((Rectangle)Box2.Around(rc.Center, Vec2.One * 8.0), Color.Orange);

            Vec2 playerLookVec = new Vec2(MathLib.ToRad(pc.Player.Values(DoubleValueKey.Heading) - 90.0));
            iSavedTarget.DrawLine(rc.Center, rc.Center + playerLookVec * 12.0, Color.Orange, 2.0f);


            WorldObject player = pc.Player;
            int playerID = player.Id;

            Position? _playerPos = Position.FromObject(player);
            if (!_playerPos.HasValue)
                return;

            Vec3 playerPos = _playerPos.Value.Global;


            double range = 35.0;
            double drawScale = rc.Size.Magnitude / range * 0.3;

            foreach (WorldObject obj in PluginCore.CoreManager.WorldFilter.GetAll())
            {
                if (obj.Id == playerID)
                    continue;

                Position? objPos = Position.FromObject(obj);
                if (!objPos.HasValue)
                    continue;

                Vec3 offset = (objPos.Value.Global - playerPos);


                Vec2 pt = offset.XY * drawScale;

                // up is down, down is up :P
                pt.y *= -1.0;

                //PluginCore.Log($"lol {pt}");


                bool isAmbient = false;
                foreach(PluginCore.Ambient amb in pc.ActiveAmbients)
                {
                    PluginCore.ObjectAmbient objAmb = amb as PluginCore.ObjectAmbient;
                    if(objAmb != null)
                    {
                        if(objAmb.WeenieID == obj.Id)
                        {
                            isAmbient = true;
                            break;
                        }

                    }

                }

                iSavedTarget.Fill((Rectangle)Box2.Around(rc.Center + pt, Vec2.One * 3.0), isAmbient ? Color.SpringGreen : Color.FromArgb(60, 60, 60));
            }



            // borders
            Color borderClrLt = Color.FromArgb(200, 200, 200);
            Color borderClrDk = Color.Black;
            float borderThick = 2.0f;
            iSavedTarget.DrawLine(rc.UL, rc.UR, borderClrLt, borderThick);
            iSavedTarget.DrawLine(rc.UR, rc.LR, borderClrDk, borderThick);
            iSavedTarget.DrawLine(rc.LR, rc.LL, borderClrDk, borderThick);
            iSavedTarget.DrawLine(rc.LL, rc.UL, borderClrLt, borderThick);
        }
    }
}
