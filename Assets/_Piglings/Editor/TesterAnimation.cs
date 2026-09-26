using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Piglings.EditorTools
{
    /// <summary>
    /// Keyframes exported from a Rig Tester artifact ("Export JSON"), sampled the same way the tester plays them.
    /// Why reproduce the tester instead of converting its keys 1:1: the tester eases between keys with its own curve
    /// and computes the break-apart from a single "pop" slider. Sampling it keeps Unity identical to what was approved.
    /// </summary>
    public sealed class TesterAnimation
    {
        public sealed class Pose
        {
            public readonly Dictionary<string, float> Numbers = new Dictionary<string, float>();
            public readonly Dictionary<string, string> Swaps = new Dictionary<string, string>();

            // The tester fills every missing slider with 0 and every missing swap with its first option.
            public float Get(string key) => Numbers.TryGetValue(key, out var v) ? v : 0f;
            public string Swap(string key, string fallback) => Swaps.TryGetValue(key, out var v) ? v : fallback;
        }

        struct Key
        {
            public float Time;
            public Pose Pose;
        }

        public float Length { get; }
        readonly string ease;
        readonly List<Key> keys = new List<Key>();

        public TesterAnimation(string json)
        {
            var root = JObject.Parse(json);
            Length = root.Value<float>("length");
            ease = root.Value<string>("ease") ?? "io";

            foreach (JObject k in root["keys"])
            {
                var pose = new Pose();
                foreach (var prop in (JObject)k["p"])
                {
                    if (prop.Value.Type == JTokenType.String) pose.Swaps[prop.Key] = (string)prop.Value;
                    else pose.Numbers[prop.Key] = prop.Value.Value<float>();
                }
                keys.Add(new Key { Time = k.Value<float>("t"), Pose = pose });
            }
            keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        public Pose Sample(float time)
        {
            if (time <= keys[0].Time) return keys[0].Pose;
            if (time >= keys[keys.Count - 1].Time) return keys[keys.Count - 1].Pose;

            int i = 0;
            while (keys[i + 1].Time < time) i++;
            var a = keys[i];
            var b = keys[i + 1];
            float e = Ease((time - a.Time) / (b.Time - a.Time));

            var pose = new Pose();
            // Swaps (eye state, mouth) don't blend: they hold the earlier key's value until the next key, like the tester.
            foreach (var s in a.Pose.Swaps) pose.Swaps[s.Key] = s.Value;

            var names = new HashSet<string>(a.Pose.Numbers.Keys);
            names.UnionWith(b.Pose.Numbers.Keys);
            foreach (var n in names) pose.Numbers[n] = a.Pose.Get(n) + (b.Pose.Get(n) - a.Pose.Get(n)) * e;
            return pose;
        }

        // Same three curves as the tester's EASE table.
        float Ease(float x)
        {
            switch (ease)
            {
                case "lin": return x;
                case "out": return 1f - Mathf.Pow(1f - x, 3f);
                default: return x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
            }
        }
    }
}
