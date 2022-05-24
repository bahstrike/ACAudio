using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace UtilityBelt.Lib
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Frame
    {
        public IntPtr vtable;
        public uint landblock;

        // quat
        public float qw, qx, qy, qz;

        // matrix
        public float m11, m12, m13;
        public float m21, m22, m23;
        public float m31, m32, m33;

        // vec
        public float x, y, z;

        public unsafe static Frame Get(int obj)
        {
            Frame* f = (Frame*)obj;
            return *f;
        }

        public override string ToString()
        {
            return $"landblock: 0x{landblock:X8}\nqw: {qw}, qx: {qx}, qy: {qy}, qz: {qz}\nm11: {m11}, m12: {m12}, m13: {m13}\nm21: {m21}, m22: {m22}, m23: {m23}\nm31: {m31}, m32: {m32}, m33: {m33}\nx: {x}, y: {y}, z: {z}";
        }
    }
}
