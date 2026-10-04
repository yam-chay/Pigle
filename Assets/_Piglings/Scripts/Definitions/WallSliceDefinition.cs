using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>What a slice does on top of being a wall. Only the data slot for now: wood / straw / brick come later.</summary>
    public enum SliceEffect { None }

    /// <summary>
    /// One 1.6-unit slice of the tower (GDD: the barn is built from slices, bottom → top). A night lists its slices;
    /// TowerBuilder stacks them and puts a Hold at each layout point. The default layout is the one the original scene's
    /// slices use, so a new slice asset only needs its sprite.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Wall Slice Definition", fileName = "Slice_")]
    public sealed class WallSliceDefinition : ScriptableObject
    {
        [Tooltip("Save key: a player's tower choices are saved by this id. Never rename it once players have a save.")]
        [SerializeField] private string id = "barn";
        [Tooltip("The slice's art (same size and pivot as barn_slice.png).")]
        [SerializeField] private Sprite sprite;
        [Tooltip("Hold positions inside the slice (local units from its pivot). Every slice needs the same count (7): a night's " +
                 "socket count is slices × holds, fixed before it starts.")]
        [SerializeField] private Vector2[] holds =
        {
            new Vector2(-1.1f, 1.12f), new Vector2(0f, 1.12f), new Vector2(1.1f, 1.12f),
            new Vector2(-1.7f, 0.54f), new Vector2(-0.55f, 0.54f), new Vector2(0.55f, 0.54f), new Vector2(1.7f, 0.54f),
        };
        [Tooltip("The holds' physics material (how balls bounce off this slice). Empty = the Hold prefab's own.")]
        [SerializeField] private PhysicsMaterial2D holdMaterial;
        [Tooltip("Not used yet: wood / straw / brick effects come later.")]
        [SerializeField] private SliceEffect effect = SliceEffect.None;

        public string Id => id;
        public Sprite Sprite => sprite;
        public Vector2[] Holds => holds;
        public PhysicsMaterial2D HoldMaterial => holdMaterial;
        public SliceEffect Effect => effect;
    }
}
