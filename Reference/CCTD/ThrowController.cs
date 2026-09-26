using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// טיפול בקלט הזריקה (גרירה ושחרור), כיוון, מלאי הביצים ויצירתן - GDD 3.1, 3.1.1, 7.2
//
// הקלט נקרא ישירות מ-Pointer.current ולא דרך Input Actions asset. לפעולה
// יחידה זה פשוט יותר, ו-Pointer מכסה גם עכבר וגם מגע באותו קוד.
public class ThrowController : MonoBehaviour
{
    public static ThrowController Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private GameObject eggPrefab;
    [SerializeField] private Transform eggSpawnPoint;
    [SerializeField] private Transform eggsContainer;

    [Header("Throw - GDD 3.1")]
    [Tooltip("כוח הזריקה. קבוע ואינו נשלט ע\"י השחקן")]
    [SerializeField] private float eggThrowForce = 10f;

    [Header("Inventory - GDD 3.1, 3.1.1")]
    [Tooltip("כמה ביצים במלאי. כולן מתחילות טעונות. במוד הבסיסי - 42")]
    [SerializeField] private int inventorySize = 42;

    [Tooltip("כמה הריגות עכביש נדרשות כדי לטעון ביצה מחדש.\n\n" +
             "אפס = המוד הבסיסי (GDD 3.1): אין טעינה כלל, ביצה שנזרקה נגמרה, " +
             "והריצה נגמרת כשנגמרו הביצים.\n" +
             "גדול מאפס = מוד הטעינה (GDD 3.1.1): מלאי קטן שחוזר אל עצמו.")]
    [SerializeField] private int killsPerReload = 0;

    [Tooltip("גיבוי למבוי סתום במוד הטעינה בלבד: כשאף ביצה אינה טעונה, כל פרק " +
             "זמן כזה מתקדם צעד טעינה אחד לכל הביצים - בדיוק כמו הריגה")]
    [SerializeField] private float emptyReloadSeconds = 3f;

    [Tooltip("רדיוס הבדיקה שהיד פנויה. ביצה חדשה לא תיווצר כל עוד ביצה אחרת " +
             "עדיין נמצאת בתחום הזה, אחרת השתיים נוצרות חופפות ומתפוצצות זו מזו")]
    [SerializeField] private float handClearRadius = 0.35f;

    [Tooltip("המתנה בשניות מרגע הזריקה ועד שהביצה הבאה מוצגת ביד. נותנת פעימה " +
             "בין זריקה לזריקה במקום שהבאה תקפוץ מיד למקום שהתפנה. אפס מבטל.\n\n" +
             "זהו רף תחתון בלבד: אם היד עדיין לא פנויה כשההמתנה נגמרת, " +
             "הביצה תחכה עוד")]
    [SerializeField] private float nextEggDelay = 0.5f;

    [Header("Aiming - GDD 3.1")]
    [Tooltip("פותר את זווית השיגור כך שהביצה תעבור דרך נקודת הלחיצה.\n\n" +
             "המהירות נשארת קבועה - רק הזווית משתנה - ולכן זה נאמן ל-GDD 3.1 " +
             "(\"כוח קבוע, לא נשלט ע\"י השחקן\") וגם שומר על הקשת.\n\n" +
             "כבוי = הזריקה הישרה הישנה, שמחטיאה מתחת לנקודה ככל שהמטרה רחוקה")]
    [SerializeField] private bool aimBallistic = true;

    [Header("Trajectory preview")]
    [SerializeField] private TrajectoryView trajectoryView;

    [Tooltip("ציור המסלול החזוי. שימושי בעיקר בעורך - במגע אמיתי אין ריחוף, " +
             "ולכן על מובייל הקו ייראה רק בזמן הנגיעה עצמה")]
    [SerializeField] private bool showTrajectory = true;

    [Tooltip("אורך הקו בשניות כשהמטרה מחוץ לטווח ואין נקודת חיתוך לחתוך בה")]
    [SerializeField] private float trajectoryFallbackSeconds = 1.2f;

    private EggInventory _inventory;
    private float _emptyTimer;

    // נותר להמתנה עד הצגת הביצה הבאה. נדרך ברגע הזריקה
    private float _nextEggTimer;

    // הפוזה שכבר נשלחה לתרנגול, כדי לא לשלוח אותה בכל פריים
    private bool _poseHolding;

    // הביצה שנמצאת כרגע ביד התרנגול. קיימת בעולם, נראית, וניתנת לפגיעה
    // ע"י ביצה אחרת שנזרקה - GDD 3.1, 8.2
    private GameObject _heldEgg;

    public EggInventory Inventory => _inventory;
    public bool HasHeldEgg => _heldEgg != null;

    private void Awake()
    {
        Instance = this;
        _inventory = new EggInventory(inventorySize, killsPerReload);
    }

    private void Update()
    {
        TickEmptyFallback();
        UpdateHeldEgg();
        SyncChickenPose();
        UpdateTrajectory();
        ReadThrowInput();
    }

    // הכנף מורמת כל עוד יש ביצה ביד. נשלח רק כשהמצב משתנה, ולא בכל פריים
    private void SyncChickenPose()
    {
        bool holding = _heldEgg != null;
        if (holding == _poseHolding) return;

        _poseHolding = holding;
        if (ChickenController.Instance != null) ChickenController.Instance.SetHoldingEgg(holding);
    }

    // ---------- הביצה שביד ----------

    // ברגע שיש ביצה מוכנה במלאי, היא מוצגת ביד. אין יצירה ברגע הזריקה:
    // הביצה כבר קיימת, והזריקה רק משחררת אותה - כמו בפרויקט הישן
    private void UpdateHeldEgg()
    {
        // התרנגול מת: הביצה שביד נושרת, ואין יותר ביצים חדשות.
        //
        // הבדיקה היא על **מצב התרנגול** ולא על IsGameOver, וזה ההבדל שחשוב:
        // המשחק נגמר רק כשהגופה נוגעת באזור הסכנה, ובין המוות לבין הרגע הזה
        // עוברת נפילה שלמה. בדיקה מול IsGameOver השאירה ביצה תלויה באוויר
        // והולידה חדשות ביד של תרנגול שכבר אינו שם.
        if (!IsChickenAlive())
        {
            DropHeldEgg();
            return;
        }

        // הביצה נוצרת פעם אחת בנקודת האחיזה ולא מוזזת לשם בכל פריים.
        //
        // ההצמדה המחזורית שהייתה כאן קודם דרסה את הפיזיקה: ביצה זרוקה שפגעה בה
        // הזיזה אותה במעט, והשורה הבאה החזירה אותה מיד למקום. התוצאה נראתה
        // כאילו הביצים כלל אינן נוגעות. נקודת האחיזה קבועה, אז אין צורך לעדכן.
        if (_heldEgg != null) return;

        // פעימה קצרה אחרי הזריקה, לפני שהבאה מוצגת. בלעדיה הביצה הבאה מופיעה
        // באותו פריים שבו היד התפנתה, והרצף מרגיש כמו מקלע ולא כמו זריקה
        if (_nextEggTimer > 0f)
        {
            _nextEggTimer -= Time.deltaTime;
            return;
        }

        if (_inventory == null || !_inventory.HasReady) return;
        if (eggPrefab == null || eggSpawnPoint == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        // היד חייבת להתפנות קודם. בלי זה הביצה החדשה נוצרת בדיוק על הזרוקה
        // שעוד לא הספיקה להתרחק, השתיים חופפות, והפיזיקה מפוצצת אותן זו מזו
        if (!IsHandClear()) return;

        _heldEgg = Instantiate(eggPrefab, eggSpawnPoint.position, Quaternion.identity, eggsContainer);

        // נשארת Kinematic "ביד". ביצה דינמית שנזרקה כן מתנגשת בה - GDD 8.2
        var rb = _heldEgg.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    // ---------- טעינה ----------

    // נקראת ע"י InteractionsManager על כל הריגת עכביש - GDD 3.1.1
    public void RegisterKill()
    {
        if (_inventory == null) return;
        _inventory.AdvanceReload();
    }

    // הגיבוי פועל אך ורק כשאין שום ביצה במשחק: לא ביד, לא במלאי, ולא על המסך.
    // כל עוד יש ביצה שעפה, היא עוד עשויה להרוג עכביש ולקדם טעינה - אין מבוי סתום,
    // ואין סיבה שהטיימר יתערב.
    //
    // במוד הבסיסי אין טעינה כלל, ולכן אין גם מבוי סתום למנוע: מלאי ריק שם פירושו
    // שהריצה נגמרה, לא שנתקענו. יוצאים מוקדם כדי לא לסרוק את הסצנה לחינם בכל פריים.
    private void TickEmptyFallback()
    {
        if (_inventory == null || !_inventory.ReloadsEnabled)
        {
            _emptyTimer = 0f;
            return;
        }

        if (_inventory.HasReady || _heldEgg != null || AnyEggOnScreen())
        {
            _emptyTimer = 0f;
            return;
        }

        _emptyTimer += Time.deltaTime;
        if (_emptyTimer < emptyReloadSeconds) return;

        _emptyTimer = 0f;
        _inventory.AdvanceReload();
    }

    private bool AnyEggOnScreen()
    {
        return FindAnyObjectByType<EggController>() != null;
    }

    // האם אין ביצה אחרת בתחום נקודת האחיזה
    private bool IsHandClear()
    {
        var hits = Physics2D.OverlapCircleAll(eggSpawnPoint.position, handClearRadius);
        foreach (var h in hits)
        {
            if (h == null || h.isTrigger) continue;
            if (h.GetComponentInParent<EggController>() != null) return false;
        }

        return true;
    }

    // ---------- זריקה ----------

    // גרירה ושחרור: מחזיקים ומכוונים, ומשחררים כדי לזרוק. הזריקה יוצאת אל
    // הנקודה שבה האצבע עזבה - החלטת שפי, 24.8.26.
    //
    // מחליף את ה-Tap שהיה כאן וש-GDD 3.1 עדיין מתאר. הסיבה: בלי שלב כיוון
    // אין רגע שבו קו המסלול מספיק לעזור לשחקן, והוא נשאר עזר פיתוח בלבד.
    // נגיעה קצרה עדיין עובדת בדיוק כמו קודם - היא פשוט לחיצה ושחרור מיידי.
    private void ReadThrowInput()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasReleasedThisFrame) return;

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (_heldEgg == null) return;
        if (IsPointerOverUI()) return;

        Vector2 screenPos = pointer.position.ReadValue();
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(screenPos);
        Throw(new Vector2(world.x, world.y));
    }

    // שחרור הביצה שביד: מעבר ל-Dynamic ודחף לכיוון הלחיצה, בכוח קבוע - GDD 3.1
    private void Throw(Vector2 targetPos)
    {
        if (_heldEgg == null) return;
        if (!_inventory.TryTakeEgg()) return;

        var rb = _heldEgg.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.angularVelocity = 0f;

            // מהירות במקום AddForce: זו בדיוק המהירות שהפותר החזיר, ואין
            // צורך לתרגם אותה חזרה לכוח ובחזרה. עם מסה 1 התוצאה זהה לקודם
            rb.linearVelocity = SolveLaunchVelocity(_heldEgg.transform.position, targetPos, rb, out _);
        }

        _heldEgg = null;
        _nextEggTimer = nextEggDelay;

        if (trajectoryView != null) trajectoryView.Hide();
        if (GameplayInfo.Instance != null) GameplayInfo.Instance.RegisterEggThrown();
    }

    private bool IsChickenAlive()
    {
        return ChickenController.Instance == null || ChickenController.Instance.IsAlive;
    }

    // שחרור הביצה שביד בלי לזרוק אותה: היא עוברת ל-Dynamic ופשוט נופלת.
    // מתאים לרגע שבו התרנגול מת - הביצה נשמטת מהכנף במקום להיעלם.
    private void DropHeldEgg()
    {
        if (_heldEgg == null) return;

        var rb = _heldEgg.GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;

        _heldEgg = null;
        if (trajectoryView != null) trajectoryView.Hide();
    }

    // האם המצביע נמצא כרגע מעל אלמנט ממשק.
    //
    // נחוץ כי הקלט נקרא ישירות מ-Pointer ואינו עובר דרך ה-EventSystem, ולכן
    // בלי הבדיקה הזו לחיצה על כפתור ההאצה הייתה גם זורקת ביצה.
    //
    // תלוי בכך שרק אלמנטים שאמורים לקבל לחיצה יסומנו Raycast Target -
    // פאנל שקוף שפרוש על כל המסך ומסומן כך יחסום את כל הזריקות.
    private bool IsPointerOverUI()
    {
        EventSystem es = EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    // ---------- כוונון ----------

    // המהירות שהביצה משוגרת בה. שני המצבים חולקים את אותו גודל מהירות,
    // ונבדלים רק בכיוון: הישן מכוון ישר אל הלחיצה, החדש פותר את הזווית
    // שתעביר את הביצה דרכה בפועל
    private Vector2 SolveLaunchVelocity(Vector2 from, Vector2 target, Rigidbody2D rb, out bool inRange)
    {
        float speed = eggThrowForce / Mathf.Max(0.0001f, rb.mass);
        float gravity = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;

        if (!aimBallistic)
        {
            inRange = true;
            Vector2 d = target - from;
            return (d.sqrMagnitude > 0f ? d.normalized : Vector2.up) * speed;
        }

        inRange = ThrowSolver.TrySolve(from, target, speed, gravity, out Vector2 velocity);
        return velocity;
    }

    // ---------- ציור המסלול ----------

    private void UpdateTrajectory()
    {
        if (trajectoryView == null) return;

        if (!showTrajectory || _heldEgg == null)
        {
            trajectoryView.Hide();
            return;
        }

        var pointer = Pointer.current;
        Camera cam = Camera.main;
        var rb = _heldEgg.GetComponent<Rigidbody2D>();
        if (pointer == null || cam == null || rb == null)
        {
            trajectoryView.Hide();
            return;
        }

        // הקו מופיע רק בזמן הגרירה, כלומר בדיוק כשהשחקן מכוון.
        // זה גם מה שיקרה במגע אמיתי, ולכן העורך מראה את ההתנהגות האמיתית
        // ולא גרסה נוחה יותר שקיימת רק כאן
        if (!pointer.press.isPressed || IsPointerOverUI())
        {
            trajectoryView.Hide();
            return;
        }

        Vector3 world = cam.ScreenToWorldPoint(pointer.position.ReadValue());
        Vector2 target = new Vector2(world.x, world.y);
        Vector2 from = _heldEgg.transform.position;

        Vector2 velocity = SolveLaunchVelocity(from, target, rb, out bool inRange);
        float gravity = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;

        // בטווח - נחתך בדיוק בנקודת המטרה, כדי שרואים איפה הביצה תהיה.
        // מחוץ לטווח אין נקודת חיתוך, ואז מציירים אורך קבוע
        float duration = inRange
            ? ThrowSolver.TimeToCross(velocity, target.x - from.x)
            : trajectoryFallbackSeconds;

        trajectoryView.Show(from, velocity, gravity, duration, inRange);
    }
}
