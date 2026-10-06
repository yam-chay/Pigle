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
        Breaching,   // got to the roof: climbs onto the perch, takes the stone, jumps off; collider off, hard time limit
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
        // A swept robot still holding on (a loss: it lets go after the wolf's beat) is already counted — nothing may knock it again.
        public bool CanLoseGrip => State == RobotState.Climbing && !_swept;

        /// <summary>The breach sequence's length (RobotDefinition). StonePile times the stolen stone's hop from it.</summary>
        public float BreachSeconds => _def != null ? _def.BreachSeconds : 0f;

        // For views on the robot prefab: they can't hold a scene reference to the NightSession,
        // so they reach the bus through their robot. Null until Initialize.
        public NightSession Session => _session;

        public event Action<RobotController, RobotState> StateChanged;

        private NightSession _session;
        private RobotDefinition _def;
        private bool _swept;           // counted by the end-of-night sweep (RobotSwept); may still be holding on (LetGo)
        private float _climbSpeed;
        private float _breakTimer;
        private float _fallTimer;
        private float _stuckTimer;     // how long a falling ball has been (nearly) still
        private float _breachElapsed;
        private Vector2 _breachFrom;   // where it touched the breach line
        private Vector2 _perch;        // its breach slot on the perch (BarnTopZone)
        private BarnTopZone _breachZone;  // holds that slot until this robot is removed
        private float _jumpAway;       // +1 / -1: the side it jumps off

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
            session.Bus.Publish(new RobotSpawned(Id, def.Id));
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
        /// <paramref name="letGo"/> false (a loss in the campaign, M11.T1): it's counted now, inside the night's end like
        /// every sweep, but keeps holding on (the wall is still in Ended) until LetGo — after the wolf's beat.
        /// True = it was swept (false: not climbing, nothing done).
        /// </summary>
        public bool Sweep(bool letGo = true)
        {
            if (!CanLoseGrip) return false;
            _swept = true;
            _session.Bus.Publish(new RobotSwept(Id));
            if (letGo) SetState(RobotState.LosingGrip);
            return true;
        }

        /// <summary>A swept robot still holding on lets go now (RobotSpawner.LetGoSwept). Any other robot: nothing.</summary>
        public void LetGo()
        {
            if (_swept && State == RobotState.Climbing) SetState(RobotState.LosingGrip);
        }

        /// <summary>
        /// A climbing robot touched the breach line. It starts its breach sequence and the breach counts NOW
        /// (RobotBreached) — the stone theft or the catch. Removed(EnteredBarn) at the end is only cleanup.
        /// </summary>
        public void ReachTop(BarnTopZone zone)
        {
            if (State != RobotState.Climbing) return;
            _breachFrom = body.position;
            _breachZone = zone;
            if (zone != null) _perch = zone.Claim(Id, _breachFrom, _def.BallRadius, out _jumpAway);
            else { _perch = _breachFrom; _jumpAway = 1f; }
            // State first, then the event: if this breach ends the night, the sweep runs inside the publish
            // (the bus is synchronous) and must already see this robot as not climbing, or it would sweep it.
            // The stolen stone starts its hop to this robot inside the publish too (StonePile).
            SetState(RobotState.Breaching);
            _session.Bus.Publish(new RobotBreached(Id));
        }

        public void Remove(RemovalReason reason)
        {
            if (State == RobotState.Removed) return;
            SetState(RobotState.Removed);
            if (_breachZone != null) _breachZone.Release(Id);
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
                    _stuckTimer = 0f;
                    gameObject.layer = LayerMask.NameToLayer(PhysicsLayers.RobotBall);
                    body.bodyType = RigidbodyType2D.Dynamic;
                    body.angularVelocity = UnityEngine.Random.Range(-360f, 360f);
                    break;
                case RobotState.Breaching:
                    // Collider off: it can't be hit, can't hit anything and touches no zone, so it can never start
                    // or join a chain. Kinematic: FixedUpdate moves it along the breach path (BreachTiming).
                    ballCollider.enabled = false;
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    _breachElapsed = 0f;
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
                // A ball resting on a hold or wedged between pegs would keep its chain open — and the next hour with
                // it. Two limits: still for StuckSeconds (the usual case, quick), or MaxFallSeconds in the air at all.
                // A ball just starting to fall is still for a moment too, but gravity gets it past StuckSpeed in a
                // few frames, well inside StuckSeconds.
                _fallTimer -= Time.deltaTime;
                _stuckTimer = body.linearVelocity.sqrMagnitude < _def.StuckSpeed * _def.StuckSpeed ? _stuckTimer + Time.deltaTime : 0f;
                if (_fallTimer <= 0f || _stuckTimer >= _def.StuckSeconds) Remove(RemovalReason.TimedOut);
            }
        }

        private void FixedUpdate()
        {
            if (State == RobotState.Breaching)
            {
                StepBreach();
                return;
            }

            // Paused while the player chooses (no Time.timeScale: only the wall stops).
            if (State == RobotState.Climbing && _session.WallMoving)
                body.MovePosition(body.position + Vector2.up * (_climbSpeed * Time.fixedDeltaTime));
        }

        // The breach sequence, all timed from BreachSeconds (BreachTiming): up onto the perch, hold the stolen
        // stone (StonePile hops it over), jump off. Runs while the wall is paused for peg placement too — a breaching
        // robot isn't climbing, it finishes its sequence — and after the night ends (the sweep skips it).
        // The removal is a hard limit, not "when the animation ends": an animation event that never fires would
        // leave the robot here forever. Moving a kinematic body is not a physics change, so EnterState stays the
        // only place that changes body type, layer and collider.
        private void StepBreach()
        {
            _breachElapsed += Time.fixedDeltaTime;
            float phase = BreachTiming.Phase(_breachElapsed, _def.BreachSeconds);

            var pos = Vector2.Lerp(_breachFrom, _perch, BreachTiming.ClimbProgress(phase));
            BreachTiming.JumpOffset(BreachTiming.JumpProgress(phase), _jumpAway, _def.BallRadius, out float dx, out float dy);
            body.MovePosition(pos + new Vector2(dx, dy));

            if (phase >= 1f) Remove(RemovalReason.EnteredBarn);
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
            if (other.TryGetComponent(out BarnTopZone zone)) ReachTop(zone);
            else if (other.TryGetComponent(out GroundZone _) && State == RobotState.Falling) Remove(RemovalReason.HitGround);
        }
    }
}
