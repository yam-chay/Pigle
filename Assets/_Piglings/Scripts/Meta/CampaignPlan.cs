using System.Collections.Generic;

namespace Piglings.Meta
{
    /// <summary>
    /// The campaign as plain values (Meta can't read the CampaignDefinition asset; NightSession builds this from it):
    /// the nights in order (by NightDefinition id), what a dawn on each night unlocks (a peg type; slices — R3), and what's
    /// owned from the start (peg types, slices). Unlocks are DERIVED from the profile's dawns, never stored — so the unlock
    /// table can change freely.
    /// </summary>
    public sealed class CampaignPlan
    {
        private readonly string[] _nights;
        private readonly string[] _unlocks;        // per night: the peg type a dawn on it unlocks (null = none)
        private readonly List<string> _starting;
        private readonly List<string> _startingSlices = new List<string>();
        private readonly List<string>[] _sliceUnlocks;   // per night: the slices a dawn on it unlocks

        public int NightCount => _nights.Length;
        public IReadOnlyList<string> StartingPegs => _starting;

        /// <param name="startingSlices">Slice ids owned from the start (R3).</param>
        /// <param name="slicesOnDawn">Per night (same order as the nights): the slice ids a dawn on it unlocks; null = none.</param>
        public CampaignPlan(IReadOnlyList<string> nightIds, IReadOnlyList<string> unlocksOnDawn = null, IReadOnlyList<string> startingPegs = null,
                            IReadOnlyList<string> startingSlices = null, IReadOnlyList<IReadOnlyList<string>> slicesOnDawn = null)
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
            if (startingSlices != null)
                foreach (var s in startingSlices) if (!string.IsNullOrEmpty(s) && !_startingSlices.Contains(s)) _startingSlices.Add(s);
            _sliceUnlocks = new List<string>[_nights.Length];
            for (int i = 0; i < _nights.Length; i++)
            {
                _sliceUnlocks[i] = new List<string>();
                if (slicesOnDawn == null || i >= slicesOnDawn.Count || slicesOnDawn[i] == null) continue;
                foreach (var s in slicesOnDawn[i]) if (!string.IsNullOrEmpty(s) && !_sliceUnlocks[i].Contains(s)) _sliceUnlocks[i].Add(s);
            }
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

        /// <summary>The night (1-based) whose dawn unlocks this peg type — the first one, if several do; 0 = none (starting or unknown).</summary>
        public int UnlockNightOf(string pegId)
        {
            if (string.IsNullOrEmpty(pegId) || _starting.Contains(pegId)) return 0;
            for (int i = 0; i < _unlocks.Length; i++)
                if (_unlocks[i] == pegId) return i + 1;
            return 0;
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

        // ---------- slices (R3) ----------

        public bool IsSliceUnlocked(PlayerProfile profile, string sliceId)
        {
            if (string.IsNullOrEmpty(sliceId)) return false;
            if (_startingSlices.Contains(sliceId)) return true;
            for (int i = 0; i < _nights.Length; i++)
                if (_sliceUnlocks[i].Contains(sliceId) && HasDawn(profile, i)) return true;
            return false;
        }

        /// <summary>Every slice the player owns: the starting ones, then those unlocked, in campaign order.</summary>
        public List<string> UnlockedSlices(PlayerProfile profile)
        {
            var list = new List<string>(_startingSlices);
            for (int i = 0; i < _nights.Length; i++)
                if (HasDawn(profile, i))
                    foreach (var s in _sliceUnlocks[i]) if (!list.Contains(s)) list.Add(s);
            return list;
        }

        /// <summary>Every slice the campaign has, in its order (starting first, then by the night that unlocks it).</summary>
        public List<string> AllSlices()
        {
            var list = new List<string>(_startingSlices);
            foreach (var night in _sliceUnlocks) foreach (var s in night) if (!list.Contains(s)) list.Add(s);
            return list;
        }

        /// <summary>The night (1-based) whose dawn unlocks this slice; 0 = a starting slice or unknown.</summary>
        public int SliceUnlockNightOf(string sliceId)
        {
            if (string.IsNullOrEmpty(sliceId) || _startingSlices.Contains(sliceId)) return 0;
            for (int i = 0; i < _sliceUnlocks.Length; i++)
                if (_sliceUnlocks[i].Contains(sliceId)) return i + 1;
            return 0;
        }

        /// <summary>
        /// Tonight's tower, bottom → top, <paramref name="height"/> slices: the player's tower (one for the campaign, carried
        /// over from night to night), slot by slot. A slot past what was built, or holding a slice that isn't owned (any more),
        /// gets the first owned slice. No owned slice at all → an empty list (nothing to build with).
        /// </summary>
        public List<string> TowerFor(PlayerProfile profile, int height)
        {
            var result = new List<string>();
            var owned = UnlockedSlices(profile);
            if (owned.Count == 0 || height < 1) return result;
            for (int i = 0; i < height; i++)
            {
                string saved = i < profile.Tower.Count ? profile.Tower[i] : null;
                result.Add(saved != null && owned.Contains(saved) ? saved : owned[0]);
            }
            return result;
        }
    }
}
