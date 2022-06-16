using System;
using System.Collections.Generic;
using System.Text;
using Smith;

namespace ACAudio
{
    public class BVH
    {
        // any landblocks that are "compatible" should return true on equality checks, as a key into dictionary of BVH instances
        public class BVHKey
        {
            public uint landblock;

            public override int GetHashCode()
            {
                return unchecked((int)(landblock & 0xFFFFFF00));// bleh just compare everything except cell
            }

            public override bool Equals(object obj)
            {
                BVHKey o = obj as BVHKey;
                if (o == null)
                    return false;

                return Position.IsLandblockCompatible(landblock, o.landblock);
            }
        }


        // maybe we dont want to implement IThing here but should rather do it within Ambient itself??
        public class BVHEntry : Engine.IThing
        {
            public bool Destroyed
            {
                get
                {
                    return false;
                }
            }

            private bool _InThingBVH = false;
            public bool InThingBVH
            {
                get
                {
                    return _InThingBVH;
                }

                set
                {
                    _InThingBVH = value;
                }
            }

            public double Radius
            {
                get
                {
                    return 1.0;
                }
            }

            public Vec3 Position
            {
                get
                {
                    return Vec3.Zero;
                }
            }

            public Box3 DynamicCollideBox(double dt)
            {
                return Box3.Around(Position, Vec3.One * Radius * 2.0);
            }
        }
    }
}
