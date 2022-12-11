using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACAVCServerLib
{
    public struct Vec2
    {
        public double x, y;

        public Vec2(double _x, double _y)
        {
            x = _x;
            y = _y;
        }

        public Vec3 At3DZ(double z)
        {
            return new Vec3(x, y, z);
        }
    }
}
