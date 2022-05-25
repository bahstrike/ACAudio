using System;
using System.Collections.Generic;
using System.Text;
using Smith;
using Decal.Adapter.Wrappers;
using UtilityBelt.Lib;

namespace ACAudio
{
    public static class SmithInterop
    {
        public static Vector3Object Vector(Vec3 v)
        {
            return new Vector3Object(v.x, v.y, v.z);
        }

        public static Vec3 Vector(Vector3Object v)
        {
            return new Vec3(v.X, v.Y, v.Z);
        }

        //used with permission by trevis (UtilityBelt)
        public static Mat4 Matrix(Frame f)
        {
            return new Mat4(
                f.m11, f.m12, f.m13, 0.0,
                f.m21, f.m22, f.m23, 0.0,
                f.m31, f.m32, f.m33, 0.0,
                f.x, f.y, f.z, 1.0
                );
        }

        public static Position Position(Frame f)
        {
            return ACAudio.Position.FromLocal(f.landblock, (double)f.x, (double)f.y, (double)f.z);
        }

        // returns Vec3.Infinite if obj is invalid
        public static Vec3 ObjectGlobalPosition(WorldObject obj)
        {
            ACAudio.Position? pos = ACAudio.Position.FromObject(obj);
            if (!pos.HasValue)
                return Vec3.Infinite;

            return pos.Value.Global;
        }
    }
}
