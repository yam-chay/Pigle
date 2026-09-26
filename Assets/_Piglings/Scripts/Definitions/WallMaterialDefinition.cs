using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>Straw / wood / brick. Changes how balls bounce off the wall and its holds.</summary>
    [CreateAssetMenu(menuName = "Piglings/Wall Material Definition", fileName = "Wall_")]
    public sealed class WallMaterialDefinition : ScriptableObject
    {
        [SerializeField] private string id = "wood";
        [SerializeField] private PhysicsMaterial2D holdMaterial;
        [SerializeField, Min(0f)] private float climbSpeedMultiplier = 1f;

        public string Id => id;
        public PhysicsMaterial2D HoldMaterial => holdMaterial;
        public float ClimbSpeedMultiplier => climbSpeedMultiplier;
    }
}
