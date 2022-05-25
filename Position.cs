using System;
using System.Collections.Generic;
using System.Text;
using Smith;
using Decal.Adapter.Wrappers;

namespace ACAudio
{
    public struct Position
    {
        public uint Landblock;
        public Vec3 Local;

        private int LandblockX { get { return (int)((Landblock & 0xFF000000)>>24); } }
        private int LandblockY {  get { return (int)((Landblock & 0x00FF0000) >> 16); } }
        private int LandblockArea {  get { return (int)((Landblock & 0x0000FF00) >> 8); } }
        //private int LandblockCell { get { return (int)(Landblock & 0x000000FF); } }//1-based

        //private int CellX { get { return (LandblockCell - 1) / 8; } }
        //private int CellY { get { return (LandblockCell - 1) % 8; } }

        public bool Terrain { get { return (LandblockArea == 0); } }

        private Vec2 LandblockGlobalOffset
        {
            get
            {
                return new Vec2((double)LandblockX * 192.0, (double)LandblockY * 192.0);
            }
        }

        public Vec3 Global
        {
            get
            {
                return Local + LandblockGlobalOffset.At3DZ(0.0);
            }

            set
            {
                Local = value - LandblockGlobalOffset.At3DZ(0.0);
            }
        }

        private Position(uint _Landblock)
        {
            Landblock = _Landblock;
            Local = Vec3.Zero;
        }

        private Position(uint _Landblock, Vec3 _Local)
        {
            Landblock = _Landblock;
            Local = _Local;
        }

        public static Position FromLocal(uint landblock, Vec3 local)
        {
            return new Position(landblock, local);
        }

        public static Position FromLocal(uint landblock, double localX, double localY, double localZ)
        {
            return new Position(landblock, new Vec3(localX, localY, localZ));
        }

        public static Position FromGlobal(uint landblock, Vec3 global)
        {
            Position p = new Position(landblock);

            p.Global = global;

            return p;
        }

        public static Position? FromObject(WorldObject obj)
        {
            if (obj == null)
                return null;

            uint landblock = (uint)obj.Values(LongValueKey.Landblock);
            if (landblock == 0)
                return null;

            return FromLocal(landblock, SmithInterop.Vector(obj.RawCoordinates()));
        }

        public override string ToString()
        {
            return $"0x{Landblock.ToString("X8")}, {Local}";
        }
    }
}
