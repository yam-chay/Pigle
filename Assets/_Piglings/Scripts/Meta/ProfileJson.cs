using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Piglings.Meta
{
    public enum ProfileReadResult
    {
        Ok,
        Corrupt,          // not JSON, or not the shape this build writes (wrong types, negative counts, empty ids…)
        UnknownVersion,   // valid JSON, but no version or one this build doesn't know (e.g. a save from a newer build)
    }

    /// <summary>
    /// The save format, version 1:
    /// <code>
    /// {
    ///   "version": 1,
    ///   "weapons": { "stone":         { "directHits": 137 } },
    ///   "robots":  { "wolfbot_basic": { "ballKnocks": 412, "knockedByBall": 300 } }
    /// }
    /// </code>
    /// One object per id (not a bare number), so a later field (feats…) is an addition, not a new version.
    /// Reading is strict about what it does read — anything our writer can't produce means the file was damaged or
    /// edited, and is reported (the caller backs it up and starts over), never half-loaded. Unknown fields are ignored
    /// and missing ones read as 0, so adding a field later stays readable both ways.
    ///
    /// Parsed with Newtonsoft's JObject (no reflection, so it's IL2CPP/AOT-safe) and checked by hand.
    /// </summary>
    public static class ProfileJson
    {
        private static readonly JsonLoadSettings Strict = new JsonLoadSettings
        {
            // Two "stone" entries can't come from our writer, and silently keeping one of them would lose hits.
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            CommentHandling = CommentHandling.Ignore,
        };

        public static string Write(PlayerProfile profile)
        {
            var weapons = new JObject();
            foreach (var w in profile.Weapons)
                weapons[w.Key] = new JObject { ["directHits"] = w.Value.DirectHits };

            var robots = new JObject();
            foreach (var r in profile.Robots)
                robots[r.Key] = new JObject { ["ballKnocks"] = r.Value.BallKnocks, ["knockedByBall"] = r.Value.KnockedByBall };

            var root = new JObject
            {
                ["version"] = PlayerProfile.CurrentVersion,
                ["weapons"] = weapons,
                ["robots"] = robots,
            };
            return root.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Reads a save. Ok → profile is the save. Anything else → profile is null and problem says why (for the log).
        /// </summary>
        public static ProfileReadResult Read(string text, out PlayerProfile profile, out string problem)
        {
            profile = null;
            problem = null;
            if (string.IsNullOrWhiteSpace(text)) { problem = "the file is empty"; return ProfileReadResult.Corrupt; }

            JObject root;
            try { root = JObject.Parse(text, Strict); }
            catch (JsonException e) { problem = "not valid JSON: " + e.Message; return ProfileReadResult.Corrupt; }

            var versionToken = root["version"];
            if (versionToken == null) { problem = "no version field"; return ProfileReadResult.UnknownVersion; }
            if (!TryCount(versionToken, out int version)) { problem = "the version isn't a whole number"; return ProfileReadResult.Corrupt; }
            if (version != PlayerProfile.CurrentVersion)
            {
                // Older versions would be migrated here; there are none yet. Newer = a save from a newer build.
                problem = $"version {version}, this build reads version {PlayerProfile.CurrentVersion}";
                return ProfileReadResult.UnknownVersion;
            }

            var result = new PlayerProfile();
            if (!ReadSection(root, "weapons", out var weapons, ref problem)) return ProfileReadResult.Corrupt;
            if (weapons != null)
                foreach (var entry in weapons.Properties())
                {
                    if (!ReadEntry(entry, out var fields, ref problem)) return ProfileReadResult.Corrupt;
                    var w = result.Weapon(entry.Name);
                    if (!ReadField(fields, entry.Name, "directHits", ref w.DirectHits, ref problem)) return ProfileReadResult.Corrupt;
                }

            if (!ReadSection(root, "robots", out var robots, ref problem)) return ProfileReadResult.Corrupt;
            if (robots != null)
                foreach (var entry in robots.Properties())
                {
                    if (!ReadEntry(entry, out var fields, ref problem)) return ProfileReadResult.Corrupt;
                    var r = result.Robot(entry.Name);
                    if (!ReadField(fields, entry.Name, "ballKnocks", ref r.BallKnocks, ref problem)) return ProfileReadResult.Corrupt;
                    if (!ReadField(fields, entry.Name, "knockedByBall", ref r.KnockedByBall, ref problem)) return ProfileReadResult.Corrupt;
                }

            profile = result;
            return ProfileReadResult.Ok;
        }

        // A missing section is fine (nothing banked there yet); one that isn't an object is not.
        private static bool ReadSection(JObject root, string name, out JObject section, ref string problem)
        {
            section = null;
            var token = root[name];
            if (token == null || token.Type == JTokenType.Null) return true;
            section = token as JObject;
            if (section == null) problem = $"\"{name}\" isn't an object";
            return section != null;
        }

        private static bool ReadEntry(JProperty entry, out JObject fields, ref string problem)
        {
            fields = entry.Value as JObject;
            if (entry.Name.Length == 0) { problem = "an entry has an empty id"; return false; }
            if (fields == null) { problem = $"\"{entry.Name}\" isn't an object"; return false; }
            return true;
        }

        // Missing = 0. Present = a whole number from 0 to int.MaxValue, or the file is corrupt.
        private static bool ReadField(JObject fields, string id, string name, ref int value, ref string problem)
        {
            var token = fields[name];
            if (token == null) return true;
            if (TryCount(token, out value)) return true;
            problem = $"{id}.{name} isn't a whole number from 0 up ({token.ToString(Formatting.None)})";
            return false;
        }

        private static bool TryCount(JToken token, out int value)
        {
            value = 0;
            // An Integer token holds a long, or a BigInteger when it doesn't fit one; 3.0 or "3" aren't Integer tokens.
            if (token.Type != JTokenType.Integer || !(((JValue)token).Value is long n)) return false;
            if (n < 0 || n > int.MaxValue) return false;
            value = (int)n;
            return true;
        }
    }
}
