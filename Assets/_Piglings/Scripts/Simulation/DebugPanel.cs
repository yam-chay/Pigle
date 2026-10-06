using System.Collections.Generic;
using Piglings.Definitions;
using Piglings.Meta;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Piglings.Simulation
{
    /// <summary>
    /// The developer's debug panel (M11.T2): F1 opens it, in the editor and in Development builds only (a release build turns
    /// it off at start — players never see it). IMGUI, so it needs no Canvas and works in WebGL.
    ///  - SAVE: stones (and so the stone's level, reverting too), copies per peg type, nights won (unlocks follow), the night
    ///    to play. "Apply & reload" writes them as causes (ProfileEdits), saves and reloads — the night is rebuilt from the
    ///    save like any other load. Reset save starts over.
    ///  - BOARD EDIT: while on, a click on a socket cycles it: empty → each of tonight's peg types, level 1 → its max → empty.
    ///  - SCENARIOS: one button per Debug Scenario asset: replaces the save with its state and loads its night.
    /// While open the game is paused (Pause While Open) and the night's pointer input stands down, so clicks on the panel
    /// never throw a stone. Changes state through NightSession only.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("For board edit: the sockets' positions. Empty = no board edit.")]
        [SerializeField] private PegBoard board;
        [Tooltip("The camera the board is seen through (to find the clicked socket).")]
        [SerializeField] private Camera cam;
        [Tooltip("One-click states: Create ▸ Piglings ▸ Debug Scenario.")]
        [SerializeField] private List<DebugScenarioDefinition> scenarios = new List<DebugScenarioDefinition>();
        [Tooltip("Freeze the game (Time.timeScale 0) while the panel is open.")]
        [SerializeField] private bool pauseWhileOpen = true;
        [Tooltip("Board edit: how far from a socket a click still picks it (world units).")]
        [SerializeField, Min(0.05f)] private float socketPickRadius = 0.4f;
        [Tooltip("The panel's size on screen (1 = IMGUI's default).")]
        [SerializeField, Range(0.75f, 3f)] private float scale = 1.5f;

        // IMGUI needs an id per window; there's one panel. (GetInstanceID is obsolete in Unity 6.5 — an error there.)
        private const int WindowId = 0x5017;

        private bool _open;
        private float _timeScaleBefore = 1f;
        private bool _boardEdit;
        private Rect _window = new Rect(12f, 12f, 360f, 520f);
        private Vector2 _scroll;

        // The edits waiting for Apply, read from the save when the panel opens.
        private int _stones, _nightsWon, _night;
        private bool _startInNight;
        private readonly Dictionary<PegDefinition, int> _copies = new Dictionary<PegDefinition, int>();

        private void Awake()
        {
            // Development builds and the editor only: a release build never shows (or listens for) the panel.
            if (!Debug.isDebugBuild) enabled = false;
        }

        private void OnDisable() => SetOpen(false);

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) SetOpen(!_open);
            if (_open && _boardEdit) EditBoard();
        }

        private void SetOpen(bool open)
        {
            if (open == _open) return;
            _open = open;
            if (session != null) session.GameplayInputBlocked = open;
            if (open)
            {
                ReadSave();
                if (pauseWhileOpen) { _timeScaleBefore = Time.timeScale; Time.timeScale = 0f; }
            }
            else if (pauseWhileOpen) Time.timeScale = _timeScaleBefore;
        }

        private void ReadSave()
        {
            var profile = session.Profile;
            _stones = session.StoneRule != null ? session.StoneRule.For(profile.DirectHits(session.Night.Throwable.Id)).Stones : 0;
            _nightsWon = ProfileEdits.NightsWon(profile, session.Plan);
            _night = session.NightIndex + 1;
            _startInNight = false;
            _copies.Clear();
            foreach (var peg in session.CampaignPegTypes()) _copies[peg] = session.CopiesOwned(peg);
        }

        // ---------- board edit ----------

        private void EditBoard()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || board == null || cam == null) return;
            Vector2 screen = mouse.position.ReadValue();
            // Over the panel: that click is the panel's.
            var gui = new Vector2(screen.x, Screen.height - screen.y) / scale;
            if (_window.Contains(gui)) return;
            Vector2 world = cam.ScreenToWorldPoint(screen);
            int socket = NearestSocket(world);
            if (socket >= 0) CycleSocket(socket);
        }

        private int NearestSocket(Vector2 world)
        {
            int best = -1;
            float bestDistance = socketPickRadius;
            for (int i = 0; i < board.SocketCount; i++)
            {
                float d = Vector2.Distance(world, board.Position(i));
                if (d <= bestDistance) { best = i; bestDistance = d; }
            }
            return best;
        }

        // empty → type A lv1 … lv max → type B lv1 … → empty. A type the Rules refuse (no copies tonight) is skipped.
        private void CycleSocket(int socket)
        {
            var s = session.State.Sockets[socket];
            var types = session.PegTypesTonight;
            int typeIndex = -1;
            for (int i = 0; i < types.Count; i++) if (!s.IsEmpty && types[i].Id == s.PegId) typeIndex = i;

            if (typeIndex >= 0 && s.Level < types[typeIndex].MaxLevel && session.DebugSetSocket(socket, s.PegId, s.Level + 1)) return;
            for (int i = typeIndex + 1; i < types.Count; i++)
                if (session.DebugSetSocket(socket, types[i].Id, 1)) return;
            session.DebugSetSocket(socket, null, 0);
        }

        // ---------- the window ----------

        private void OnGUI()
        {
            if (!_open || session == null) return;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Debug (F1)");
        }

        private void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawSave();
            GUILayout.Space(8f);
            DrawBoard();
            GUILayout.Space(8f);
            DrawScenarios();
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawSave()
        {
            GUILayout.Label("<b>SAVE</b>  (applied on reload)", Rich());
            if (!session.IsCampaign)
            {
                GUILayout.Label("Not the campaign scene: this night uses its own pile and loadout.");
                return;
            }
            var rule = session.StoneRule;
            if (rule != null)
            {
                var status = rule.For(ProfileEdits.HitsForStones(rule, _stones));
                _stones = Stepper("Stones", _stones, rule.StartStones, rule.MaxStones);
                GUILayout.Label($"   → level {status.Level}, refill +{status.Refill}");
                GUILayout.BeginHorizontal();
                GUILayout.Label("   Evolution:", GUILayout.Width(90f));
                for (int level = 1; level <= rule.Evolutions.Count; level++)
                    if (GUILayout.Button($"Lv{level}")) _stones = ProfileEdits.StonesForLevel(rule, level);
                GUILayout.EndHorizontal();
            }
            var plan = session.Plan;
            int nights = plan != null ? plan.NightCount : 1;
            _nightsWon = Stepper("Nights won", _nightsWon, 0, nights);
            _night = Stepper("Night to play", _night, 1, nights);
            foreach (var peg in session.CampaignPegTypes())
            {
                var rulePeg = NightSession.ProgressionFor(peg);
                // Unlocked after Apply? A starting type, or its unlock night inside Nights Won.
                int unlockNight = plan != null ? plan.UnlockNightOf(peg.Id) : 0;
                bool unlocked = unlockNight == 0 || unlockNight <= _nightsWon;
                _copies.TryGetValue(peg, out int copies);
                _copies[peg] = Stepper($"{peg.DisplayName}{(unlocked ? "" : " (locked)")}", copies, rulePeg.StartCopies, rulePeg.MaxCopies);
            }
            _startInNight = GUILayout.Toggle(_startInNight, " Load straight into the night");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply & reload")) Apply();
            if (GUILayout.Button("Reset save")) { SetOpen(false); session.DebugResetSave(); }
            GUILayout.EndHorizontal();
        }

        private void Apply()
        {
            int stones = _stones, nightsWon = _nightsWon, night = _night;
            var copies = new Dictionary<PegDefinition, int>(_copies);
            var weapon = session.Night.Throwable.Id;
            var rule = session.StoneRule;
            var plan = session.Plan;
            bool startInNight = _startInNight;
            SetOpen(false);
            session.DebugEditSave(profile =>
            {
                if (rule != null) ProfileEdits.SetStones(profile, weapon, rule, stones);
                ProfileEdits.SetNightsWon(profile, plan, nightsWon);
                ProfileEdits.SetCurrentNight(profile, plan, night - 1);
                foreach (var entry in copies) ProfileEdits.SetPegCopies(profile, entry.Key.Id, NightSession.ProgressionFor(entry.Key), entry.Value);
            }, startInNight, $"panel — {stones} stones, {nightsWon} nights won, night {night}");
        }

        private void DrawBoard()
        {
            GUILayout.Label("<b>BOARD</b>  (tonight, not saved)", Rich());
            if (board == null || cam == null) { GUILayout.Label("Wire Board and Cam for board edit."); return; }
            if (session.State.Ended) { GUILayout.Label("The night is over."); return; }
            _boardEdit = GUILayout.Toggle(_boardEdit, " Board edit: click a socket to cycle its peg");
            if (GUILayout.Button("Empty every socket"))
                for (int i = 0; i < board.SocketCount; i++) session.DebugSetSocket(i, null, 0);
        }

        private void DrawScenarios()
        {
            GUILayout.Label("<b>SCENARIOS</b>  (replace the save)", Rich());
            if (scenarios.Count == 0) { GUILayout.Label("None: Create ▸ Piglings ▸ Debug Scenario, then add it here."); return; }
            foreach (var scenario in scenarios)
            {
                if (scenario == null) continue;
                if (GUILayout.Button(new GUIContent(scenario.name, scenario.Notes)))
                {
                    SetOpen(false);
                    session.DebugLoadScenario(scenario);
                    return;
                }
            }
        }

        private static int Stepper(string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150f));
            if (GUILayout.Button("−", GUILayout.Width(28f))) value--;
            GUILayout.Label(value.ToString(), GUILayout.Width(36f));
            if (GUILayout.Button("+", GUILayout.Width(28f))) value++;
            GUILayout.EndHorizontal();
            return Mathf.Clamp(value, min, max);
        }

        private GUIStyle _rich;   // built on first use: GUI.skin exists only inside OnGUI
        private GUIStyle Rich()
        {
            if (_rich == null) _rich = new GUIStyle(GUI.skin.label) { richText = true };
            return _rich;
        }
    }
}
