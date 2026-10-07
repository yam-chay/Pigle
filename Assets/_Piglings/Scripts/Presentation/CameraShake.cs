using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Screen shake for big moments (M4.3): deep hits and big chains add "trauma"; the camera shakes by
    /// trauma² so small bumps barely register and big chains really kick. Trauma drains over time.
    ///
    /// Why the base pose is restored early each frame: gameplay (ThrowController aiming) reads the camera
    /// in Update. This runs before it and puts the camera back where it belongs, then shakes it again in
    /// LateUpdate — so only the rendered picture shakes, never the aim.
    /// Put it on the camera. Reads events only; changes nothing but the camera's look.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        [Header("What shakes")]
        [Tooltip("Robots this deep or deeper shake the screen. 2 = hit by a ball that was itself hit by a ball.")]
        [SerializeField, Min(1)] private int minDepth = 2;
        [Tooltip("Trauma added per depth step at or past Min Depth (trauma is 0..1).")]
        [SerializeField, Range(0f, 1f)] private float traumaPerDepth = 0.25f;
        [SerializeField, Range(0f, 1f)] private float chainTrauma = 0.6f;
        [Tooltip("A Bomb going off adds this (its victims, falling deeper, add their own as they score).")]
        [SerializeField, Range(0f, 1f)] private float bombTrauma = 0.4f;

        [Header("How it shakes")]
        [SerializeField, Min(0f)] private float maxOffset = 0.15f;     // world units at full trauma
        [SerializeField, Min(0f)] private float maxAngle = 2f;         // degrees at full trauma
        [SerializeField, Min(0f)] private float frequency = 22f;       // noise samples per second
        [Tooltip("Trauma lost per second. 1.5 = a full shake settles in under a second.")]
        [SerializeField, Min(0.01f)] private float recovery = 1.5f;

        private float _trauma;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private bool _shaken;

        private void Start()
        {
            session.Bus.Subscribe<RobotLostGrip>(OnLostGrip);
            session.Bus.Subscribe<ChainScored>(OnChainScored);
            session.Bus.Subscribe<BombExploded>(OnBombExploded);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<RobotLostGrip>(OnLostGrip);
            session.Bus.Unsubscribe<ChainScored>(OnChainScored);
            session.Bus.Unsubscribe<BombExploded>(OnBombExploded);
        }

        // A deep knock shakes (M10.S: there are no per-robot points any more; the depth is the knock's own).
        private void OnLostGrip(RobotLostGrip e)
        {
            int depth = e.Cause.Depth;
            if (depth >= minDepth) AddTrauma(traumaPerDepth * (depth - minDepth + 1));
        }

        // How big a "big chain" is lives in the Visuals (shared with the chain popup), so the shake and the popup agree.
        private void OnChainScored(ChainScored e) => AddTrauma(chainTrauma * Mathf.Clamp01(e.Total / (float)session.Visuals.BigChainTotal));

        private void OnBombExploded(BombExploded e) => AddTrauma(bombTrauma);

        private void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        // Early (order -900): undo last frame's shake before anything reads the camera.
        private void Update()
        {
            if (!_shaken) return;
            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;
            _shaken = false;
        }

        private void LateUpdate()
        {
            _trauma = Mathf.Max(0f, _trauma - recovery * Time.deltaTime);
            if (_trauma <= 0f) return;

            // Remember the pose other scripts left this frame, then shake on top of it.
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;

            float shake = _trauma * _trauma;
            float t = Time.time * frequency;
            var offset = new Vector3(Noise(1f, t), Noise(2f, t), 0f) * (maxOffset * shake);
            float angle = Noise(3f, t) * maxAngle * shake;
            transform.localPosition = _basePosition + offset;
            transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, angle);
            _shaken = true;
        }

        // Smooth -1..1 noise; a different seed per axis so they don't move together.
        private static float Noise(float seed, float t) => (Mathf.PerlinNoise(seed * 10f, t) - 0.5f) * 2f;
    }
}
