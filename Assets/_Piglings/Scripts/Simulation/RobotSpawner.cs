using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>Spawns robots along the bottom of the wall at the night's interval.</summary>
    public sealed class RobotSpawner : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private RobotController robotPrefab;
        [SerializeField] private Transform container;
        [SerializeField] private float minX = -2.5f;
        [SerializeField] private float maxX = 2.5f;

        private float _timer;

        private void Update()
        {
            var night = session.Night;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = night.SpawnInterval;

            var pos = new Vector3(Random.Range(minX, maxX), transform.position.y, 0f);
            var robot = Instantiate(robotPrefab, pos, Quaternion.identity, container);
            robot.Initialize(session, night.Robot, night.Wall != null ? night.Wall.ClimbSpeedMultiplier : 1f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var y = transform.position.y;
            Gizmos.DrawLine(new Vector3(minX, y), new Vector3(maxX, y));
        }
    }
}
