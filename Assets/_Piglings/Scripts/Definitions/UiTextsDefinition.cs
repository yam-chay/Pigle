using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// Every text the code writes into the UI (the night's scoreboard, the score popups, the post-run, the night sign), as
    /// templates: words stay as written, {names} are filled in by the code — "HOUR {hour} · ×{mult}" shows "HOUR 3 · ×2".
    /// Leave a {name} out and that value doesn't show; a {name} the code doesn't fill stays as typed (so a typo is visible).
    /// Each field's tooltip lists the {names} it gets.
    /// TextMeshPro rich text works inside them: &lt;b&gt;, &lt;size=80%&gt;, &lt;color=#ffcc00&gt;… (on texts painted by
    /// quality — scoreboard rows, popups — the quality colour paints over &lt;color&gt;).
    /// Numbers arrive already formatted (thousands separators, mults like 2.5). One asset on NightSession ▸ Ui Texts, read
    /// by every view; unassigned = these defaults (the texts as they were).
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/UI Texts", fileName = "UiTexts")]
    public sealed class UiTextsDefinition : ScriptableObject
    {
        [Header("Shared")]
        [Tooltip("Shown where there's nothing yet (no best throw, no record).")]
        public string nothing = "—";
        [Tooltip("{count} = 1.")]
        public string oneWolf = "1 wolf";
        [Tooltip("{count}")]
        public string manyWolves = "{count} wolves";

        [Header("Scoreboard (the night's chalkboard)")]
        [Tooltip("{hour} {mult} (the hour's multiplier)")]
        public string hourTitle = "HOUR {hour} · ×{mult}";
        public string dawnTitle = "DAWN";
        [Tooltip("{hour} {from} {to} (the hour's gap, in score)")]
        public string hourGap = "hour {hour} gap: {from} -> {to}";
        [Tooltip("{score} {target} (the next threshold)")]
        public string scoreLine = "{score} / {target}";
        [Tooltip("{score} — at dawn there's no next threshold.")]
        public string scoreAtDawn = "{score}";
        [Tooltip("A LAST THROWS row while its chain is in play, and after: {score} {mult} {hour} {hourMult}")]
        public string throwRow = "{score} × {mult} × H{hour}";
        [Tooltip("The row's result when its chain closes: {total}")]
        public string throwResult = "+{total}";
        [Tooltip("BEST TONIGHT's breakdown: {score} {mult} {hour} {hourMult}")]
        public string bestRow = "{score} × {mult} × H{hour} =";
        [Tooltip("BEST TONIGHT's result: {total}")]
        public string bestResult = "+{total}";

        [Header("Score popups (on the board)")]
        [Tooltip("At a robot knocked loose — the chain so far: {score} {mult}")]
        public string popupRobot = "{score} ×{mult}";
        [Tooltip("At a special peg: {mult} (its bonus)")]
        public string popupPegMult = "+{mult} mult";
        [Tooltip("At a special peg that triggers: {score} (its Score Value at its level)")]
        public string popupPegScore = "+{score}";
        [Tooltip("At a plain hold: {score}")]
        public string popupPlainHold = "+{score}";
        [Tooltip("The chain's close, above the pig: {score} {mult} {hour} {hourMult} {total}")]
        public string popupChain = "{score} ×{mult} ×H{hour} = {total}";

        [Header("Night sign (barn room)")]
        [Tooltip("{night} {hours}")]
        public string nightSign = "NIGHT {night} · {hours} HOURS";

        [Header("Post-run ▸ the night")]
        [Tooltip("{night}")]
        public string nightTitle = "NIGHT {night}";
        public string resultDawn = "DAWN";
        public string resultOutOfStones = "OUT OF STONES";
        [Tooltip("Each hour dot's label: {hour}")]
        public string hourDot = "{hour}";
        [Tooltip("{reached} {hours}")]
        public string hoursReached = "{reached} / {hours}";
        [Tooltip("{score} (the night's real total, a loss included) {banked} (what the bank keeps: on a loss, the last threshold reached)")]
        public string totalScore = "Total Score: {score}";
        [Tooltip("BEST THROW EACH HOUR's row label: {hour}")]
        public string hourRow = "H{hour}";
        [Tooltip("The biggest chain with depth: {wolves} (\"3 wolves\", from the wolf texts above) {depth}")]
        public string biggestChain = "{wolves} · depth {depth}";
        [Tooltip("{thrown} {stolen}")]
        public string stones = "{thrown} · {stolen}";

        [Header("Post-run ▸ records")]
        [Tooltip("The deepest chain: {depth}")]
        public string recordDepth = "depth {depth}";

        [Header("Post-run ▸ progress")]
        [Tooltip("{count}")]
        public string wolvesTonight = "+{count}";
        [Tooltip("{count}")]
        public string wolvesTotal = "{count} all time";
        [Tooltip("{hits}")]
        public string stoneHits = "+{hits} hits";
        [Tooltip("The pile grew tonight: {before} {after}")]
        public string stonesGrew = "{before} → {after} stones";
        [Tooltip("{count}")]
        public string stoneCount = "{count} stones";
        public string fullyEvolved = "fully evolved";
        [Tooltip("{stones} (the stone count of the next evolution)")]
        public string nextEvolution = "next: {stones} · evolve";
        [Tooltip("Each evolution point's label on the track: {stones}")]
        public string evolutionPoint = "{stones}";
        [Tooltip("A peg's copies grew tonight: {before} {after}")]
        public string copiesGrew = "{before} → {after}";
        [Tooltip("{count}")]
        public string copies = "{count}";
        [Tooltip("{night}")]
        public string pegUnlocked = "unlocked by dawn on night {night}";
        [Tooltip("{copies} (the next step) {count} (follow-ups in a row there)")]
        public string pegNextFollowUp = "{copies} = {count} in a row";
        public string pegAtMax = "max";
    }
}
