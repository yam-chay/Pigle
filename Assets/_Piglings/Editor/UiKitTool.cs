using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Piglings.EditorTools
{
    /// <summary>
    /// Puts the UI kit (Art/UI) to work, from the menu Piglings ▸ UI:
    ///  1. "Set 9-slice borders" writes each kit sprite's slice borders (from PROTOTYPE_V2.md's art map) into its import
    ///     settings. Without them a button or panel stretches the whole image — corners turn into ovals.
    ///  2. "Style selected as …" turns the selected Images (a Button's, a panel's) into the kit's primary / secondary
    ///     button or round-rect panel: the sprite, Sliced, and corners scaled to fit small rects. Undo works.
    /// Editor-only; changes import settings (.meta) and the scene, which get committed like any editor change.
    /// </summary>
    public static class UiKitTool
    {
        private const string UiFolder = "Assets/_Piglings/Art/UI";

        // Slice borders in pixels, as Unity stores them: x = left, y = bottom, z = right, w = top.
        private static readonly (string name, Vector4 border)[] Borders =
        {
            ("board_frame", new Vector4(36, 52, 36, 36)),
            ("ui_button_primary", new Vector4(32, 40, 32, 32)),
            ("ui_button_secondary", new Vector4(32, 40, 32, 32)),
            ("ui_round_rect", new Vector4(20, 20, 20, 20)),
            ("ui_pill", new Vector4(12, 12, 12, 12)),
        };

        [MenuItem("Piglings/UI/1. Set 9-slice borders on the UI kit")]
        private static void SetBorders()
        {
            int done = 0;
            foreach (var (name, border) in Borders)
            {
                string path = $"{UiFolder}/{name}.png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogWarning($"UI kit: {path} not found — skipped.");
                    continue;
                }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                // A 9-sliced sprite needs a full-rect mesh (a tight mesh can't be stretched by its borders).
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.spriteBorder = border;
                importer.SaveAndReimport();
                done++;
            }
            Debug.Log($"UI kit: 9-slice borders set on {done} sprites (board_frame, buttons, round rect, pill). Commit the changed .meta files.");
        }

        [MenuItem("Piglings/UI/2. Style selected as Primary button")]
        private static void Primary() => Style("ui_button_primary");

        [MenuItem("Piglings/UI/2. Style selected as Secondary button")]
        private static void Secondary() => Style("ui_button_secondary");

        [MenuItem("Piglings/UI/2. Style selected as Panel (round rect)")]
        private static void Panel() => Style("ui_round_rect");

        [MenuItem("Piglings/UI/2. Style selected as Primary button", true)]
        [MenuItem("Piglings/UI/2. Style selected as Secondary button", true)]
        [MenuItem("Piglings/UI/2. Style selected as Panel (round rect)", true)]
        private static bool HasImageSelected()
        {
            foreach (var go in Selection.gameObjects) if (go.GetComponent<Image>() != null) return true;
            return false;
        }

        private static void Style(string spriteName)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/{spriteName}.png");
            if (sprite == null) { Debug.LogWarning($"UI kit: {spriteName} not found in {UiFolder}."); return; }
            if (sprite.border == Vector4.zero)
                Debug.LogWarning($"UI kit: {spriteName} has no 9-slice borders yet — run \"1. Set 9-slice borders\" first, then style again.");

            int done = 0;
            foreach (var go in Selection.gameObjects)
            {
                var image = go.GetComponent<Image>();
                if (image == null) continue;
                Undo.RecordObject(image, $"Style {spriteName}");
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                image.color = Color.white;
                image.pixelsPerUnitMultiplier = FitCorners(image, sprite);
                EditorUtility.SetDirty(image);
                done++;
            }
            Debug.Log($"UI kit: {done} selected Image(s) styled as {spriteName}.");
        }

        // On a small rect the corners would overlap: scale the slices down until they take at most 70% of each side.
        private static float FitCorners(Image image, Sprite sprite)
        {
            var canvas = image.GetComponentInParent<Canvas>();
            float refPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            float unitsPerPixel = refPpu / sprite.pixelsPerUnit;
            var rect = image.rectTransform.rect;
            float across = (sprite.border.x + sprite.border.z) * unitsPerPixel;
            float tall = (sprite.border.y + sprite.border.w) * unitsPerPixel;
            float m = 1f;
            if (rect.width > 0f) m = Mathf.Max(m, across / (0.7f * rect.width));
            if (rect.height > 0f) m = Mathf.Max(m, tall / (0.7f * rect.height));
            return m;
        }
    }
}
