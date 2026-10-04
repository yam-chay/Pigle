using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// The campaign: the nights in order, which peg type a dawn on each night unlocks, the types owned from the start, and
    /// every slice the player may build with. Read by NightSession in the campaign scene only (Night.unity has none).
    /// The save stores the player's causes (dawns per night id, the current night); unlocks are derived from this table.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Campaign Definition", fileName = "Campaign")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        [Tooltip("In order: night 1 first. Each night's id is a save key.")]
        [SerializeField] private List<CampaignNight> nights = new List<CampaignNight>();
        [Tooltip("Peg types owned from the first night (Bouncy).")]
        [SerializeField] private List<PegDefinition> startingPegs = new List<PegDefinition>();
        [Tooltip("Every slice the player may put in a tower (all available for now; unlocks later).")]
        [SerializeField] private List<WallSliceDefinition> slices = new List<WallSliceDefinition>();

        public IReadOnlyList<CampaignNight> Nights => nights;
        public IReadOnlyList<PegDefinition> StartingPegs => startingPegs;
        public IReadOnlyList<WallSliceDefinition> Slices => slices;

        public NightDefinition NightAt(int index) =>
            nights.Count == 0 ? null : nights[Mathf.Clamp(index, 0, nights.Count - 1)].night;

        /// <summary>A slice by id: the campaign's list first, then any night's own slices. Null if none has it.</summary>
        public WallSliceDefinition FindSlice(string id)
        {
            foreach (var s in slices) if (s != null && s.Id == id) return s;
            foreach (var n in nights)
                if (n != null && n.night != null)
                    foreach (var s in n.night.Slices) if (s != null && s.Id == id) return s;
            return null;
        }

        /// <summary>A peg type the campaign can give (starting or unlocked), by id. Null if none.</summary>
        public PegDefinition FindPeg(string id)
        {
            foreach (var p in startingPegs) if (p != null && p.Id == id) return p;
            foreach (var n in nights) if (n != null && n.unlocksOnDawn != null && n.unlocksOnDawn.Id == id) return n.unlocksOnDawn;
            return null;
        }
    }

    /// <summary>One night of the campaign, and what a dawn on it unlocks.</summary>
    [System.Serializable]
    public sealed class CampaignNight
    {
        public NightDefinition night;
        [Tooltip("The peg type a dawn on this night unlocks (empty = none). It starts with 1 copy.")]
        public PegDefinition unlocksOnDawn;
    }
}
