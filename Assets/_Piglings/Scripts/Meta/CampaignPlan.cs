using System.Collections.Generic;

namespace Piglings.Meta
{
    /// <summary>
    /// The campaign as plain values (Meta can't read the CampaignDefinition asset; NightSession builds this from it):
    /// the nights in order (by NightDefinition id), which peg type a dawn on each night unlocks, and the types owned from
    /// the start. Unlocks are DERIVED from the profile's dawns, never stored — so the unlock table can change freely.
    /// </summary>
    public sealed class CampaignPlan
    {
        private readonly string[] _nights;
        private readonly string[] _unlocks;        // per night: the peg type a dawn on it unlocks (null = none)
        private readonly List<string> _starting;

        public int NightCount => _nights.Length;
        public IReadOnlyList<string> StartingPegs => _starting;

        public CampaignPlan(IReadOnlyList<string> nightIds, IReadOnlyList<string> unlocksOnDawn = null, IReadOnlyList<string> startingPegs = null)
        {
            _nights = new string[nightIds?.Count ?? 0];
            _unlocks = new string[_nights.Length];
            for (int i = 0; i < _nights.Length; i++)
            {
                _nights[i] = nightIds[i];
                _unlocks[i] = unlocksOnDawn != null && i < unlocksOnDawn.Count && !string.IsNullOrEmpty(unlocksOnDawn[i]) ? unlocksOnDawn[i] : null;
            }
            _starting = new List<string>();
            if (startingPegs != null) foreach (var p in startingPegs) if (!string.IsNullOrEmpty(p) && !_starting.Contains(p)) _starting.Add(p);
        }

        public string NightId(int index) => index >= 0 && index < _nights.Length ? _nights[index] : null;

        /// <summary>The saved night index, kept inside the campaign (a save from a longer campaign lands on the last night).</summary>
        public int CurrentNight(PlayerProfile profile)
        {
            if (_nights.Length == 0) return 0;
            int i = profile.CurrentNight;
            return i < 0 ? 0 : i >= _nights.Length ? _nights.Length - 1 : i;
        }

        public bool HasDawn(PlayerProfile profile, int nightIndex) => profile.DawnsOn(NightId(nightIndex)) > 0;

        /// <summary>"Next night" is offered after a dawn on this night, when there is a next night.</summary>
        public bool CanGoNext(PlayerProfile profile, int nightIndex) => HasDawn(profile, nightIndex) && nightIndex + 1 < _nights.Length;

        public bool IsUnlocked(PlayerProfile profile, string pegId)
        {
            if (string.IsNullOrEmpty(pegId)) return false;
            if (_starting.Contains(pegId)) return true;
            for (int i = 0; i < _nights.Length; i++)
                if (_unlocks[i] == pegId && HasDawn(profile, i)) return true;
            return false;
        }

        /// <summary>Every peg type the player owns: the starting ones, then those unlocked, in campaign order.</summary>
        public List<string> UnlockedPegs(PlayerProfile profile)
        {
            var list = new List<string>(_starting);
            for (int i = 0; i < _nights.Length; i++)
                if (_unlocks[i] != null && !list.Contains(_unlocks[i]) && HasDawn(profile, i)) list.Add(_unlocks[i]);
            return list;
        }

        /// <summary>The next peg type still locked, and the night (1-based, for "dawn on night n") whose dawn unlocks it.</summary>
        public bool TryNextLocked(PlayerProfile profile, out string pegId, out int nightNumber)
        {
            for (int i = 0; i < _nights.Length; i++)
                if (_unlocks[i] != null && !IsUnlocked(profile, _unlocks[i]))
                {
                    pegId = _unlocks[i];
                    nightNumber = i + 1;
                    return true;
                }
            pegId = null;
            nightNumber = 0;
            return false;
        }

        /// <summary>
        /// The slices for a night's tower: the player's saved choice, or the night's own (bottom → top). A saved choice
        /// with a different number of slices (the night was re-authored since) is ignored: the tower's height is the night's.
        /// </summary>
        public static IReadOnlyList<string> TowerFor(PlayerProfile profile, string nightId, IReadOnlyList<string> authored)
        {
            int count = authored?.Count ?? 0;
            if (nightId != null && profile.Towers.TryGetValue(nightId, out var chosen) && chosen.Count == count && count > 0) return chosen;
            return authored;
        }
    }
}
