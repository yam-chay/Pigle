using System;
using System.Collections.Generic;
using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>One closed chain, as the balance log writes it (M11.T3).</summary>
    public sealed class ChainRecord
    {
        public int Index;            // 1, 2, 3… in the order chains closed tonight
        public int Hour;             // the hour it was thrown in
        public float ThrownAt;       // seconds since the night began
        public int Score;            // SCORE (raw)
        public float Mult;
        public float HourMultiplier;
        public int Total;            // the result: score × mult × hour
        public int Wolves;           // robots it dropped
        public int Depth;            // the deepest depth it reached
        public int Wall;             // robots climbing when it was thrown (wall density)
        public int StonesLeft;       // on the pile right after the throw
        public bool Miss => Wolves == 0;
    }

    /// <summary>One hour of the night, summed from its chains (by the hour they were thrown in).</summary>
    public sealed class HourRecord
    {
        public int Hour;
        public float Seconds;        // from the hour's start (the night's begin, or its round) to the next hour's start / the night's end
        public int Chains;
        public int Wolves;           // dropped by the hour's chains (the sweep isn't a chain)
        public int Total;            // the hour's chains' results
        public int MissTotal;        // the part of Total from chains that dropped no wolf
        public int Breaches;
        /// <summary>The share of the hour's chain score from misses, 0..1 (0 with no score) — a miss → hour → refill loop shows here.</summary>
        public float MissShare => Total > 0 ? MissTotal / (float)Total : 0f;
    }

    /// <summary>
    /// The numbers behind tuning (M11.T3), gathered from the bus while a night plays: every chain with the wall's density
    /// at its throw, and per hour its length, wolves and how much of its score came from misses. Rules can't keep time, so
    /// the caller passes its clock. Reads only — nothing in the game depends on it. Simulation's BalanceLog writes it.
    /// </summary>
    public sealed class BalanceTally
    {
        private sealed class Thrown { public float At; public int Hour; public int Wall; public int StonesLeft; }

        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly Func<float> _clock;
        private readonly HashSet<GameId> _climbing = new HashSet<GameId>();
        private readonly Dictionary<GameId, Thrown> _thrown = new Dictionary<GameId, Thrown>();
        private readonly List<ChainRecord> _chains = new List<ChainRecord>();
        private readonly List<float> _hourStarts = new List<float>();   // index 0 = hour 1's start
        private float _endedAt = -1f;
        private long _wallSum;
        private int _throws;

        public IReadOnlyList<ChainRecord> Chains => _chains;
        public float BeganAt => _hourStarts[0];
        /// <summary>Seconds from the night's begin to its end (or to now, while it plays).</summary>
        public float Seconds => (_endedAt >= 0f ? _endedAt : _clock()) - BeganAt;
        /// <summary>Robots climbing at a throw, on average over tonight's throws (0 before the first).</summary>
        public float AverageWall => _throws > 0 ? _wallSum / (float)_throws : 0f;

        public BalanceTally(EventBus bus, NightState state, Func<float> clock)
        {
            _bus = bus; _state = state; _clock = clock;
            _hourStarts.Add(clock());
            _bus.Subscribe<NightPhaseChanged>(OnPhaseChanged);
            _bus.Subscribe<RobotSpawned>(OnSpawned);
            _bus.Subscribe<RobotLostGrip>(OnLostGrip);
            _bus.Subscribe<RobotBreached>(OnBreached);
            _bus.Subscribe<RobotSwept>(OnSwept);
            _bus.Subscribe<RobotRemoved>(OnRemoved);
            _bus.Subscribe<ThrowReleased>(OnThrow);
            _bus.Subscribe<ChainScored>(OnChainScored);
            _bus.Subscribe<HourReached>(OnHourReached);
            _bus.Subscribe<NightEnded>(OnNightEnded);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<NightPhaseChanged>(OnPhaseChanged);
            _bus.Unsubscribe<RobotSpawned>(OnSpawned);
            _bus.Unsubscribe<RobotLostGrip>(OnLostGrip);
            _bus.Unsubscribe<RobotBreached>(OnBreached);
            _bus.Unsubscribe<RobotSwept>(OnSwept);
            _bus.Unsubscribe<RobotRemoved>(OnRemoved);
            _bus.Unsubscribe<ThrowReleased>(OnThrow);
            _bus.Unsubscribe<ChainScored>(OnChainScored);
            _bus.Unsubscribe<HourReached>(OnHourReached);
            _bus.Unsubscribe<NightEnded>(OnNightEnded);
        }

        /// <summary>
        /// Hour by hour, 1 → the last hour played: its length and its chains summed by the hour they were thrown in.
        /// The breaches come from NightState's per-hour stats.
        /// </summary>
        public List<HourRecord> Hours()
        {
            int played = Math.Max(_hourStarts.Count, _state.Hours.Count);
            foreach (var c in _chains) if (c.Hour > played) played = c.Hour;
            var hours = new List<HourRecord>();
            float end = _endedAt >= 0f ? _endedAt : _clock();
            for (int h = 1; h <= played; h++)
            {
                float from = h - 1 < _hourStarts.Count ? _hourStarts[h - 1] : end;
                float to = h < _hourStarts.Count ? _hourStarts[h] : end;
                var r = new HourRecord { Hour = h, Seconds = Math.Max(0f, to - from), Breaches = h - 1 < _state.Hours.Count ? _state.Hours[h - 1].Breaches : 0 };
                foreach (var c in _chains)
                {
                    if (c.Hour != h) continue;
                    r.Chains++;
                    r.Wolves += c.Wolves;
                    r.Total += c.Total;
                    if (c.Miss) r.MissTotal += c.Total;
                }
                hours.Add(r);
            }
            return hours;
        }

        // The clock starts when the night does (leaving Dusk), not in the day phase.
        private void OnPhaseChanged(NightPhaseChanged e)
        {
            if (e.From == NightPhase.Dusk) _hourStarts[0] = _clock();
        }

        private void OnSpawned(RobotSpawned e) => _climbing.Add(e.Robot);
        private void OnLostGrip(RobotLostGrip e) => _climbing.Remove(e.Robot);
        private void OnBreached(RobotBreached e) => _climbing.Remove(e.Robot);
        private void OnSwept(RobotSwept e) => _climbing.Remove(e.Robot);
        private void OnRemoved(RobotRemoved e) => _climbing.Remove(e.Robot);

        private void OnThrow(ThrowReleased e)
        {
            _thrown[e.Chain.Id] = new Thrown { At = _clock() - BeganAt, Hour = _state.Hour, Wall = _climbing.Count, StonesLeft = _state.StonesLeft };
            _wallSum += _climbing.Count;
            _throws++;
        }

        private void OnChainScored(ChainScored e)
        {
            _thrown.TryGetValue(e.Chain.Id, out var t);
            _thrown.Remove(e.Chain.Id);
            _chains.Add(new ChainRecord
            {
                Index = _chains.Count + 1, Hour = e.Hour, ThrownAt = t != null ? t.At : 0f,
                Score = e.Score, Mult = e.Mult, HourMultiplier = e.HourMultiplier, Total = e.Total,
                Wolves = e.RobotsDropped, Depth = e.MaxDepth, Wall = t != null ? t.Wall : 0, StonesLeft = t != null ? t.StonesLeft : 0,
            });
        }

        // An hour starts at its round (HourReached); the last one runs to the night's end (a dawn waits for the chains in flight).
        private void OnHourReached(HourReached e)
        {
            while (_hourStarts.Count < e.Hour) _hourStarts.Add(_clock());
        }

        private void OnNightEnded(NightEnded e) => _endedAt = _clock();
    }
}
