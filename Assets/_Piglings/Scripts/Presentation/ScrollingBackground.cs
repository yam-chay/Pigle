using UnityEngine;

namespace Piglings.Presentation
{
    // Scrolls a tiled background forever. The pattern repeats every tile,
    // so after moving one full tile we can jump back by exactly one tile
    // and nobody can tell — that's what makes it endless.
    [RequireComponent(typeof(SpriteRenderer))]
    public class ScrollingBackground : MonoBehaviour
    {
        [SerializeField] Vector2 direction = new Vector2(-1f, 0f); // (-1,0) = left, (1,0) = right, (0,1) = up, (1,1) = diagonal
        [SerializeField] float speed = 0.3f;                        // world units per second

        Vector3 startPosition;
        Vector2 tileSize;
        Vector2 offset;

        void Awake()
        {
            startPosition = transform.position;

            // One tile's size in world units = the texture's pixels / Pixels Per Unit, times the object's scale.
            Sprite sprite = GetComponent<SpriteRenderer>().sprite;
            Vector2 scale = transform.lossyScale;
            tileSize = sprite.rect.size / sprite.pixelsPerUnit * scale;
        }

        void Update()
        {
            offset += direction.normalized * speed * Time.deltaTime;

            // Keep the offset inside one tile; the wrap lands on an identical-looking spot.
            offset.x = Mathf.Repeat(offset.x, tileSize.x);
            offset.y = Mathf.Repeat(offset.y, tileSize.y);

            transform.position = startPosition + (Vector3)offset;
        }
    }
}