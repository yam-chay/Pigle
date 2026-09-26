using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// הולדת עכבישים אדומים לאורך המשחק - GDD 7.2, 8.4
public class SpiderSpawner : MonoBehaviour
{
    // אזור הולדה מלבני. המיקום נקבע ע"י אובייקט עוגן בסצנה, והגודל ע"י שדה size.
    //
    // המצלמה מביטה על פני המגדל, ולא עליו מהצד. העכבישים מטפסים ישר כלפי מעלה
    // ומתפזרים על פני רוחב המגדל, ולכן די באזור אחד שנפרש על הרוחב בתחתית.
    // המערך נשמר כדי לאפשר בהמשך גיוון, למשל אזור נוסף בקצב הולדה אחר.
    [Serializable]
    public class SpawnArea
    {
        [Tooltip("שם לזיהוי בלבד, לא משפיע על הלוגיקה")]
        public string label = "Wall";

        [Tooltip("אובייקט עוגן בסצנה שמיקומו הוא מרכז האזור")]
        public Transform anchor;

        [Tooltip("רוחב וגובה האזור ביחידות עולם")]
        public Vector2 size = new Vector2(0.5f, 1f);

        [Tooltip("האם עכביש שנולד כאן מוצג הפוך (דופן ימנית)")]
        public bool spriteFacesLeft;

        [Tooltip("משקל יחסי בהגרלה בין האזורים. אם כל האזורים באפס, כולם מקבלים משקל שווה")]
        [Min(0f)] public float weight = 1f;

        public Color gizmoColor = new Color(1f, 0.35f, 0.35f, 0.35f);

        // תקינות נקבעת לפי העוגן בלבד. משקל אפס אינו מנטרל אזור, כי יוניטי מאפסת
        // איברי מערך שנוצרים בעורך ואינה מחילה עליהם את ברירת המחדל שלמעלה
        public bool IsValid => anchor != null;

        public Vector2 RandomPoint()
        {
            Vector2 center = anchor.position;
            Vector2 half = size * 0.5f;
            return new Vector2(
                center.x + UnityEngine.Random.Range(-half.x, half.x),
                center.y + UnityEngine.Random.Range(-half.y, half.y));
        }
    }

    [Header("Prefab & Parent")]
    [SerializeField] private GameObject spiderPrefab;
    [SerializeField] private Transform spidersContainer;

    [Header("Tweaking - GDD 8.4")]
    [SerializeField] private float spiderClimbSpeed = 1f;
    [SerializeField] private float spawnInterval = 2f;

    [Header("Spawn Areas")]
    [Tooltip("אזורי ההולדה בתחתית דפנות המגדל. אם ריק - העכבישים ייווצרו במיקום ה-Spawner עצמו")]
    [SerializeField] private SpawnArea[] spawnAreas;

    [Header("Spacing")]
    [Tooltip("מרחק מינימלי מעכביש חי אחר בעת ההולדה. אפס מבטל את הבדיקה")]
    [Min(0f)] [SerializeField] private float minSpawnSeparation = 0.6f;

    [Tooltip("כמה מיקומים להגריל בחיפוש מקום פנוי. אם לא נמצא מקום מספיק רחוק, נבחר הרחוק ביותר מבין הניסיונות")]
    [Min(1)] [SerializeField] private int maxSpawnAttempts = 30;

    [Header("Flow")]
    [SerializeField] private bool spawnOnStart = true;

    private Coroutine _spawnLoop;

    // רשימה מתוחזקת של העכבישים שנולדו, במקום סריקת סצנה מלאה בכל ניסיון הגרלה
    private readonly List<SpiderController> _spawned = new List<SpiderController>();

    public float ClimbSpeed => spiderClimbSpeed;

    private void Start()
    {
        if (spawnOnStart) StartSpawning();
    }

    public void StartSpawning()
    {
        if (_spawnLoop != null) return;
        _spawnLoop = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (_spawnLoop == null) return;
        StopCoroutine(_spawnLoop);
        _spawnLoop = null;
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            // אותו מכפיל שמאיץ את הטיפוס מקצר כאן את ההמתנה, כדי שמספר
            // העכבישים על המסך יישאר זהה. בלי זה האצת הטיפוס דווקא מרוקנת
            // את הדופן: הם מתפנים מהר יותר אבל נולדים באותו קצב
            float multiplier = GameManager.Instance != null
                ? GameManager.Instance.SpiderSpeedMultiplier
                : 1f;
            if (multiplier <= 0f) multiplier = 1f;

            yield return new WaitForSeconds(Mathf.Max(0.05f, spawnInterval / multiplier));
            SpawnSpider();
        }
    }

    public SpiderController SpawnSpider()
    {
        if (spiderPrefab == null)
        {
            Debug.LogError("[SpiderSpawner] spiderPrefab לא מחווט", this);
            return null;
        }

        PruneSpawned();

        SpawnArea area;
        Vector3 position = FindSpawnPosition(out area);
        bool facesLeft = area != null && area.spriteFacesLeft;

        GameObject spiderObject = Instantiate(spiderPrefab, position, Quaternion.identity, spidersContainer);

        SpiderController controller = spiderObject.GetComponent<SpiderController>();
        if (controller == null)
        {
            Debug.LogError("[SpiderSpawner] לפריפאב העכביש אין SpiderController", spiderObject);
            return null;
        }

        controller.Initialize(SpiderType.Red, spiderClimbSpeed, facesLeft);
        _spawned.Add(controller);
        return controller;
    }

    // הגרלת מיקום שאינו צמוד לעכביש חי אחר.
    // לא מגרילים עד שמצליחים, אלא מנסים מספר קצוב ושומרים את המועמד הרחוק ביותר -
    // כך שגם באזור צפוף ההולדה מצליחה, במיקום הפחות גרוע, במקום להיתקע
    private Vector3 FindSpawnPosition(out SpawnArea chosenArea)
    {
        chosenArea = PickArea();
        if (chosenArea == null) return transform.position;

        Vector2 best = chosenArea.RandomPoint();
        if (minSpawnSeparation <= 0f) return best;

        float bestDistance = MinDistanceToAliveSpider(best);

        for (int i = 1; i < maxSpawnAttempts; i++)
        {
            if (bestDistance >= minSpawnSeparation) break;

            SpawnArea area = PickArea();
            if (area == null) break;

            Vector2 candidate = area.RandomPoint();
            float distance = MinDistanceToAliveSpider(candidate);
            if (distance <= bestDistance) continue;

            best = candidate;
            bestDistance = distance;
            chosenArea = area;
        }

        return best;
    }

    private float MinDistanceToAliveSpider(Vector2 position)
    {
        float min = float.MaxValue;
        for (int i = 0; i < _spawned.Count; i++)
        {
            SpiderController spider = _spawned[i];
            if (spider == null || !spider.IsAlive) continue;

            float distance = Vector2.Distance(position, spider.transform.position);
            if (distance < min) min = distance;
        }
        return min;
    }

    // הסרת עכבישים שכבר הושמדו או מתו, כדי שהרשימה לא תתפח ולא תשפיע על ההגרלה
    private void PruneSpawned()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            SpiderController spider = _spawned[i];
            if (spider == null || !spider.IsAlive) _spawned.RemoveAt(i);
        }
    }

    // הגרלה משוקללת בין האזורים התקינים
    private SpawnArea PickArea()
    {
        if (spawnAreas == null || spawnAreas.Length == 0) return null;

        float totalWeight = 0f;
        int validCount = 0;
        for (int i = 0; i < spawnAreas.Length; i++)
        {
            if (spawnAreas[i] == null || !spawnAreas[i].IsValid) continue;
            totalWeight += spawnAreas[i].weight;
            validCount++;
        }

        if (validCount == 0) return null;

        // כל המשקלים באפס - מתייחסים לכולם כשווים, במקום לא להגריל כלום
        bool uniform = totalWeight <= 0f;
        if (uniform) totalWeight = validCount;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < spawnAreas.Length; i++)
        {
            SpawnArea area = spawnAreas[i];
            if (area == null || !area.IsValid) continue;

            roll -= uniform ? 1f : area.weight;
            if (roll <= 0f) return area;
        }

        return null;
    }

    // ציור האזורים בחלון הסצנה כדי שיהיו ניתנים לכוונון ויזואלי
    private void OnDrawGizmos()
    {
        if (spawnAreas == null) return;

        for (int i = 0; i < spawnAreas.Length; i++)
        {
            SpawnArea area = spawnAreas[i];
            if (area == null || area.anchor == null) continue;

            Vector3 center = area.anchor.position;
            Vector3 size = new Vector3(area.size.x, area.size.y, 0.01f);

            Gizmos.color = area.gizmoColor;
            Gizmos.DrawCube(center, size);

            Gizmos.color = new Color(area.gizmoColor.r, area.gizmoColor.g, area.gizmoColor.b, 1f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
