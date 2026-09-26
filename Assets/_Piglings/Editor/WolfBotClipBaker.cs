using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Piglings.EditorTools
{
    /// <summary>
    /// Piglings ▸ Animation ▸ Bake WolfBot Clips.
    /// Reads the Wolf-Bot Rig Tester keyframes (Source/*.json) and writes them into WolfBot_Climb / WolfBot_Break.
    /// Workflow: tweak a pose in the tester → Export JSON → paste into the Source file → bake again.
    /// Hand edits inside the .anim files are overwritten by the next bake.
    /// </summary>
    public static class WolfBotClipBaker
    {
        const string PrefabPath = "Assets/_Piglings/Prefabs/WolfBot.prefab";
        const string ClipFolder = "Assets/_Piglings/Art/WolfBot/Animations/";
        const string SourceFolder = ClipFolder + "Source/";

        // 30 samples per second: smooth at game speed, few enough keys to inspect in the Animation window.
        const float SampleRate = 30f;

        // The tester draws at 1x (300 px canvas). The PSB is 3x at 300 PPU, so one tester pixel = 3 / 300 units.
        const float UnitsPerTesterPixel = 0.01f;

        // The PSD importer adds "_1" to each bone because a sprite object already has the same name.
        const string BoneSuffix = "_1";

        // If RobotView also switches the eyes in code, set this to false: one owner per piece of state.
        const bool BakeEyes = true;

        // Tester slider name == sprite name == bone name without the suffix.
        static readonly string[] Joints =
        {
            "ear_F", "ear_B", "antenna",
            "arm_L_upper", "arm_L_lower", "arm_R_upper", "arm_R_lower",
            "leg_L_upper", "leg_L_lower", "leg_R_upper", "leg_R_lower",
            "tail_1", "tail_2",
        };

        // Break-apart, copied from the tester's "pop" table: the bone that flies off, its outward direction
        // (tester pixels, y pointing down), which way it spins, and the sprites that fade with it.
        sealed class PopPart
        {
            public string Bone;
            public Vector2 Direction;
            public float Spin;
            public string[] Sprites;
        }

        static readonly PopPart[] PopParts =
        {
            new PopPart { Bone = "tail_1", Direction = new Vector2(60, 40), Spin = 300, Sprites = new[] { "tail_1", "tail_2" } },
            new PopPart { Bone = "arm_L_upper", Direction = new Vector2(-120, -90), Spin = -420, Sprites = new[] { "arm_L_upper", "arm_L_lower" } },
            new PopPart { Bone = "arm_R_upper", Direction = new Vector2(120, -90), Spin = 420, Sprites = new[] { "arm_R_upper", "arm_R_lower" } },
            new PopPart { Bone = "leg_L_upper", Direction = new Vector2(-90, 20), Spin = -300, Sprites = new[] { "leg_L_upper", "leg_L_lower" } },
            new PopPart { Bone = "leg_R_upper", Direction = new Vector2(90, 20), Spin = 300, Sprites = new[] { "leg_R_upper", "leg_R_lower" } },
            new PopPart { Bone = "antenna", Direction = new Vector2(-40, -140), Spin = -600, Sprites = new[] { "antenna" } },
        };

        static readonly (string state, string sprite)[] Eyes = { ("on", "eye_on"), ("red", "eye_red"), ("x", "eye_x") };

        [MenuItem("Piglings/Animation/Bake WolfBot Clips")]
        static void BakeAll()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var rig = new Rig(root.transform);
                bool ok = Bake(rig, "WolfBot_Climb", loop: true) & Bake(rig, "WolfBot_Break", loop: false);
                AssetDatabase.SaveAssets();
                if (ok) Debug.Log("[WolfBotClipBaker] Baked WolfBot_Climb and WolfBot_Break.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool Bake(Rig rig, string clipName, bool loop)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(SourceFolder + clipName + ".json");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + clipName + ".anim");
            if (source == null || clip == null)
            {
                Debug.LogError($"[WolfBotClipBaker] Missing {SourceFolder}{clipName}.json or {ClipFolder}{clipName}.anim");
                return false;
            }

            var anim = new TesterAnimation(source.text);
            var builder = new ClipBuilder();
            int frames = Mathf.CeilToInt(anim.Length * SampleRate);
            for (int f = 0; f <= frames; f++)
            {
                float time = Mathf.Min(f / SampleRate, anim.Length);
                WritePose(rig, anim.Sample(time), time, builder);
            }
            builder.WriteTo(clip, loop);
            return true;
        }

        static void WritePose(Rig rig, TesterAnimation.Pose p, float time, ClipBuilder clip)
        {
            // Rotations: the tester stores clockwise-positive offsets from the rest pose;
            // Unity stores counter-clockwise-positive absolute angles. So: Unity Z = rest Z − tester value.

            // Body: the tester moves, tilts and squashes the whole robot around the ball's center — where body_1 sits.
            var body = rig.Bone("body");
            float squash = p.Get("squash") / 100f;
            var bodyPosition = body.Position + new Vector3(p.Get("root_x"), -p.Get("root_y"), 0f) * UnitsPerTesterPixel;
            // body_1 points up, so its local X axis is the screen's vertical: stretch X, squeeze Y.
            var bodyScale = Vector3.Scale(body.Scale, new Vector3(1f + squash, 1f - squash * 0.5f, 1f));
            clip.AddTransform(body.Path, time, bodyPosition, WithZ(body.Euler, body.Euler.z - p.Get("body_rot")), bodyScale);

            float pop = p.Get("pop") / 100f;
            var fade = new Dictionary<string, float>();

            foreach (var joint in Joints)
            {
                var bone = rig.Bone(joint);
                var position = bone.Position;
                var scale = bone.Scale;
                float z = bone.Euler.z - p.Get(joint);

                var part = FindPopPart(joint);
                if (part != null)
                {
                    ApplyPop(part, pop, body, ref position, ref z, ref scale);
                    // Same fade curve as the tester: parts start fading once they collapse (pop > 30%).
                    float alpha = pop > 0.3f ? Mathf.Max(0f, 1f - Mathf.Pow((pop - 0.3f) / 0.7f, 1.5f)) : 1f;
                    foreach (var sprite in part.Sprites) fade[sprite] = alpha;
                }
                clip.AddTransform(bone.Path, time, position, WithZ(bone.Euler, z), scale);
            }

            foreach (var pair in fade) clip.AddAlpha(rig.Sprite(pair.Key), time, pair.Value);

            // Sparks flash while the limbs crack, then fade out. Outside the break they stay hidden.
            bool sparksOn = pop > 0f && pop < 0.75f;
            clip.AddActive(rig.Sprite("sparks"), time, sparksOn);
            clip.AddAlpha(rig.Sprite("sparks"), time, pop < 0.3f ? 1f : Mathf.Max(0f, 1f - (pop - 0.3f) / 0.45f));

            if (BakeEyes)
            {
                string eye = p.Swap("eye", "on");
                foreach (var (state, sprite) in Eyes) clip.AddActive(rig.Sprite(sprite), time, eye == state);
            }
        }

        // The tester's break: a short outward jerk (peaks at pop 30%), then the part shrinks into the ball's
        // center while spinning 40°. body_1's origin IS the ball's center, so scaling the bone's local position
        // pulls the part toward it.
        static void ApplyPop(PopPart part, float pop, Rig.BoneRest body, ref Vector3 position, ref float z, ref Vector3 scale)
        {
            if (pop <= 0f) return;

            float jerk = pop < 0.3f ? pop / 0.3f : Mathf.Max(0f, 1f - (pop - 0.3f) / 0.2f);
            float t = Mathf.Clamp01((pop - 0.3f) / 0.7f);
            float collapse = t * t * (3f - 2f * t);
            float spin = -(part.Spin > 0f ? 1f : -1f) * 40f * collapse; // tester spins clockwise-positive

            position = Quaternion.Euler(0f, 0f, spin) * position * (1f - 0.9f * collapse);
            scale *= 1f - 0.9f * collapse;
            z += spin;

            // The jerk is 7 tester pixels along the part's outward direction, measured on screen.
            // Turn it from screen space (y down) into body_1's local space (which is rotated to point up).
            var screen = new Vector3(part.Direction.x, -part.Direction.y, 0f).normalized * (7f * jerk * UnitsPerTesterPixel);
            position += Quaternion.Euler(0f, 0f, -body.Euler.z) * screen;
        }

        static PopPart FindPopPart(string joint)
        {
            foreach (var part in PopParts)
                if (part.Bone == joint) return part;
            return null;
        }

        static Vector3 WithZ(Vector3 euler, float z) => new Vector3(euler.x, euler.y, z);

        /// <summary>Rest pose and animation paths of the rig, read from the prefab so nothing is typed by hand.</summary>
        sealed class Rig
        {
            public sealed class BoneRest
            {
                public string Path;
                public Vector3 Position;
                public Vector3 Euler;
                public Vector3 Scale;
            }

            readonly Transform root;
            readonly Dictionary<string, Transform> byName = new Dictionary<string, Transform>();

            public Rig(Transform root)
            {
                this.root = root;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
            }

            public BoneRest Bone(string name)
            {
                var t = Find(name + BoneSuffix);
                return new BoneRest
                {
                    Path = AnimationUtility.CalculateTransformPath(t, root),
                    Position = t.localPosition,
                    Euler = t.localEulerAngles,
                    Scale = t.localScale,
                };
            }

            public string Sprite(string name) => AnimationUtility.CalculateTransformPath(Find(name), root);

            Transform Find(string name)
            {
                if (byName.TryGetValue(name, out var t)) return t;
                throw new KeyNotFoundException($"[WolfBotClipBaker] No object named '{name}' in {PrefabPath}");
            }
        }
    }
}
