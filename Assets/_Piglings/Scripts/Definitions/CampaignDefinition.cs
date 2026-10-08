using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Piglings.Definitions
{
    /// <summary>
    /// The campaign owns progression (R3): the nights in order, what a dawn on each night unlocks (a peg type, slices), and
    /// the fixed starting loadout the player begins with and upgrades over time (peg types, slices, the weapon, peg throws
    /// per round). One night's own content lives on its NightDefinition. Read by NightSession (TestNight).
    /// The save stores the player's causes (dawns per night id, the current night); unlocks are derived from this table.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Campaign Definition", fileName = "Campaign")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        [Tooltip("In order: night 1 first. Each night's id is a save key. A dawn on a night unlocks what its entry lists.")]
        [SerializeField] private List<CampaignNight> nights = new List<CampaignNight>();
        [Tooltip("The save this campaign plays on: piglings_<name>.json (and, in a browser build, its localStorage copy). The " +
                 "title screen and the night read it here, so both use the same save. A different name = a separate save, for " +
                 "testing without touching the real one. Letters, digits, - and _ only. Renaming it orphans players' saves.")]
        [SerializeField] private string saveName = "campaign";

        [Header("Starting loadout")]
        [Tooltip("Peg types owned from the first night (Bouncy).")]
        [SerializeField] private List<PegDefinition> startingPegs = new List<PegDefinition>();
        [Tooltip("Slices owned from the first night. The first one also fills any tower slot nothing else is in.")]
        [FormerlySerializedAs("slices")]
        [SerializeField] private List<WallSliceDefinition> startingSlices = new List<WallSliceDefinition>();
        [Tooltip("The weapon thrown (the stone). A weapon-select screen will choose from the owned weapons later.")]
        [SerializeField] private ThrowableDefinition weapon;
        [Tooltip("Peg throws every hour's placement round gives, before any type's follow-ups (those come from its copies). " +
                 "(Future: a skill-tree node adds +1.)")]
        [SerializeField, Min(0)] private int pegThrowsPerRound = 1;

        public IReadOnlyList<CampaignNight> Nights => nights;
        public string SaveName => saveName;
        public IReadOnlyList<PegDefinition> StartingPegs => startingPegs;
        public IReadOnlyList<WallSliceDefinition> StartingSlices => startingSlices;
        public ThrowableDefinition Weapon => weapon;
        public int PegThrowsPerRound => pegThrowsPerRound;

        public NightDefinition NightAt(int index) =>
            nights.Count == 0 ? null : nights[Mathf.Clamp(index, 0, nights.Count - 1)].night;

        /// <summary>A slice by id: the starting ones, then every night's unlocks. Null if the campaign doesn't have it.</summary>
        public WallSliceDefinition FindSlice(string id)
        {
            foreach (var s in startingSlices) if (s != null && s.Id == id) return s;
            foreach (var n in nights)
                if (n != null)
                    foreach (var s in n.slicesOnDawn) if (s != null && s.Id == id) return s;
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
        [Tooltip("The slices a dawn on this night unlocks (R3). Until then the slice picker shows them locked.")]
        public List<WallSliceDefinition> slicesOnDawn = new List<WallSliceDefinition>();
    }
}
