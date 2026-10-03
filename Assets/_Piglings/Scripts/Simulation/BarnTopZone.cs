using System.Collections.Generic;
using Piglings.Events;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Trigger at the breach line. A climbing robot touching it has reached the top and starts its breach.
    ///
    /// It also owns the perch's breach slots: fixed points beside the stone pile, above the throw line, where
    /// breaching robots stand — clearly out of play, never where the player aims at the robots still climbing.
    /// A breaching robot takes the free slot nearest to where it arrived and frees it when it's removed, so
    /// thieves line up side by side, each with its stone.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class BarnTopZone : MonoBehaviour
    {
        [Tooltip("Breach slots on the perch (4–5, beside the pile, above the throw line), in a row, in order. " +
                 "When all are taken, extra robots stand past the last one, slightly overlapping.")]
        [SerializeField] private Transform[] slots;

        // How far past the last slot an extra robot stands, as a share of the slot spacing: < 1 = a small overlap.
        // Rare (every slot full), but defined.
        private const float OverflowStep = 0.7f;

        private readonly Dictionary<GameId, int> _taken = new Dictionary<GameId, int>();   // robot → slot index
        private readonly List<GameId> _overflow = new List<GameId>();                     // robots past the last slot
        private bool _warned;

        /// <summary>
        /// Takes a slot for this robot and returns where it stands, plus the side it jumps off (+1 right, -1 left:
        /// away from this zone's centre). Without slots the robot breaches where it is — playable, but in the line of fire.
        /// </summary>
        public Vector2 Claim(GameId robot, Vector2 arrivedAt, float ballRadius, out float away)
        {
            Vector2 spot = Spot(robot, arrivedAt, ballRadius);
            away = spot.x >= transform.position.x ? 1f : -1f;
            return spot;
        }

        /// <summary>The robot is gone: its slot is free again.</summary>
        public void Release(GameId robot)
        {
            _taken.Remove(robot);
            _overflow.Remove(robot);
        }

        private Vector2 Spot(GameId robot, Vector2 arrivedAt, float ballRadius)
        {
            if (slots == null || slots.Length == 0)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("BarnTopZone: no breach slots assigned — breaching robots stay where they reached the top.", this);
                }
                return arrivedAt;
            }

            // The free slot nearest to where it arrived, so robots don't cross each other on the way up.
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || _taken.ContainsValue(i)) continue;
                float d = Mathf.Abs(slots[i].position.x - arrivedAt.x);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            if (best >= 0)
            {
                _taken[robot] = best;
                return slots[best].position;
            }

            // All full: past the last slot, continuing the row, a little closer than the slots are to each other.
            _overflow.Add(robot);
            Vector2 last = slots[slots.Length - 1].position;
            Vector2 step = slots.Length > 1
                ? last - (Vector2)slots[slots.Length - 2].position
                : new Vector2(2f * ballRadius, 0f);
            return last + step * (OverflowStep * _overflow.Count);
        }
    }
}
