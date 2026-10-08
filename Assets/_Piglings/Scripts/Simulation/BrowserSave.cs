#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Piglings.Simulation
{
    /// <summary>
    /// The browser's localStorage, for the save's second copy in a WebGL build (T8). Why not just the file: WebGL's
    /// persistentDataPath is IndexedDB under a hash of the page's URL, and itch serves every upload from a new URL — a new
    /// upload would reset every tester's save. localStorage is keyed by the site (itch's html host), not the upload, so a
    /// copy there survives it. The JavaScript side is Plugins/WebGL/PiglingsStorage.jslib.
    /// Outside a WebGL build (the editor, desktop) there's no browser: Available is false and every call does nothing.
    /// Stateless (no fields), so a static class is fine.
    /// </summary>
    public static class BrowserSave
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string PiglingsStorageGet(string key);
        [DllImport("__Internal")] private static extern int PiglingsStorageSet(string key, string value);
        [DllImport("__Internal")] private static extern void PiglingsStorageRemove(string key);

        public static bool Available => true;
        /// <summary>The stored text, or null when there's none (or the browser blocks storage).</summary>
        public static string Read(string key) => PiglingsStorageGet(key);
        /// <summary>False when the browser refused it (storage full, or blocked in private browsing).</summary>
        public static bool Write(string key, string value) => PiglingsStorageSet(key, value) == 1;
        public static void Delete(string key) => PiglingsStorageRemove(key);
#else
        public static bool Available => false;
        public static string Read(string key) => null;
        public static bool Write(string key, string value) => false;
        public static void Delete(string key) { }
#endif
    }
}
