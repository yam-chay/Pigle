using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Piglings.EditorTools
{
    /// <summary>
    /// Collects sampled values per property and writes them into an existing AnimationClip asset.
    /// The clip asset is reused (not recreated) so the Animator Controller's references to it stay valid.
    /// </summary>
    public sealed class ClipBuilder
    {
        sealed class Track
        {
            public EditorCurveBinding Binding;
            public AnimationCurve Curve = new AnimationCurve();
            public bool Stepped;
        }

        readonly Dictionary<string, Track> tracks = new Dictionary<string, Track>();

        public void Add(string path, Type type, string property, float time, float value, bool stepped = false)
        {
            string id = path + "|" + type.Name + "|" + property;
            if (!tracks.TryGetValue(id, out var track))
            {
                track = new Track { Binding = EditorCurveBinding.FloatCurve(path, type, property), Stepped = stepped };
                tracks[id] = track;
            }
            track.Curve.AddKey(new Keyframe(time, value));
        }

        // Every bone gets its full transform every frame, so both clips write the same properties
        // and nothing depends on Animator "Write Defaults" to reset a bone between states.
        public void AddTransform(string path, float time, Vector3 position, Vector3 euler, Vector3 scale)
        {
            Add(path, typeof(Transform), "m_LocalPosition.x", time, position.x);
            Add(path, typeof(Transform), "m_LocalPosition.y", time, position.y);
            Add(path, typeof(Transform), "m_LocalPosition.z", time, position.z);
            // "Raw" Euler keeps angles like 450 as-is instead of wrapping them, so rotations never flip the long way round.
            Add(path, typeof(Transform), "localEulerAnglesRaw.x", time, euler.x);
            Add(path, typeof(Transform), "localEulerAnglesRaw.y", time, euler.y);
            Add(path, typeof(Transform), "localEulerAnglesRaw.z", time, euler.z);
            Add(path, typeof(Transform), "m_LocalScale.x", time, scale.x);
            Add(path, typeof(Transform), "m_LocalScale.y", time, scale.y);
            Add(path, typeof(Transform), "m_LocalScale.z", time, scale.z);
        }

        public void AddActive(string path, float time, bool active) =>
            Add(path, typeof(GameObject), "m_IsActive", time, active ? 1f : 0f, stepped: true);

        public void AddAlpha(string path, float time, float alpha) =>
            Add(path, typeof(SpriteRenderer), "m_Color.a", time, alpha);

        public void WriteTo(AnimationClip clip, bool loop)
        {
            clip.ClearCurves();
            foreach (var track in tracks.Values)
            {
                var mode = track.Stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear;
                for (int i = 0; i < track.Curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(track.Curve, i, mode);
                    AnimationUtility.SetKeyRightTangentMode(track.Curve, i, mode);
                }
                AnimationUtility.SetEditorCurve(clip, track.Binding, track.Curve);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }
    }
}
