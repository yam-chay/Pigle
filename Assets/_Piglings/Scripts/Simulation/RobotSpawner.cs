using System.Collections.Generic;
using Piglings.Events;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Spawns robots along the bottom of the wall at the night's interval, and sweeps the wall when the
    /// night ends (every robot still climbing lets go). It spawned them, so it's the one that knows them.
    /// </summary>
    public sealed class RobotSpawner : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private RobotController robotPrefab;
        [SerializeField] private Transform container;
        [SerializeField] private float minX = -2.5f;
        [SerializeField] private float maxX = 2.5f;

        private float _timer;
        private readonly List<RobotController> _spawned = new List<RobotController>();

        // Views that need to find a robot by its GameId (e.g. score popups) listen here.
        // Raised after Initialize, so the robot already has its Id.
        public event System.Action<RobotController> Spawned;

        // Start, not Awake: the session's bus is created in its Awake.
        private void Start() => session.Bus.Subscribe<NightPhaseChanged>(OnPhaseChanged);

        private void OnDestroy()
        {
            if (session != null && session.Bus != null) session.Bus.Unsubscribe<NightPhaseChanged>(OnPhaseChanged);
        }

        // Runs inside the referee's phase change (the bus is synchronous): every RobotSwept is scored
        // before the night banks its mastery and publishes NightEnded.
        private void OnPhaseChanged(NightPhaseChanged e)
        {
            if (e.To != NightPhase.Ended) return;
            foreach (var robot in _spawned)
                if (robot != null) robot.Sweep();   // destroyed robots compare equal to null; Sweep skips non-climbers
            _spawned.Clear();
        }

        private void Update()
        {
            if (!session.WallMoving) return;   // paused for the choice, or the night is over
            var night = session.Night;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = night.SpawnInterval;

            var pos = new Vector3(Random.Range(minX, maxX), transform.position.y, 0f);
            var robot = Instantiate(robotPrefab, pos, Quaternion.identity, container);
            robot.Initialize(session, night.Robot, night.Wall != null ? night.Wall.ClimbSpeedMultiplier : 1f);
            _spawned.RemoveAll(r => r == null);   // forget destroyed robots so the list stays small
            _spawned.Add(robot);
            Spawned?.Invoke(robot);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var y = transform.position.y;
            Gizmos.DrawLine(new Vector3(minX, y), new Vector3(maxX, y));
        }
    }
}
