using System.Collections.Generic;
using Piglings.Definitions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Piglings.Simulation
{
    /// <summary>
    /// The title screen (M11.T8), its own scene, first in Build Settings — a scene of its own because the game scene reloads
    /// for every night, and the title must show once per launch, not before every night.
    ///   No progress in the save → Play: the tip cards, one per click (Next), then the game.
    ///   Progress → Continue (straight into the game) and Reset save (asks first; the old save is copied aside, never lost).
    /// It reads the same save the night does (Campaign ▸ Save Name, through ProfileStore), so in a browser it sees the copy
    /// that survives a reload and a new upload. The clicks are wired here in code — the buttons need no On Click entries.
    /// Every button / panel is optional. Simulation, because Reset changes the save (views only read).
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [Tooltip("Its Save Name is the save shown and reset here — the same asset NightSession uses.")]
        [SerializeField] private CampaignDefinition campaign;
        [Tooltip("The game scene's name (TestNight). It must be in File ▸ Build Profiles (Scene List), after this one.")]
        [SerializeField] private string gameScene = "TestNight";

        [Header("Buttons")]
        [Tooltip("Shown when the save has no progress: the tips, then the game.")]
        [SerializeField] private Button play;
        [Tooltip("Shown when the save has progress: straight into the game.")]
        [SerializeField] private Button continueGame;
        [Tooltip("Shown when the save has progress: asks first (Confirm Panel), then starts over.")]
        [SerializeField] private Button resetSave;

        [Header("Reset confirmation")]
        [Tooltip("Shown by Reset save: 'Start over? Your progress is lost.' with the two buttons below. Empty = reset at once.")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Button confirmReset;
        [SerializeField] private Button cancelReset;

        [Header("Tips (Play)")]
        [Tooltip("The tip cards, in order (depth, peg placement): each a panel in this scene, hidden at start. Play shows them " +
                 "one at a time; Next on the last one starts the game. Empty = Play starts the game at once.")]
        [SerializeField] private List<GameObject> tipCards = new List<GameObject>();
        [Tooltip("One button over the cards: the next card, or the game after the last.")]
        [SerializeField] private Button nextTip;
        [Tooltip("Hidden while the tips show (the title's buttons and logo), so the cards stand alone. Optional.")]
        [SerializeField] private GameObject menu;

        private ProfileStore _store;
        private bool _hasProgress;
        private int _tip = -1;   // the card showing; -1 = none (the menu)

        private void Start()
        {
            if (campaign == null)
            {
                Debug.LogError("TitleScreen: assign the Campaign (its Save Name is the save to show).", this);
                enabled = false;
                return;
            }
            if (!CheckLayout())
            {
                enabled = false;
                return;
            }
            _store = new ProfileStore(campaign.SaveName);
            ReadSave();
            Wire(play, Play);
            Wire(continueGame, StartGame);
            Wire(resetSave, AskReset);
            Wire(confirmReset, ResetSave);
            Wire(cancelReset, CancelReset);
            Wire(nextTip, NextTip);
            ShowTip(-1);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            Refresh();
        }

        // The panels are switched on and off whole, so one that holds a thing it must not hide (the Canvas itself as the
        // Confirm Panel, a tip card around the buttons…) blanks the screen. Said once, clearly, instead.
        private bool CheckLayout()
        {
            var menuButtons = new Component[] { play, continueGame, resetSave };
            bool ok = true;
            ok &= NotInside(confirmPanel, "Confirm Panel", "Play / Continue / Reset save (only Yes and Cancel go inside it)", menuButtons);
            ok &= NotInside(confirmPanel, "Confirm Panel", "the Menu or a tip card", menu, tipCards.ToArray());
            ok &= NotInside(menu, "Menu", "Next or the tip cards (the Menu is hidden while they show)", nextTip, tipCards.ToArray());
            ok &= NotInside(menu, "Menu", "the Confirm Panel", confirmPanel);
            foreach (var card in tipCards)
            {
                ok &= NotInside(card, "a tip card", "Play / Continue / Reset save", menuButtons);
                ok &= NotInside(card, "a tip card", "the Menu, the Confirm Panel or another card", menu, confirmPanel);
                foreach (var other in tipCards)
                    if (other != card) ok &= NotInside(card, "a tip card", "another tip card", other);
            }
            return ok;
        }

        private bool NotInside(GameObject panel, string panelName, string what, params Object[] things)
        {
            if (panel == null) return true;
            foreach (var thing in things)
            {
                var t = thing is GameObject go ? go.transform : thing is Component c ? c.transform : null;
                if (t == null || panel.transform == t || !t.IsChildOf(panel.transform)) continue;
                Debug.LogError($"TitleScreen: {panelName} ({panel.name}) holds {thing.name} — it must not hold {what}. It is " +
                               "switched on and off whole, so the screen would go blank. Give it its own child object.", this);
                return false;
            }
            if (panel.GetComponent<Canvas>() != null && panel.transform.parent == null)
            {
                Debug.LogError($"TitleScreen: {panelName} is the Canvas itself — hiding it hides everything. Use a child panel.", this);
                return false;
            }
            return true;
        }

        private bool NotInside(GameObject panel, string panelName, string what, Object first, Object[] rest)
        {
            var all = new List<Object> { first };
            all.AddRange(rest);
            return NotInside(panel, panelName, what, all.ToArray());
        }

        // Load also writes the save when there's none yet (a first launch) — harmless, the night would do the same.
        private void ReadSave()
        {
            var load = _store.Load();
            _hasProgress = load.Profile.HasProgress;
            string line = $"Piglings title: save {load.Source} — {load.Profile.Describe()}  ({_store.Where})";
            if (load.Problems.Count == 0 && load.SaveError == null) Debug.Log(line, this);
            else Debug.LogWarning(line + "\n  problems: " + string.Join("; ", load.Problems) +
                                  (load.SaveError != null ? "\n  couldn't write the save: " + load.SaveError : ""), this);
        }

        private void Refresh()
        {
            bool asking = confirmPanel != null && confirmPanel.activeSelf;
            bool tips = _tip >= 0;
            if (menu != null) menu.SetActive(!tips);
            Show(play, !tips && !_hasProgress, !asking);
            Show(continueGame, !tips && _hasProgress, !asking);
            Show(resetSave, !tips && _hasProgress, !asking);
            Show(nextTip, tips, true);
        }

        private void Play()
        {
            if (tipCards.Count == 0) StartGame();
            else ShowTip(0);
            Refresh();
        }

        private void NextTip()
        {
            if (_tip + 1 >= tipCards.Count) StartGame();
            else ShowTip(_tip + 1);
            Refresh();
        }

        private void ShowTip(int index)
        {
            _tip = index;
            for (int i = 0; i < tipCards.Count; i++)
                if (tipCards[i] != null) tipCards[i].SetActive(i == index);
        }

        private void AskReset()
        {
            if (confirmPanel == null) ResetSave();
            else confirmPanel.SetActive(true);
            Refresh();
        }

        private void CancelReset()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            Refresh();
        }

        private void ResetSave()
        {
            var error = _store.Reset();
            if (error == null) Debug.Log($"Piglings title: save reset (the old one copied aside as piglings_{_store.File.ProfileName}.reset-*.json)", this);
            else Debug.LogError("Piglings title: couldn't reset the save — " + error, this);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            ReadSave();
            Refresh();
        }

        private void StartGame()
        {
            if (!Application.CanStreamedLevelBeLoaded(gameScene))
            {
                Debug.LogError($"TitleScreen: can't load '{gameScene}' — add it to File ▸ Build Profiles (Scene List), or fix Game Scene.", this);
                return;
            }
            SceneManager.LoadScene(gameScene);
        }

        private static void Show(Button button, bool visible, bool pressable)
        {
            if (button == null) return;
            if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
            if (visible) button.interactable = pressable;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }
    }
}
