using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Keeps the game at one aspect ratio (M11.T8): the camera draws into the biggest centred 16:9 (Aspect) rectangle the
    /// window allows, with bars around it. Why: a browser window (itch's fullscreen button, a resized tab) can be any
    /// shape, and the camera framing, the tower and the wall are tuned for one — a wider window would show past the
    /// barn's sides. On the main camera, in every scene that has one (the title too).
    ///
    /// The bars need something to clear them: URP leaves the area outside a camera's rect untouched (garbage in WebGL), so
    /// this makes a second camera at start that draws nothing but Bar Colour, behind the main one. Pointer input stays
    /// right: ScreenToWorldPoint accounts for the rect. A Screen Space - Overlay canvas still covers the whole window
    /// (bars included); a canvas that should stay inside the picture uses Screen Space - Camera with this camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FixedAspect : MonoBehaviour
    {
        [Tooltip("Width : height of the picture. 16:9 by default — the Player Settings' WebGL resolution should match.")]
        [SerializeField] private Vector2 aspect = new Vector2(16f, 9f);
        [SerializeField] private Color barColour = Color.black;

        private Camera _camera;
        private Camera _bars;
        private int _width, _height;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            var go = new GameObject("FixedAspect bars");
            _bars = go.AddComponent<Camera>();
            _bars.depth = _camera.depth - 1;   // first: the main camera draws over it, inside its rect
            _bars.clearFlags = CameraClearFlags.SolidColor;
            _bars.backgroundColor = barColour;
            _bars.cullingMask = 0;
            _bars.orthographic = true;
            Apply();
        }

        private void OnDestroy()
        {
            if (_bars != null) Destroy(_bars.gameObject);
        }

        // Every frame, cheaply: only a changed window size does anything.
        private void LateUpdate()
        {
            if (Screen.width != _width || Screen.height != _height) Apply();
        }

        private void Apply()
        {
            _width = Screen.width;
            _height = Screen.height;
            if (_width <= 0 || _height <= 0 || aspect.x <= 0f || aspect.y <= 0f) return;
            float target = aspect.x / aspect.y;
            float window = (float)_width / _height;
            // Wider than the target: bars left and right. Taller: bars top and bottom.
            _camera.rect = window > target
                ? new Rect((1f - target / window) / 2f, 0f, target / window, 1f)
                : new Rect(0f, (1f - window / target) / 2f, 1f, window / target);
        }
    }
}
