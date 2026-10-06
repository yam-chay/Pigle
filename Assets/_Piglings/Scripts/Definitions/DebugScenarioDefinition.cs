using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>One peg type's copies in a debug scenario.</summary>
    [System.Serializable]
    public sealed class ScenarioPeg
    {
        public PegDefinition peg;
        [Tooltip("Copies owned (written as the fewest triggers that give them). Only shows if the type is unlocked — a starting " +
                 "type, or its night won (Nights Won).")]
        [Min(1)] public int copies = 1;
    }

    /// <summary>
    /// A debug scenario (M11.T2): a whole player state + the night to play, loaded in one click from the F1 panel — "night 3
    /// with 18 stones and 4 Bouncy" without playing there. Loading REPLACES the save (records and every count start from
    /// empty), then saves and reloads. Developer only; nothing in the game reads it.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Debug Scenario", fileName = "Scenario_")]
    public sealed class DebugScenarioDefinition : ScriptableObject
    {
        [Tooltip("What this scenario is for (shown in the panel as the button's tooltip).")]
        [TextArea(1, 3)] [SerializeField] private string notes;
        [Tooltip("The campaign night to play (1 = the first).")]
        [SerializeField, Min(1)] private int night = 1;
        [Tooltip("The campaign's first N nights won (one dawn each): opens the nights after them and their peg unlocks.")]
        [SerializeField, Min(0)] private int nightsWon;
        [Tooltip("Stones on the pile at the start of a night (the level and refill follow from the stone's Levels).")]
        [SerializeField, Min(0)] private int stones = 10;
        [Tooltip("Copies per peg type. A type not listed keeps its starting copies (1).")]
        [SerializeField] private List<ScenarioPeg> pegs = new List<ScenarioPeg>();
        [Tooltip("Load straight into the night (no barn room).")]
        [SerializeField] private bool startInNight;

        public string Notes => notes;
        public int Night => night;
        public int NightsWon => nightsWon;
        public int Stones => stones;
        public IReadOnlyList<ScenarioPeg> Pegs => pegs;
        public bool StartInNight => startInNight;
    }
}
