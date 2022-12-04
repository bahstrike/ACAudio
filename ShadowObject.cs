using System;
using System.Collections.Generic;
using System.Text;
using Smith;
using Decal.Adapter.Wrappers;
using ACACommon;

namespace ACAudio
{
    public class ShadowObject
    {
#if true
        public readonly int Id;
        public readonly WorldObject Object;
#else
        public readonly int ID;

        private double _Object_Timestamp = 0.0;
        private WorldObject _Object = null;
        public WorldObject Object
        {
            get
            {
                if(_Object == null || (PluginCore.Instance.WorldTime-_Object_Timestamp) > 0.1)
                {
                    _Object_Timestamp = PluginCore.Instance.WorldTime;
                    _Object = PluginCore.CoreManager.WorldFilter[ID];
                }

                return _Object;
            }
        }
#endif

        public readonly ObjectClass ObjectClass;

        public ShadowObject(WorldObject _Object)
        {
            Id = _Object.Id;
            Object = _Object;

            ObjectClass = Object.ObjectClass;
        }

        // used to help scramble query timers
        private double TimerVariance = MathLib.random.NextDouble() * (1.0 / 20.0)/*potentially offset by upwards of a frame at 20fps??*/;

        private double _Position_Timestamp = 0.0;
        private Position _Position = Position.Invalid;
        public Position Position
        {
            get
            {
                if (_Position.Equals(Position.Invalid) || (PluginCore.Instance.WorldTime - _Position_Timestamp) > 0.1 + TimerVariance)
                {
                    _Position_Timestamp = PluginCore.Instance.WorldTime;
                    _Position = SmithInterop.Position(Object) ?? Position.Invalid;
                }

                return _Position;
            }
        }

        private double _GlobalCoords_Timestamp = 0.0;
        private Vec3 _GlobalCoords = Vec3.Infinite;
        public Vec3 GlobalCoords
        {
            get
            {
                if (_GlobalCoords.Equals(Vec3.Infinite) || (PluginCore.Instance.WorldTime - _GlobalCoords_Timestamp) > 0.1 + TimerVariance)
                {
                    _GlobalCoords_Timestamp = PluginCore.Instance.WorldTime;
                    _GlobalCoords = Position.Global;
                }

                return _GlobalCoords;
            }
        }

        private double _LongKeys_Timestamp = 0.0;
        private List<int> _LongKeys = null;
        public List<int> LongKeys
        {
            get
            {
                if (_LongKeys == null || (PluginCore.Instance.WorldTime - _LongKeys_Timestamp) > 0.1 + TimerVariance)
                {
                    _LongKeys_Timestamp = PluginCore.Instance.WorldTime;

                    _LongKeys = Object.LongKeys;
                }

                return _LongKeys;
            }
        }

        // split?
        private double _LongValues_Timestamp = 0.0;
        private Dictionary<LongValueKey, int> _LongValues = new Dictionary<LongValueKey, int>();
        public int Values(LongValueKey key)
        {
            int val;
            if(!_LongValues.TryGetValue(key, out val) || (PluginCore.Instance.WorldTime - _LongValues_Timestamp) > 0.1 + TimerVariance)
            {
                _LongValues_Timestamp = PluginCore.Instance.WorldTime;

                val = Object.Values(key);
                _LongValues[key] = val;
            }

            return val;
        }

        private double _StringKeys_Timestamp = 0.0;
        private List<int> _StringKeys = null;
        public List<int> StringKeys
        {
            get
            {
                if (_StringKeys == null || (PluginCore.Instance.WorldTime - _StringKeys_Timestamp) > 0.1 + TimerVariance)
                {
                    _StringKeys_Timestamp = PluginCore.Instance.WorldTime;

                    _StringKeys = Object.StringKeys;
                }

                return _StringKeys;
            }
        }

        // split?
        private double _StringValues_Timestamp = 0.0;
        private Dictionary<StringValueKey, string> _StringValues = new Dictionary<StringValueKey, string>();
        public string Values(StringValueKey key)
        {
            string val;
            if (!_StringValues.TryGetValue(key, out val) || (PluginCore.Instance.WorldTime - _StringValues_Timestamp) > 0.1 + TimerVariance)
            {
                _StringValues_Timestamp = PluginCore.Instance.WorldTime;

                val = Object.Values(key);
                _StringValues[key] = val;
            }

            return val;
        }
    }
}
