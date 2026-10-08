using Piglings.Meta;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Where a profile is kept (T8): the file (ProfileFile, in persistentDataPath) and, in a WebGL build, a copy in the
    /// browser's localStorage (BrowserSave) — the copy that survives a page reload AND a new itch upload. Used by NightSession
    /// and the title screen, so both read and write the same save the same way.
    ///   Load:  the browser's copy, when there is one, is adopted as the file first (it's never older: every save writes
    ///          both) → the file's own load, with its .prev / backup safety → the result is copied back to the browser.
    ///   Save:  the file, then the browser. In a browser the copy is the one that counts (the file may not outlive the page).
    ///   Reset: the file's Reset (the old save copied aside), and the browser's copy removed.
    /// Outside WebGL it is exactly the file.
    /// </summary>
    public sealed class ProfileStore
    {
        public ProfileFile File { get; }
        private readonly string _key;

        public ProfileStore(string profileName)
        {
            File = new ProfileFile(Application.persistentDataPath, profileName);
            // localStorage is shared by every game on itch's html host: the game's name keeps the key ours.
            _key = "piglings_save_" + File.ProfileName;
        }

        /// <summary>Where the save is, for log lines: the file, plus the browser key in a WebGL build.</summary>
        public string Where => BrowserSave.Available ? $"{File.MainPath} + localStorage[{_key}]" : File.MainPath;

        public ProfileLoad Load()
        {
            string adoptProblem = BrowserSave.Available ? File.Adopt(BrowserSave.Read(_key)) : null;
            var load = File.Load();
            if (adoptProblem != null) load.Problems.Add("browser copy: " + adoptProblem);
            if (BrowserSave.Available && !BrowserSave.Write(_key, ProfileJson.Write(load.Profile)))
                load.Problems.Add("browser copy: the browser refused it (private browsing or full storage?) — progress lasts until the page closes");
            return load;
        }

        /// <summary>Null on success; else what went wrong (the older save is still intact wherever a write failed).</summary>
        public string Save(PlayerProfile profile)
        {
            string fileError = File.Save(profile);
            if (!BrowserSave.Available || BrowserSave.Write(_key, ProfileJson.Write(profile))) return fileError;
            const string refused = "the browser refused its copy (private browsing or full storage?)";
            return fileError != null ? fileError + "; and " + refused : refused;
        }

        /// <summary>Starts over: the next Load is a first launch. Null on success.</summary>
        public string Reset()
        {
            string error = File.Reset();
            // Only once the file is gone: otherwise a failed reset would leave the file as the one copy, adopted by nothing.
            if (error == null) BrowserSave.Delete(_key);
            return error;
        }
    }
}
