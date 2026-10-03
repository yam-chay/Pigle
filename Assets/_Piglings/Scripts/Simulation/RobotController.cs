using System;
using Piglings.Definitions;
using Piglings.Events;
using UnityEngine;

namespace Piglings.Simulation
{
    public enum RobotState
    {
        Spawned,     // instantiated, not initialized
        Climbing,    // kinematic, moving up the wall
        LosingGrip,  // break clip playing (flail -> crack -> collapse); still kinematic
        Falling,     // dynamic ball, bouncing through the holds; can knock others loose
        Breaching,   // got to the roof: the breach sequence, collider off, ends on a hard time limit
        Removed      // hit the ground / timed out / breach over; about to be destroyed
    }

    /// <summary>
    /// One wolf-bot. State is the single source of truth (ported from CCTD's SpiderController):
    /// EnterState is the ONLY place a state becomes physics/layer changes.
    /// Visuals subscribe to StateChanged; this class never touches sprites or animators.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class RobotController : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private CircleCollider2D ballCollider;

        public GameId Id { get; private set; }
        public RobotState State { get; private set; } = RobotState.Spawned;
        public ChainId Chain { get; private set; } = ChainId.None;
        public int Depth { get; private set; }
        public bool CanLoseGrip => State == RobotState.Climbing;

        // For views on the robot prefab: they can't hold a scene reference to the NightSession,
        // so they reach the bus through their robot. Null until Initialize.
        public NightSession Session => _session;

        public event Action<RobotController, RobotState> StateChanged;

        private NightSession _session;
        private RobotDefinition _def;
        private float _climbSpeed;
        private float _breakTimer;
        private float _fallTimer;
        private float _breachTimer;

        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            ballCollider = GetComponent<CircleCollider2D>();
        }

        public void Initialize(NightSession session, RobotDefinition def, float climbSpeedMultiplier)
        {
            _session = session; _def = def;
            _climbSpeed = def.ClimbSpeed * climbSpeedMultiplier;
            Id = session.Ids.Next();
            ballCollider.radius = def.BallRadius / Mathf.Max(0.0001f, transform.lossyScale.x);
            body.mass = def.BallMass;
            if (def.BallMaterial != null) ballCollider.sharedMaterial = def.BallMaterial;
            session.Bus.Publish(new RobotSpawned(Id));
            SetState(RobotState.Climbing);
        }

        /// <summary>Called by whatever hit this robot (a throwable, or another robot's ball).</summary>
        public void LoseGrip(ChainId chain, Attribution cause)
        {
            if (!CanLoseGrip) return;
            Chain = chain;
            Depth = cause.Depth;
            _session.Bus.Publish(new RobotLostGrip(Id, chain, cause));
            SetState(RobotState.LosingGrip);
        }

        /// <summary>
        /// The end-of-night sweep: a robot still climbing lets go and falls. Not part of any chain — the Rules
        /// score it flat (RobotSwept). Called by RobotSpawner when the night ends. A breaching robot is skipped:
        /// its breach already counted, and its time limit removes it.
        /// </summary>
        public void Sweep()
        {
            if (!CanLoseGrip) return;
            _session.Bus.Publish(new RobotSwept(Id));
            SetState(RobotState.LosingGrip);
        }

        /// <summary>
        /// A climbing robot touched the breach line. It starts its breach sequence and the breach counts NOW
        /// (RobotBreached) — the stone theft or the catch. Removed(EnteredBarn) at the end is only cleanup.
        /// </summary>
        public void ReachTop()
        {
            if (State != RobotState.Climbing) return;
            // State first, then the event: if this breach ends the night, the sweep runs inside the publish
            // (the bus is synchronous) and must already see this robot as not climbing, or it would sweep it.
            // The stolen stone is parented to this robot inside the publish too (StonePile).
            SetState(RobotState.Breaching);
            _session.Bus.Publish(new RobotBreached(Id));
        }

        public void Remove(RemovalReason reason)
        {
            if (State == RobotState.Removed) return;
            SetState(RobotState.Removed);
            _session.Bus.Publish(new RobotRemoved(Id, Chain, reason));
            Destroy(gameObject);
        }

        private void SetState(RobotState next)
        {
            if (State == next) return;
            State = next;
            EnterState(next);
            StateChanged?.Invoke(this, next);
        }

        private void EnterState(RobotState s)
        {
            switch (s)
            {
                case RobotState.Climbing:
                    body.bodyType = RigidbodyType2D.Kinematic;
                    gameObject.layer = LayerMask.NameToLayer(PhysicsLayers.RobotClimbing);
                    break;
                case RobotState.LosingGrip:
                    _breakTimer = _def.BreakDuration;
                    body.linearVelocity = Vector2.zero;
                    break;
                case RobotState.Falling:
                    _fallTimer = _def.MaxFallSeconds;
                    gameObject.layer = LayerMask.NameToLayer(PhysicsLayers.RobotBall);
                    body.bodyType = RigidbodyType2D.Dynamic;
                    body.angularVelocity = UnityEngine.Random.Range(-360f, 360f);
                    break;
                case RobotState.Breaching:
                    // Collider off: it can't be hit, can't hit anything and touches no zone, so it can never start
                    // or join a chain. Kinematic and still: the breach animation plays in place.
                    ballCollider.enabled = false;
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    _breachTimer = _def.BreachSeconds;
                    break;
                case RobotState.Removed:
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    break;
            }
        }

        private void Update()
        {
            if (State == RobotState.LosingGrip)
            {
                _breakTimer -= Time.deltaTime;
                if (_breakTimer <= 0f) SetState(RobotState.Falling);
            }
            else if (State == RobotState.Falling)
            {
                // A ball resting on a hold would keep its chain open forever — and the night with it.
                _fallTimer -= Time.deltaTime;
                if (_fallTimer <= 0f) Remove(RemovalReason.TimedOut);
            }
            else if (State == RobotState.Breaching)
            {
                // Hard limit, not "when the animation ends": an animation event that never fires would leave the
                // robot here forever. Runs while the wall is paused for the choice too — a breaching robot isn't
                // climbing, it finishes its sequence — and after the night ends (the sweep skips it).
                _breachTimer -= Time.deltaTime;
                if (_breachTimer <= 0f) Remove(RemovalReason.EnteredBarn);
            }
        }

        private void FixedUpdate()
        {
            // Paused while the player chooses (no Time.timeScale: only the wall stops).
            if (State == RobotState.Climbing && _session.WallMoving)
                body.MovePosition(body.position + Vector2.up * (_climbSpeed * Time.fixedDeltaTime));
        }

        private void OnCollisionEnter2D(Collision2D c)
        {
            // Only a falling ball carries a chain forward.
            if (State != RobotState.Falling) return;
            if (c.collider.TryGetComponent(out RobotController other) && other.CanLoseGrip)
                other.LoseGrip(Chain, Attribution.FromRobotBall(Id, Depth));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Zones are triggers. Robot-vs-robot hits are real collisions (see OnCollisionEnter2D).
            if (other.TryGetComponent(out BarnTopZone _)) ReachTop();
            else if (other.TryGetComponent(out GroundZone _) && State == RobotState.Falling) Remove(RemovalReason.HitGround);
        }
    }
}
