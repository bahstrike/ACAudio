using System;
using System.Collections.Generic;
using System.Text;
using Smith;
using ACACommon;

namespace ACAudio
{
    public static class BVH
    {
        private static Dictionary<BVHKey, Engine.ThingBVH> Universe = new Dictionary<BVHKey, Engine.ThingBVH>();

        public static void Reset()
        {
            Universe.Clear();
        }

        public static void Add(PluginCore.StaticPosition sp, Config.SoundSourceStatic src)
        {
            Engine.ThingBVH bvh;

            BVHKey key = new BVHKey(sp.Position.Landblock);
            if (!Universe.TryGetValue(key, out bvh))
            {
                bvh = new Engine.ThingBVH();
                Universe.Add(key, bvh);
            }

            // now try to insert us
            bvh.DirtyThings.Add(new BVHEntry_StaticPosition(sp, src));
        }


        public static BVHEntry_StaticPosition[] QueryStaticPositions(Position pos)
        {
            Vec3 globalPos = pos.Global;

            List<BVHEntry_StaticPosition> ret = new List<BVHEntry_StaticPosition>();
            foreach (BVHKey key in Universe.Keys)
            {
                if (!Position.IsLandblockCompatible(pos.Landblock, key.landblock))
                    continue;

                Engine.ThingBVH bvh = Universe[key];

                List<Engine.IThing> things = new List<Engine.IThing>();
                bvh.GetRoughFromPoint(globalPos, ref things, null, false/*static bvh*/);

                foreach (Engine.IThing t in things)
                {
                    BVHEntry_StaticPosition sp = t as BVHEntry_StaticPosition;
                    if (sp == null)
                        continue;

                    double dist = (globalPos - sp.Position).Magnitude;
                    if (dist >= sp.Radius)
                        continue;

                    // keep
                    ret.Add(sp);
                }
            }

            return ret.ToArray();
        }


        public static void Process(double dt)
        {
            foreach (Engine.ThingBVH bvh in Universe.Values)
                bvh.Update(dt);
        }

        public static void GetTreeInfo(out int numBVHs, out int numNodes, out int numThings)
        {
            numBVHs = Universe.Keys.Count;
            numNodes = 0;
            numThings = 0;

            foreach (Engine.ThingBVH bvh in Universe.Values)
            {
                int n, t;
                bvh.GetTreeInfo(out n, out t);

                numNodes += n;
                numThings += t;
            }
        }



        // any landblocks that are "compatible" should return true on equality checks, as a key into dictionary of BVH instances
        private class BVHKey
        {
            public readonly uint landblock;

            public BVHKey(uint lb)
            {
                landblock = lb;
            }

            public static BVHKey From(Position pos)
            {
                return new BVHKey(pos.Landblock);
            }

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
        public class BVHEntry_StaticPosition : Engine.IThing
        {
            public readonly PluginCore.StaticPosition StaticPosition;
            public readonly Config.SoundSourceStatic Source;

            public BVHEntry_StaticPosition(PluginCore.StaticPosition _StaticPosition, Config.SoundSourceStatic _Source)
            {
                StaticPosition = _StaticPosition;
                Source = _Source;
            }

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
                    return Source.Sound.maxdist;
                }
            }

            public Vec3 Position
            {
                get
                {
                    return StaticPosition.Position.Global;
                }
            }

            public Box3 DynamicCollideBox(double dt)
            {
                return Box3.Around(Position, Vec3.One * Radius * 2.0);// donno; using for static entries to begin with
            }
        }

    }
}
