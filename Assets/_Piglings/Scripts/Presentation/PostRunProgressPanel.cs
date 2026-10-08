using System.Collections.Generic;
using Piglings.Meta;
using Piglings.Simulation;
using TMPro;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The post-run's PROGRESS panel (M10.E; mockup Docs/Mockups/post_run_v5.png): only what moved tonight. Every bar shows before tonight (dim) and tonight's
    /// gain (bright, filling in as the panel appears); a bar tonight completed shows READY. Upgrades aren't played here —
    /// the barn room does that (the footer, laid out by Yam, says so).
    ///  - The stone (always): its icon at its level, "+52 hits", the bar to the next +1 stone, "14 → 15 stones", the
    ///    evolution it was heading for tonight ("next: 15 · evolve"), and the evolution track (one slot per evolution: the
    ///    stones it unlocks at, stone sprite and the line into it; reached ones full, the rest faded).
    ///  - A row per peg type that fired tonight or was unlocked tonight: name + sprite, copies "5 → 6", the bar to the next
    ///    copy, the next follow-up step ("6 = 3 in a row"); unlocked tonight: the NEW badge + "unlocked by dawn on night n",
    ///    no copies, no bar. Unused types have no row.
    ///  - Wolves dropped (always): "+141" tonight — coloured by the record rule (tonight ÷ your most-wolves night, Visuals ▸
    ///    Record Bands) — and "1,204 all time".
    /// The rows come from NightSession.BuildPostRunReport (Meta's PostRunProgress). Every field is optional. Filled once,
    /// by PostRunView. Reads only.
    /// </summary>
    public sealed class PostRunProgressPanel : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        [Header("Stones")]
        [Tooltip("The stone's icon: given the sprite of the level it has now. Optional.")]
        [SerializeField] private UnityEngine.UI.Image stoneIcon;
        [Tooltip("\"+52 hits\".")]
        [SerializeField] private TMP_Text stoneHits;
        [Tooltip("The bar to the next +1 stone.")]
        [SerializeField] private ProgressBarView stoneBar;
        [Tooltip("\"14 → 15 stones\" (\"15 stones\" when none was earned).")]
        [SerializeField] private TMP_Text stoneCount;
        [Tooltip("The evolution the stone was heading for tonight: \"next: 15 · evolve\" / \"fully evolved\".")]
        [SerializeField] private TMP_Text nextEvolution;
        [Tooltip("One evolution slot (marker = an Image for the level's stone sprite, label = its Stones Needed, bar = the line " +
                 "leading into it — full once reached, hidden on the first), cloned per level of the stone under a Horizontal " +
                 "Layout Group.")]
        [SerializeField] private TemplateSlot evolutionSlot;
        [Tooltip("A bar along the evolution track (a ProgressBarView stretched from the first slot to the last): dim up to the stones " +
                 "before tonight, bright up to now; its READY tag when tonight reached a new evolution. Optional.")]
        [SerializeField] private ProgressBarView evolutionBar;
        [Tooltip("An evolution not reached yet: its sprite at this alpha.")]
        [SerializeField, Range(0f, 1f)] private float notReachedAlpha = 0.3f;

        [Header("Pegs (only what moved)")]
        [Tooltip("One peg row, cloned per type that moved under a Vertical Layout Group: marker = an Image for the peg's sprite, " +
                 "label = its name, detail = the copies \"5 → 6\", note = the next step \"6 = 3 in a row\" or \"unlocked by dawn " +
                 "on night n\", bar = to the next copy (hidden on a NEW row), badge = NEW.")]
        [SerializeField] private TemplateSlot pegRow;

        [Header("Wolves dropped")]
        [Tooltip("\"+141\".")]
        [SerializeField] private TMP_Text wolvesTonight;
        [Tooltip("\"1,204 all time\".")]
        [SerializeField] private TMP_Text wolvesTotal;

        [Header("Timing")]
        [Tooltip("Seconds between one bar filling in and the next (stone first, then each peg).")]
        [SerializeField, Min(0f)] private float rowDelay = 0.15f;

        private List<TemplateSlot> _evolutions = new List<TemplateSlot>();
        private ScoreStyles _styles;
        private PopupColor _wolvesStyle;   // "+N" against your most-wolves night: repainted every frame (pulse / rainbow)
        private List<TemplateSlot> _pegs = new List<TemplateSlot>();

        public void Show(PostRunReport report, float delay)
        {
            if (report == null) return;
            ShowStone(report.Stone, delay);

            if (_pegs.Count == 0) _pegs = TemplateList.Build(pegRow, null, report.Pegs.Count);
            for (int i = 0; i < _pegs.Count && i < report.Pegs.Count; i++) ShowPeg(_pegs[i], report.Pegs[i], delay + (i + 1) * rowDelay);

            var texts = session.Texts;
            if (wolvesTonight != null)
            {
                wolvesTonight.text = UiText.Fill(texts.wolvesTonight, ("count", Numbers.Thousands(report.WolvesTonight)));
                // Coloured by the record rule: tonight ÷ your most-wolves night before tonight (≥ 1 = a new record).
                if (_styles == null) _styles = new ScoreStyles(session.Visuals);
                _wolvesStyle = report.WolvesTonight > 0 ? _styles.ForRecord(report.WolvesVsRecord) : null;
            }
            if (wolvesTotal != null) wolvesTotal.text = UiText.Fill(texts.wolvesTotal, ("count", Numbers.Thousands(report.WolvesTotal)));
        }

        private void LateUpdate()
        {
            if (_wolvesStyle != null) TextColouring.Apply(wolvesTonight, _wolvesStyle, 0f, 0f, 1f);
        }

        private void ShowStone(StoneProgressRow stone, float delay)
        {
            if (stone == null) return;
            var weapon = session.Weapon;
            var texts = session.Texts;
            if (stoneIcon != null && weapon.SpriteFor(stone.After.Level) != null) stoneIcon.sprite = weapon.SpriteFor(stone.After.Level);
            if (stoneHits != null) stoneHits.text = UiText.Fill(texts.stoneHits, ("hits", Numbers.Thousands(stone.HitsGained)));
            if (stoneBar != null) stoneBar.Show(stone.Bar.Before, stone.Bar.After, stone.Bar.Ready, delay);
            // The bar along the track: the Evolution Bar field, or (as laid out in TestNight) the evolution slot's Bar when
            // it isn't inside the slot — one bar shared by every clone, so it must be driven once, here, not per slot.
            var trackBar = TrackBar();
            if (trackBar != null) trackBar.Show(stone.EvolutionBar.Before, stone.EvolutionBar.After, stone.EvolvedTonight, delay);
            if (stoneCount != null)
                stoneCount.text = stone.After.Stones > stone.Before.Stones
                    ? UiText.Fill(texts.stonesGrew, ("before", stone.Before.Stones.ToString()), ("after", stone.After.Stones.ToString()))
                    : UiText.Fill(texts.stoneCount, ("count", stone.After.Stones.ToString()));
            // The evolution it was heading for when the night began (post_run_v5: 14 → 15 stones, "next: 15 · evolve" — the
            // stone earned tonight reaches it; the barn plays it).
            if (nextEvolution != null)
                nextEvolution.text = stone.Before.NextEvolutionStones < 0 ? texts.fullyEvolved
                    : UiText.Fill(texts.nextEvolution, ("stones", stone.Before.NextEvolutionStones.ToString()));

            // The track: one slot per evolution of the night's stone (its Evolutions list), reached = the evolution the stones give now.
            var levels = weapon.Evolutions;
            if (_evolutions.Count == 0) _evolutions = TemplateList.Build(evolutionSlot, null, levels.Count);
            for (int i = 0; i < _evolutions.Count && i < levels.Count; i++)
            {
                int level = i + 1;
                var slot = _evolutions[i];
                slot.SetSprite(weapon.SpriteFor(level));
                // Players read stones, not levels: the stones this evolution unlocks at.
                slot.SetLabel(levels[i] != null ? UiText.Fill(texts.evolutionPoint, ("stones", session.StoneRule.StonesForEvolution(level).ToString())) : "");
                bool reached = level <= stone.After.Level;
                slot.SetAlpha(reached ? 1f : notReachedAlpha);
                // The line into this slot (only a bar INSIDE the slot): full once reached; the first slot has none.
                if (slot.Bar != null && slot.Bar != trackBar && slot.Bar.transform.IsChildOf(slot.transform))
                {
                    slot.Bar.gameObject.SetActive(i > 0);
                    if (i > 0) slot.Bar.Show(0f, reached ? 1f : 0f, false);
                }
            }
        }

        private ProgressBarView TrackBar()
        {
            if (evolutionBar != null) return evolutionBar;
            if (evolutionSlot != null && evolutionSlot.Bar != null && !evolutionSlot.Bar.transform.IsChildOf(evolutionSlot.transform))
                return evolutionSlot.Bar;
            return null;
        }

        private void ShowPeg(TemplateSlot slot, PegProgressRow row, float delay)
        {
            var peg = session.Campaign != null ? session.Campaign.FindPeg(row.PegId) : null;
            slot.SetLabel(peg != null ? peg.DisplayName : row.PegId);
            if (peg != null) slot.SetSprite(peg.Sprite);
            // A type unlocked tonight shows only its NEW tag and where it came from (post_run_v5): no copies, no bar yet.
            var texts = session.Texts;
            slot.SetDetail(row.UnlockedTonight ? ""
                : row.CopiesAfter > row.CopiesBefore
                    ? UiText.Fill(texts.copiesGrew, ("before", row.CopiesBefore.ToString()), ("after", row.CopiesAfter.ToString()))
                    : UiText.Fill(texts.copies, ("count", row.CopiesAfter.ToString())));
            slot.ShowBadge(row.UnlockedTonight);
            // The next follow-up step in the mockup's shape ("6 = …"): at that many copies, a throw of it gives this many in a row.
            slot.SetNote(row.UnlockedTonight ? UiText.Fill(texts.pegUnlocked, ("night", row.UnlockedByNight.ToString()))
                : row.InARowAtNext > 0 ? UiText.Fill(texts.pegNextFollowUp, ("copies", row.After.NextFollowUpAt.ToString()), ("count", row.InARowAtNext.ToString()))
                : row.After.AtMax ? texts.pegAtMax : "");
            if (slot.Bar == null) return;
            slot.Bar.gameObject.SetActive(!row.UnlockedTonight);
            if (!row.UnlockedTonight) slot.Bar.Show(row.Bar.Before, row.Bar.After, row.Bar.Ready, delay);
        }
    }
}
