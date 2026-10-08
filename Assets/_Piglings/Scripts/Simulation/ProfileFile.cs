using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Piglings.Meta;

namespace Piglings.Simulation
{
    public enum ProfileSource
    {
        New,        // no save yet: a fresh profile (first launch)
        Main,       // the save loaded normally
        Previous,   // the save was bad; the one before it (.prev) loaded instead — at most one night lost
        Fresh,      // the save was bad and there was no good .prev: a fresh profile (the bad files are backed up)
    }

    /// <summary>What Load found, for NightSession's log line. Problems / BackedUp are empty on a normal load.</summary>
    public sealed class ProfileLoad
    {
        public PlayerProfile Profile;
        public ProfileSource Source;
        public readonly List<string> Problems = new List<string>();
        public readonly List<string> BackedUp = new List<string>();   // paths of the bad files' copies
        public string SaveError;                                      // set if the repaired/new file couldn't be written
    }

    /// <summary>
    /// The save on disk: one JSON file (Meta's ProfileJson) in a folder NightSession gives it
    /// (Application.persistentDataPath). Engine-free on purpose — like ThrowSolver — so CoreCheck runs it on a real
    /// temp folder.
    ///
    ///   piglings_<name>.json        the save            (name = the profile: "campaign" in TestNight)
    ///   piglings_<name>.prev.json   the save before the last one (the fallback)
    ///   piglings_<name>.json.tmp    a save being written; ignored on load
    ///   piglings_<name>.corrupt-*   copies of bad files, kept for us to look at
    ///   piglings_<name>.reset-*     the save as it was when the player reset it (the title screen's Reset save)
    /// Separate profiles are separate files: a test profile never advances the real campaign.
    ///
    /// Save never leaves a half-written file: it writes the .tmp, flushes it to disk, then swaps it in with File.Replace
    /// (one rename). Before the swap the current save is copied to .prev.
    /// Load: the save → if it's bad, copy it aside and try .prev → if that's bad too, copy it aside and start fresh.
    /// Whatever it ends up with is written back right away, so the next launch doesn't trip on the same bad file.
    /// It never throws: a disk problem is reported and the game plays on (it just won't keep tonight's progress).
    /// If a bad file can't be copied aside, saving stays off for this session, so it's never overwritten without a copy.
    /// </summary>
    public sealed class ProfileFile
    {
        /// <summary>The profile name used when none is given — and the file the first saves were written to.</summary>
        public const string DefaultProfile = "profile";

        public string MainPath { get; }
        public string PrevPath { get; }
        public string TempPath { get; }
        private readonly string _directory;

        // Set when a bad file couldn't be backed up: writing would destroy the only copy of it.
        private string _blocked;

        // UTF-8 without a byte-order mark: plain JSON for any tool that opens it.
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public string ProfileName { get; }

        public ProfileFile(string directory, string profileName = DefaultProfile)
        {
            _directory = directory;
            ProfileName = SafeName(profileName);
            MainPath = Path.Combine(directory, $"piglings_{ProfileName}.json");
            PrevPath = Path.Combine(directory, $"piglings_{ProfileName}.prev.json");
            TempPath = MainPath + ".tmp";
        }

        public ProfileLoad Load()
        {
            var load = new ProfileLoad();
            if (!File.Exists(MainPath))
            {
                // No save. A .prev without a save can only be a leftover; it'll be replaced by the next save.
                load.Profile = new PlayerProfile();
                load.Source = ProfileSource.New;
                load.SaveError = Write(load.Profile, keepPrevious: false);
                return load;
            }

            if (TryRead(MainPath, load, out var main))
            {
                load.Profile = main;
                load.Source = ProfileSource.Main;
                return load;
            }

            // The save is bad (it was copied aside in TryRead). The last good one may still be in .prev.
            if (File.Exists(PrevPath) && TryRead(PrevPath, load, out var prev))
            {
                load.Profile = prev;
                load.Source = ProfileSource.Previous;
            }
            else
            {
                load.Profile = new PlayerProfile();
                load.Source = ProfileSource.Fresh;
                // A bad .prev is copied aside by now; left in place it would be "recovered" from (and copied) again
                // on every later bad load. Kept if the copy failed — then nothing may touch it.
                if (_blocked == null) TryDelete(PrevPath);
            }
            // Replace the bad save now. keepPrevious false: .prev must not become the bad file (it's already copied aside).
            load.SaveError = _blocked ?? Write(load.Profile, keepPrevious: false);
            return load;
        }

        /// <summary>Writes the profile. Null on success, else what went wrong (the old save is still intact).</summary>
        public string Save(PlayerProfile profile) => _blocked ?? Write(profile, keepPrevious: true);

        /// <summary>
        /// Another copy of the save wins over the file (T8: the browser's copy, which survives a new itch upload when the
        /// file doesn't). A readable copy that differs from the file is written as the save (the file becomes .prev); an
        /// unreadable one is ignored, so a bad copy never replaces a good file. Call before Load. Null = nothing to report.
        /// </summary>
        public string Adopt(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            if (ProfileJson.Read(json, out var profile, out var problem) != ProfileReadResult.Ok)
                return "the other copy is unreadable (" + problem + "); the file is used";
            try
            {
                if (File.Exists(MainPath) && File.ReadAllText(MainPath, Utf8) == ProfileJson.Write(profile)) return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            return _blocked ?? Write(profile, keepPrevious: File.Exists(MainPath));
        }

        /// <summary>
        /// Starts over (the title screen's Reset save): the save is copied aside as .reset-*, then the save and .prev are
        /// removed — the next Load is a first launch. Null on success; on an error nothing is removed.
        /// </summary>
        public string Reset()
        {
            if (_blocked != null) return _blocked;
            if (File.Exists(MainPath) && BackUp(MainPath, "reset") == null)
                return "couldn't copy the save aside, so it's left untouched";
            try
            {
                if (File.Exists(MainPath)) File.Delete(MainPath);
                if (File.Exists(PrevPath)) File.Delete(PrevPath);
                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        private string Write(PlayerProfile profile, bool keepPrevious)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                var bytes = Utf8.GetBytes(ProfileJson.Write(profile));
                using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(flushToDisk: true);   // on disk before the swap, or a power cut could leave an empty save
                }

                if (File.Exists(MainPath))
                {
                    if (keepPrevious) File.Copy(MainPath, PrevPath, overwrite: true);
                    File.Replace(TempPath, MainPath, null);
                }
                else File.Move(TempPath, MainPath);
                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        // Reads and parses one file. A bad one is copied aside (never deleted, never silently overwritten) and its
        // problem recorded.
        private bool TryRead(string path, ProfileLoad load, out PlayerProfile profile)
        {
            profile = null;
            string problem;
            try
            {
                if (ProfileJson.Read(File.ReadAllText(path, Utf8), out profile, out problem) == ProfileReadResult.Ok) return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                problem = "couldn't read it: " + e.Message;
            }

            load.Problems.Add(Path.GetFileName(path) + ": " + problem);
            var backup = BackUp(path, "corrupt");
            if (backup != null) load.BackedUp.Add(backup);
            else
            {
                _blocked = "saving is off: " + Path.GetFileName(path) + " is bad and couldn't be backed up, so it's left untouched";
                load.Problems.Add(_blocked);
            }
            return false;
        }

        // A profile name becomes part of a file name: letters, digits, - and _ only; anything else (or nothing) → the default.
        private static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return DefaultProfile;
            foreach (char c in name)
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return DefaultProfile;
            return name;
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
        }

        // why = "corrupt" or "reset": the copy's name says why it was made.
        private string BackUp(string path, string why)
        {
            try
            {
                // piglings_<name>.corrupt-20261003-142501.json (or .prev.json), -2, -3… if that second is taken.
                string kind = path == PrevPath ? ".prev" : "";
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                string name = $"piglings_{ProfileName}.{why}-{stamp}{kind}.json";
                for (int n = 2; File.Exists(Path.Combine(_directory, name)); n++)
                    name = $"piglings_{ProfileName}.{why}-{stamp}-{n}{kind}.json";
                var target = Path.Combine(_directory, name);
                File.Copy(path, target);
                return target;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
