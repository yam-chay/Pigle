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

        public GameId Id { get; private set; }
        public ChainId Chain { get; private set; }

        private NightSession _session;
        private float _life;
        private bool _removed;

        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
        }

        public void Launch(NightSession session, ThrowableDefinition def, Vector2 velocity)
        {
            _session = session;
            Id = session.Ids.Next();
            Chain = new ChainId(session.Ids.Next());
            _life = def.Lifetime;
            gameObject.layer = LayerMask.NameToLayer(PhysicsLayers.Throwable);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = def.Mass;
            circle.radius = def.Radius / Mathf.Max(0.0001f, transform.lossyScale.x);
            if (def.Material != null) circle.sharedMaterial = def.Material;
            body.linearVelocity = velocity;
            session.Bus.Publish(new ThrowReleased(Chain, Id));
        }

        private void Update()
        {
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
