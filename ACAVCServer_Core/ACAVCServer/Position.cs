using System.IO;

namespace ACAVCServer
{
    internal struct Position
    {
        public uint Landblock;
        public Vec3 Local;

        private int LandblockX { get { return (int)((Landblock & 0xFF000000)>>24); } }
        private int LandblockY {  get { return (int)((Landblock & 0x00FF0000) >> 16); } }
        //private int LandblockArea { get { return (int)((Landblock & 0x0000FF00) >> 8); } }
        //private int LandblockCell { get { return (int)(Landblock & 0x000000FF); } }//1-based

        //private int CellX { get { return (LandblockCell - 1) / 8; } }
        //private int CellY { get { return (LandblockCell - 1) % 8; } }

        public bool IsValid { get { return (Landblock != 0); } }


#if true
        public static bool IsLandblockTerrain(uint lb)
        {
            return (lb & 0x0000FF00) == 0;
        }
        public bool IsTerrain {  get { return IsLandblockTerrain(Landblock); } }

        public static int LandblockDungeonID(uint lb)
        {
            if (IsLandblockTerrain(lb))
                return -1;

            return (int)((lb & 0xFFFF0000) >> 16);
        }

        public int DungeonID
        {
            get
            {
                return LandblockDungeonID(Landblock);
            }
        }

        public static bool IsLandblockCompatible(uint a, uint b)
        {
            // if both are terrain, then definitely compatible
            if (IsLandblockTerrain(a) && IsLandblockTerrain(b))
                return true;

            // if neither are terrain, then directly compare the dungeon ID
            if (!IsLandblockTerrain(a) && !IsLandblockTerrain(b))
                return (LandblockDungeonID(a) == LandblockDungeonID(b));

            // if one is terrain and other is dungeon then check if landblock XXYY is same, and we'll say its OK.. i guess..
            return (a & 0xFFFF0000) == (b & 0xFFFF0000);
        }

        public bool IsCompatibleWith(Position o)
        {
            // if both are terrain, then definitely compatible
            if (IsTerrain && o.IsTerrain)
                return true;

            // if neither are terrain, then directly compare the dungeon ID
            if (!IsTerrain && !o.IsTerrain)
                return (DungeonID == o.DungeonID);

            // if one is terrain and other is dungeon then check if landblock XXYY is same, and we'll say its OK.. i guess..
            return (Landblock & 0xFFFF0000) == (o.Landblock & 0xFFFF0000);
        }
#else
        public int DungeonID { get { return IsolateDungeonID(Landblock); } }    // -1 if terrain

        // returns -1 if terrain
        public static int IsolateDungeonID(uint landblock)
        {
            if ((landblock & 0x0000FF00) == 0)
                return -1;

            return (int)((landblock & 0xFFFF0000) >> 16);
        }

        public bool IsCompatibleWith(Position o)
        {
            return (DungeonID == o.DungeonID);
        }
#endif

        private Vec2 LandblockGlobalOffset
        {
            get
            {
                // if dungeon, just use local  (no offset)
                //if (DungeonID != -1)
                    //return Vec2.Zero;

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

        public static Position Invalid
        {
            get
            {
                return new Position(0);
            }
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

        public static Position FromStream(Stream s, bool floats)
        {
            BinaryReader br = new BinaryReader(s);

            uint lb = br.ReadUInt32();

            Vec3 local = new Vec3();
            if (floats)
            {
                local.x = br.ReadSingle();
                local.y = br.ReadSingle();
                local.z = br.ReadSingle();
            }
            else
            {
                local.x = br.ReadDouble();
                local.y = br.ReadDouble();
                local.z = br.ReadDouble();
            }

            return Position.FromLocal(lb, local);
        }

        public void ToStream(Stream s, bool floats)
        {
            BinaryWriter bw = new BinaryWriter(s);

            bw.Write(Landblock);

            if (floats)
            {
                bw.Write((float)Local.x);
                bw.Write((float)Local.y);
                bw.Write((float)Local.z);
            }
            else
            {
                bw.Write(Local.x);
                bw.Write(Local.y);
                bw.Write(Local.z);
            }
        }

        public override string ToString()
        {
            return $"0x{Landblock.ToString("X8")}, {Local}";
        }

        public override bool Equals(object obj)
        {
            Position o = (Position)obj;

            if (Landblock != o.Landblock)
                return false;

            return (Local == o.Local);
        }

        public override int GetHashCode()
        {
            return unchecked(
                (int)Landblock //+
                //Local.GetHashCode()
                );
        }
    }
}
