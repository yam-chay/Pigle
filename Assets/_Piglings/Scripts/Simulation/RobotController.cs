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
        ReachedTop,  // got to the roof
        Removed      // hit the ground / entered the barn; about to be destroyed
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

        public event Action<RobotController, RobotState> StateChanged;

        private NightSession _session;
        private RobotDefinition _def;
        private float _climbSpeed;
        private float _breakTimer;

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

        public void ReachTop()
        {
            if (State != RobotState.Climbing) return;
            SetState(RobotState.ReachedTop);
            Remove(RemovalReason.EnteredBarn);
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
                    gameObject.layer = LayerMask.NameToLayer(PhysicsLayers.RobotBall);
                    body.bodyType = RigidbodyType2D.Dynamic;
                    body.angularVelocity = UnityEngine.Random.Range(-360f, 360f);
                    break;
                case RobotState.ReachedTop:
                case RobotState.Removed:
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    break;
            }
        }

        private void Update()
        {
            if (State != RobotState.LosingGrip) return;
            _breakTimer -= Time.deltaTime;
            if (_breakTimer <= 0f) SetState(RobotState.Falling);
        }

        private void FixedUpdate()
        {
            if (State == RobotState.Climbing)
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
