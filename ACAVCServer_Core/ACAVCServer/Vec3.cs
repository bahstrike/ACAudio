using System;
using System.Diagnostics.CodeAnalysis;

namespace ACAVCServer
{
    internal struct Vec3
    {
        public double x, y, z;

        public Vec3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vec3 Zero
        {
            get
            {
                Vec3 t = new Vec3();
                t.x = 0.0;
                t.y = 0.0;
                t.z = 0.0;
                return t;
            }
        }

        public static bool operator ==(Vec3 a, Vec3 b)
        {
            return (a.x == b.x &&
                a.y == b.y &&
                a.z == b.z);
        }

        public static bool operator !=(Vec3 a, Vec3 b)
        {
            return (a.x != b.x ||
                a.y != b.y ||
                a.z != b.z);
        }

        public override bool Equals(object obj)
        {
            return (this == (Vec3)obj);
        }

        public override int GetHashCode()
        {
            return unchecked((int)x + (int)y + (int)z);
        }

        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            Vec3 v = new Vec3();
            v.x = a.x + b.x;
            v.y = a.y + b.y;
            v.z = a.z + b.z;
            return v;
        }

        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            Vec3 v = new Vec3();
            v.x = a.x - b.x;
            v.y = a.y - b.y;
            v.z = a.z - b.z;
            return v;
        }

        public double Magnitude
        {
            get
            {
                return Math.Sqrt(x * x + y * y + z * z);
            }
        }
    }
}
