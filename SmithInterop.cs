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

        public static Mat4 Matrix(Frame f)
        {
            return new Mat4(
                f.m11, f.m12, f.m13, 0.0,
                f.m21, f.m22, f.m23, 0.0,
                f.m31, f.m32, f.m33, 0.0,
                f.x, f.y, f.z, 1.0
                );
        }
    }
}
