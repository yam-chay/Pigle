using Piglings.Definitions;
using Piglings.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Piglings.Simulation
{
    /// <summary>
    /// Something the pig threw. Starts a chain; every robot it knocks loose directly is depth 0.
    /// It keeps flying after a hit, so one stone can knock several robots.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class Throwable : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private CircleCollider2D circle;
        [Tooltip("The stone's sprite. Its size follows the collider (ApplyLevel). Empty = the one on this object.")]
        [SerializeField] private SpriteRenderer look;

        public GameId Id { get; private set; }
        public ChainId Chain { get; private set; }

        /// <summary>The mastery level this stone looks like / flies as (1 until ApplyLevel). Views read it (trail colour).</summary>
        public int Level { get; private set; } = 1;

        /// <summary>× its level's size: 1 for a thrown stone, smaller for a Splitter piece (PegDefinition.PieceScale).</summary>
        public float SizeScale { get; private set; } = 1f;

        /// <summary>The definition its level came from (null until ApplyLevel). Views read the trail colour from it.</summary>
        public ThrowableDefinition Definition { get; private set; }

        /// <summary>Thrown and flying (not on the pile, in the hand, or riding a thief). Views start the trail on it.</summary>
        public bool InFlight => _launched && !_removed;

        /// <summary>The stone's sprite, for views that tint it (the pile's gold pulse). Never resized by a view.</summary>
        public SpriteRenderer Look => look != null ? look : (look = GetComponent<SpriteRenderer>());

        // ThrowController reads this on the prefab to aim with the gravity the stone will really feel.
        public float GravityScale => body.gravityScale;

        private NightSession _session;
        private float _life;
        private bool _removed;
        private bool _launched;   // false while it sits on the pile, hops to the hand, or is carried off by a thief

        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
            look = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Looks and sizes the stone for a mastery level: the level's sprite (if it has one), and a scale that makes the
        /// sprite exactly as wide as the collider — so what you see is what hits. The pile calls it on each stone; Launch
        /// calls it again with the night's level, which is the one that counts for play.
        /// </summary>
        public void ApplyLevel(ThrowableDefinition def, int level, float sizeScale = 1f)
        {
            Level = level;
            Definition = def;
            SizeScale = sizeScale;
            if (look == null) look = GetComponent<SpriteRenderer>();   // prefabs saved before this field existed
            float worldRadius = def.RadiusAt(level) * sizeScale;

            var sprite = def.SpriteFor(level);
            if (look != null && sprite != null) look.sprite = sprite;

            // Size from the sprite itself (whatever its pixels-per-unit): its width at scale 1 → the diameter we want.
            // Uniform scale, and the parent's scale divided out, so it's right on the pile, in the hand or in flight.
            if (look != null && look.sprite != null && look.sprite.bounds.size.x > 0f)
            {
                float worldScale = 2f * worldRadius / look.sprite.bounds.size.x;
                float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
                transform.localScale = Vector3.one * (worldScale / Mathf.Max(0.0001f, parentScale));
            }
            circle.radius = worldRadius / Mathf.Max(0.0001f, transform.lossyScale.x);
        }

        /// <summary>
        /// Not a weapon (yet): no physics, no collider. Used while the stone sits on the pile, rides to the
        /// pig's hand, or is carried off by a robot — so it can never hit anything or start a chain.
        /// </summary>
        public void Park()
        {
            body.simulated = false;
            circle.enabled = false;
        }

        public void Launch(NightSession session, ThrowableDefinition def, Vector2 velocity)
        {
            _session = session;
            _launched = true;
            body.simulated = true;
            circle.enabled = true;
            Id = session.Ids.Next();
            Chain = new ChainId(session.Ids.Next());
            _life = def.Lifetime;
            gameObject.layer = LayerMask.NameToLayer(PhysicsLayers.Throwable);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = def.Mass;
            // The level was fixed when the night started (a level earned tonight applies next night), whatever the pile shows.
            ApplyLevel(def, session.WeaponLevel);
            if (def.Material != null) circle.sharedMaterial = def.Material;
            body.linearVelocity = velocity;
            session.Bus.Publish(new ThrowReleased(Chain, Id, def.Id));
        }

        /// <summary>
        /// Splitter: launches newPieces more stones from this one, fanned around its velocity (just after the bounce off
        /// the needle) at the same speed, in its chain. This stone keeps flying as the middle one of the fan. Called by
        /// the Hold once the Rules decided the split (StoneSplit); each piece then announces itself (StonePieceLaunched).
        /// Pieces don't touch the pile: they come from the throw.
        /// </summary>
        public void Split(int newPieces, float fanDegrees, float pieceScale, int socket)
        {
            if (!InFlight || newPieces <= 0) return;
            Vector2 velocity = body.linearVelocity;
            int total = newPieces + 1;
            int keep = total / 2;   // this stone takes a middle direction, so its own course changes least
            for (int i = 0; i < total; i++)
            {
                float angle = -fanDegrees / 2f + fanDegrees * i / (total - 1);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * velocity;
                if (i == keep) { body.linearVelocity = direction; continue; }
                var piece = Instantiate(this, transform.position, transform.rotation, transform.parent);
                piece.LaunchPiece(this, direction, pieceScale, socket);
            }
        }

        // A piece: a copy of its parent (same level look, trail), a bit smaller, flying in the parent's chain with the
        // parent's remaining lifetime. No ThrowReleased — it isn't a throw, and the stone count doesn't change.
        private void LaunchPiece(Throwable parent, Vector2 velocity, float pieceScale, int socket)
        {
            _session = parent._session;
            _launched = true;
            body.simulated = true;
            circle.enabled = true;
            Id = _session.Ids.Next();
            Chain = parent.Chain;
            _life = parent._life;
            gameObject.layer = parent.gameObject.layer;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = parent.body.mass;
            ApplyLevel(parent.Definition, parent.Level, parent.SizeScale * pieceScale);
            circle.sharedMaterial = parent.circle.sharedMaterial;
            body.linearVelocity = velocity;
            _session.Bus.Publish(new StonePieceLaunched(Chain, Id, parent.Id, socket));
        }

        private void Update()
        {
            if (!_launched) return;   // a parked stone has no lifetime
            _life -= Time.deltaTime;
            if (_life <= 0f) Remove();
        }

        private void OnCollisionEnter2D(Collision2D c)
        {
            if (c.collider.TryGetComponent(out RobotController robot) && robot.CanLoseGrip)
                robot.LoseGrip(Chain, Attribution.FromThrowable(Id));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out GroundZone _)) Remove();
        }

        private void Remove()
        {
            if (_removed) return;
            _removed = true;
            _session.Bus.Publish(new ThrowableRemoved(Id, Chain));
            Destroy(gameObject);
        }
    }
}
