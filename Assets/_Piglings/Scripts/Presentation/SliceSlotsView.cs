using System.Collections.Generic;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Slice placement's look (M10.F, the Tower frame): every slice of the tower gets a faint frame so it reads as a slot,
    /// the hovered one a stronger pulsing one, and a swapped slice flashes. Shown only in the Tower state (SlicePicker
    /// reads the input; this only draws). The frame sprite is any white 9-sliced rounded rect (ui_round_rect).
    /// </summary>
    public sealed class SliceSlotsView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private NightFlow flow;
        [SerializeField] private SlicePicker picker;
        [Tooltip("A white, 9-sliced rounded rect (ui_round_rect): drawn Sliced at each slice's size.")]
        [SerializeField] private Sprite frame;
        [SerializeField] private Color colour = new Color(1f, 0.95f, 0.8f, 1f);
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.15f;
        [SerializeField, Range(0f, 1f)] private float hoverAlpha = 0.6f;
        [SerializeField, Min(0f)] private float pulsesPerSecond = 1.5f;
        [Tooltip("The slot's size: the slice's width (the barn is 6) and a small inset from its height.")]
        [SerializeField, Min(0.1f)] private float width = 5.2f;
        [SerializeField, Min(0f)] private float inset = 0.06f;
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 50;
        [Tooltip("Seconds a swapped slice's flash lasts.")]
        [SerializeField, Min(0.01f)] private float flashSeconds = 0.35f;

        private readonly List<SpriteRenderer> _slots = new List<SpriteRenderer>();
        private readonly List<float> _flash = new List<float>();   // Time.time a slot was swapped; -inf = never

        private void OnEnable() { if (picker != null) picker.SliceChanged += OnSliceChanged; }
        private void OnDisable() { if (picker != null) picker.SliceChanged -= OnSliceChanged; }

        private void Start()
        {
            var tower = session.Tower;
            if (tower == null || frame == null) return;
            for (int i = 0; i < session.TowerSlices.Count; i++)
            {
                var go = new GameObject($"Slot {i + 1}");
                go.transform.SetParent(transform, worldPositionStays: false);
                float h = tower.SliceHeight - inset * 2f;
                go.transform.position = new Vector3(tower.CentreX, tower.SliceBottom(i) + tower.SliceHeight / 2f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frame;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(width, h);
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
                sr.enabled = false;
                _slots.Add(sr);
                _flash.Add(float.NegativeInfinity);
            }
        }

        private void OnSliceChanged(int index) { if (index >= 0 && index < _flash.Count) _flash[index] = Time.time; }

        private void LateUpdate()
        {
            bool show = flow != null && flow.State == FlowState.Tower && !flow.CameraMoving;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * pulsesPerSecond * Mathf.PI * 2f);
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].enabled = show;
                if (!show) continue;
                float a = picker != null && picker.HoveredSlice == i ? hoverAlpha * pulse : idleAlpha;
                float flash = Mathf.Clamp01(1f - (Time.time - _flash[i]) / flashSeconds);
                var c = Color.Lerp(colour, Color.white, flash);
                c.a *= Mathf.Max(a, flash);
                _slots[i].color = c;
            }
        }
    }
}
