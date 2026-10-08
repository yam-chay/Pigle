using System.Collections.Generic;
using Piglings.Events;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// A peg's effect and ONLY its own settings (R4): one class per effect, held by the PegDefinition through
    /// [SerializeReference], so the Inspector shows just the picked effect's fields — no Splitter numbers on a Bomb. Each
    /// effect keeps its own levels list (entry 0 = level 1; a level past the list uses the last entry) and its own LOOK — the
    /// effects only it plays (R4c, Yam: tuning one peg shouldn't mean hunting through a shared view). Colours shared by the
    /// whole game (the gold) stay in Visuals. PegBoardView plays these; it keeps only board-wide settings.
    /// Plain [Serializable] types, so they may share this file (only MonoBehaviours / ScriptableObjects need their own).
    /// </summary>
    [System.Serializable]
    public abstract class PegEffectSettings
    {
        public abstract PegEffect Kind { get; }
        public abstract int LevelCount { get; }

        /// <summary>The common numbers of this level (score, mastery weight; special pegs add a mult bonus). Null = no levels.</summary>
        public abstract PegEffectLevel LevelAt(int level);

        /// <summary>A fresh effect of this kind, with one level of sensible defaults (never the Inspector's all-zero first entry).</summary>
        public static PegEffectSettings Create(PegEffect kind)
        {
            switch (kind)
            {
                case PegEffect.Bouncy: return new BouncyEffect();
                case PegEffect.Splitter: return new SplitterEffect();
                case PegEffect.Bomb: return new BombEffect();
                default: return new PlainEffect();
            }
        }

        protected static T At<T>(List<T> levels, int level) where T : class =>
            levels.Count == 0 ? null : levels[Mathf.Clamp(level - 1, 0, levels.Count - 1)];
    }

    /// <summary>What every level has: its SCORE value and its peg-mastery weight.</summary>
    [System.Serializable]
    public class PegEffectLevel
    {
        [Tooltip("Peg mastery per trigger at this level (a Bouncy bonus granted, a split, an explosion). 0 = the level itself " +
                 "(level 2 counts double).")]
        [Min(0f)] public float masteryWeight = 0f;
        [Tooltip("This peg's SCORE value at this level — a Plain peg adds it on every contact (its Contact Cooldown applies), a " +
                 "special peg when it triggers. -1 = the default: Peg_Plain's level-1 value for a Plain peg, nothing for a special " +
                 "one. Peg_Plain itself needs a real value (every empty hold scores it).")]
        [Min(-1)] public int scoreValue = -1;
    }

    /// <summary>A special peg's level adds what it gives its chain's MULT when it triggers.</summary>
    [System.Serializable]
    public class SpecialPegLevel : PegEffectLevel
    {
        [Tooltip("What this peg adds to its chain's MULT when it triggers at this level (Bouncy: a stone or ball bouncing off " +
                 "it, once per peg per stone / ball; Splitter: a split; Bomb: an explosion). 0 = none.")]
        [Min(0f)] public float multBonus = 0f;
    }

    // ---------- Plain ----------

    [System.Serializable]
    public sealed class PlainLevel : PegEffectLevel
    {
        public PlainLevel() { scoreValue = 1; }
    }

    /// <summary>Plain: no effect, a hold with a look and a score (Peg_Plain is every empty hold).</summary>
    [System.Serializable]
    public sealed class PlainEffect : PegEffectSettings
    {
        [Tooltip("Seconds before the same stone / ball scores again on the same hold — so rattling or resting balls can't farm " +
                 "it (Max Fall Seconds still ends a stuck ball).")]
        [Min(0f)] public float contactCooldown = 0.2f;
        public List<PlainLevel> levels = new List<PlainLevel> { new PlainLevel() };

        public override PegEffect Kind => PegEffect.Plain;
        public override int LevelCount => levels.Count;
        public override PegEffectLevel LevelAt(int level) => At(levels, level);
    }

    // ---------- Bouncy ----------

    [System.Serializable]
    public sealed class BouncyLevel : SpecialPegLevel
    {
        [Tooltip("Physical bounciness of the peg (0..1+). Balls and stones visibly pop off it. 0 = the hold's own material.")]
        [Min(0f)] public float bounciness = 0.8f;
        public BouncyLevel() { multBonus = 1f; }
    }

    /// <summary>Bouncy: a stone or ball bouncing off it adds its mult bonus to the chain, once per peg per hitter.</summary>
    [System.Serializable]
    public sealed class BouncyEffect : PegEffectSettings
    {
        public List<BouncyLevel> levels = new List<BouncyLevel> { new BouncyLevel() };

        [Header("Look")]
        [Tooltip("How long the squash-stretch lasts after every hit.")]
        [Min(0.01f)] public float popSeconds = 0.3f;
        [Tooltip("How far it squashes: 0.3 = 30% wider, then 30% taller.")]
        [Range(0f, 1f)] public float popAmount = 0.4f;
        [Tooltip("fx_burst_ring: the small ring (in the Visuals' gold) when a stone / ball gets the bonus. Empty = no ring.")]
        public Sprite bonusRing;
        public FxMotion bonusRingMotion = new FxMotion { seconds = 0.5f, startScale = 0.2f, endScale = 1.2f };

        public override PegEffect Kind => PegEffect.Bouncy;
        public override int LevelCount => levels.Count;
        public override PegEffectLevel LevelAt(int level) => At(levels, level);
        public BouncyLevel Level(int level) => At(levels, level);
    }

    // ---------- Splitter ----------

    [System.Serializable]
    public sealed class SplitterLevel : SpecialPegLevel
    {
        [Tooltip("A thrown stone that hits the needle becomes this many stones, itself included (2, 3…). Robot balls never split.")]
        [Min(1)] public int pieces = 2;
        [Tooltip("Degrees between the outermost pieces, fanned around the stone's direction after the bounce. Same speed.")]
        [Range(0f, 180f)] public float fanAngle = 30f;
    }

    /// <summary>Splitter (the needle): a thrown stone that hits it becomes several, fanned, same chain.</summary>
    [System.Serializable]
    public sealed class SplitterEffect : PegEffectSettings
    {
        [Tooltip("Most stones one throw can have flying at once, the original included. Splits beyond it are cut short.")]
        [Min(1)] public int maxStonesPerThrow = 4;
        [Tooltip("A piece's size (collider and sprite) × the stone's. Gameplay: smaller pieces are harder to hit with.")]
        [Range(0.3f, 1f)] public float pieceScale = 0.8f;
        [Tooltip("On: robots a piece knocks loose itself count as stone hits for mastery, like the stone's own.")]
        public bool countSplitHitsForMastery = true;
        public List<SplitterLevel> levels = new List<SplitterLevel> { new SplitterLevel() };

        [Header("Look")]
        [Tooltip("The needle's colour at the moment it splits a stone, fading back.")]
        public Color flashColour = new Color(1f, 1f, 0.6f, 1f);
        [Min(0.01f)] public float flashSeconds = 0.4f;
        [Tooltip("fx_sparkle: the burst out of the needle when it splits. Empty = no burst.")]
        public Sprite sparkle;
        [Min(0)] public int sparkles = 6;
        [Tooltip("How far (world units) the burst's sparkles fly out.")]
        [Min(0f)] public float sparkleSpread = 0.5f;
        public Color sparkleColour = new Color(1f, 0.95f, 0.7f, 1f);
        public FxMotion sparkleMotion = new FxMotion { seconds = 0.4f, startScale = 0.35f, endScale = 0.15f, spin = 200f };

        public override PegEffect Kind => PegEffect.Splitter;
        public override int LevelCount => levels.Count;
        public override PegEffectLevel LevelAt(int level) => At(levels, level);
        public SplitterLevel Level(int level) => At(levels, level);
    }

    // ---------- Bomb ----------

    [System.Serializable]
    public sealed class BombLevel : SpecialPegLevel
    {
        [Tooltip("A stone or falling ball that hits it knocks every CLIMBING robot within this radius (world units) loose.")]
        [Min(0f)] public float bombRadius = 1f;
        [Tooltip("Seconds it stays spent (a plain hold) after going off. Counts only while the wall moves.")]
        [Min(0f)] public float cooldownSeconds = 8f;
        [Tooltip("Push given to falling balls and flying stones in the radius, away from the bomb (0 = none).")]
        [Min(0f)] public float impulse = 0.5f;
    }

    /// <summary>Bomb: a stone or ball sets it off — climbing robots in its radius are knocked loose; then it recharges.</summary>
    [System.Serializable]
    public sealed class BombEffect : PegEffectSettings
    {
        [Tooltip("How it looks while spent (after going off, until it recharges). Empty = its sprite, tinted Spent Tint.")]
        public Sprite spentSprite;
        public List<BombLevel> levels = new List<BombLevel> { new BombLevel() };

        [Header("Look")]
        [Tooltip("The explosion's colour: its star, its ring and the recharge sparkles.")]
        public Color colour = new Color(1f, 0.55f, 0.15f, 1f);
        [Tooltip("fx_hit_star at the bomb when it goes off. Empty = none.")]
        public Sprite star;
        public FxMotion starMotion = new FxMotion { seconds = 0.3f, startScale = 0.8f, endScale = 1.6f, spin = 60f };
        [Tooltip("fx_burst_ring: grows to the bomb's radius, so you see exactly what it reached. Empty = none.")]
        public Sprite ring;
        [Tooltip("The ring's end size is set from the radius; Seconds and Start Scale are used as is.")]
        public FxMotion ringMotion = new FxMotion { seconds = 0.45f, startScale = 0.2f, endScale = 1f };
        [Tooltip("A spent bomb without a Spent Sprite is tinted this instead.")]
        public Color spentTint = new Color(0.45f, 0.45f, 0.45f, 1f);
        [Tooltip("fx_sparkle: fizzes on the fuse when it recharges. Empty = none.")]
        public Sprite rechargeSparkle;
        [Tooltip("Where the fuse is, from the peg's centre (world units).")]
        public Vector2 fuseOffset = new Vector2(0.05f, 0.12f);
        [Min(0)] public int rechargeSparkles = 4;
        public FxMotion rechargeSparkleMotion = new FxMotion { seconds = 0.5f, startScale = 0.25f, endScale = 0.05f, rise = 0.15f, spin = 240f };

        public override PegEffect Kind => PegEffect.Bomb;
        public override int LevelCount => levels.Count;
        public override PegEffectLevel LevelAt(int level) => At(levels, level);
        public BombLevel Level(int level) => At(levels, level);
    }
}
