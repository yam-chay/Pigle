using UnityEngine;

// לוגיקת העכביש, בנויה כמכונת מצבים - GDD 3.2, 3.3, 4.1, 8.2, 8.4
//
//   Spawned -> Ready -> Climbing --+-- ReachedTowerTop -> EnteredTower -> Removed
//                                  |
//                                  +-- Falling -> Removed
//
// המצב הוא מקור האמת היחיד, ו-EnterState הוא המקום היחיד שבו נגזרות ממנו
// התוצאות: סוג הגוף הפיזיקלי, האנימציה, והאינטראקציה עם שאר העכבישים.
// אין דגלים נלווים - כל שאלה על העכביש נענית מהמצב.
public class SpiderController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpiderView view;
    [SerializeField] private Collider2D bodyCollider;

    [Header("Death Physics - GDD 8.4")]
    [Tooltip("עוצמת הדחף האלכסוני בעת המוות")]
    [SerializeField] private float deathForce = 5f;

    [Tooltip("סטייה יחסית אקראית סביב עוצמת הדחף. 0.2 פירושו טווח של עשרים אחוז למעלה ולמטה")]
    [Range(0f, 1f)][SerializeField] private float deathForceVariance = 0.25f;

    [Tooltip("טווח הזווית במעלות בין הדחף לבין הכיוון האנכי. הצד נקבע לפי כיוון הפגיעה")]
    [SerializeField] private Vector2 deathAngleRange = new Vector2(25f, 55f);

    [Tooltip("טווח הסחרור האקראי בעת המוות")]
    [SerializeField] private Vector2 deathTorqueRange = new Vector2(-8f, 8f);

    [Tooltip("עוצמת הכבידה על העכביש המת")]
    [SerializeField] private float deathGravityScale = 1f;

    [Header("Death Collider")]
    // אנימציית המוות מכווצת את הספרייט בהדרגה: ההליכה היא 0.715 x 0.500,
    // והפריים האחרון של המוות הוא 0.356 x 0.355 - כמחצית הרוחב.
    // הקולידר עצמו אינו משתנה, ולכן על גופה הוא תופס 92% מהרוחב במקום 46%.
    [Tooltip("רדיוס הקולידר אחרי המוות. אפס משאיר את הרדיוס של החי")]
    [SerializeField] private float deathColliderRadius = 0.082f;

    // ה-offset נמדד במרחב מקומי, ולכן הוא מסתובב יחד עם הגופה המסוחררת:
    // נמדד בהרצה שגופות ב-179, 239 ו-318 מעלות נשאו את אותו offset בכיוונים שונים.
    [Tooltip("איפוס ה-offset במוות, כדי שלא ינדוד סביב הספרייט כשהגופה מסתחררת")]
    [SerializeField] private bool zeroColliderOffsetOnDeath = true;


    private readonly SpiderData _data = new SpiderData();
    private float _climbSpeed;
    private bool _facingLeft;
    private Vector2 _deathImpact;

    public SpiderData Data => _data;
    public SpiderState State => _data.State;
    public SpiderKiller KilledBy => _data.KilledBy;

    // מת = נהרג. עכביש שנכנס למגדל אינו "מת" - הוא יצא מהמשחק בדרך אחרת
    public bool IsDead => _data.State == SpiderState.Falling || _data.State == SpiderState.Removed;

    // נשמר בשם הזה כי מערכות אחרות כבר נשענות עליו (SpiderSpawner, ColliderDebugView)
    public bool IsAlive => !IsDead;

    // ניתן להריגה רק כל עוד הוא מטפס. עכביש שכבר נגע בראש המגדל מוגן,
    // כדי שאנימציית הגסיסה לא תקטע את אנימציית ההיעלמות ואיתה את רצף מות התרנגול
    public bool CanBeKilled => _data.State == SpiderState.Ready || _data.State == SpiderState.Climbing;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (view == null) view = GetComponent<SpiderView>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
    }

    // ---------- מעברי מצב ----------

    // נקראת ע"י SpiderSpawner מיד לאחר ההולדה
    public void Initialize(SpiderType type, float climbSpeed, bool facingLeft = false)
    {
        _data.Type = type;
        _data.KilledBy = SpiderKiller.None;
        _data.Position = transform.position;

        _climbSpeed = climbSpeed;
        _facingLeft = facingLeft;

        SetState(SpiderState.Ready);
        SetState(SpiderState.Climbing);
    }

    // העכביש נגע בראש המגדל. נקראת אך ורק ע"י InteractionsManager
    public void ReachTowerTop()
    {
        if (_data.State != SpiderState.Climbing && _data.State != SpiderState.Ready) return;
        SetState(SpiderState.ReachedTowerTop);
    }

    // העכביש נהרג. נקראת אך ורק ע"י InteractionsManager
    public void Kill(SpiderKiller killer, Vector2 impactPoint)
    {
        if (!CanBeKilled) return;

        _data.KilledBy = killer;
        _deathImpact = impactPoint;
        SetState(SpiderState.Falling);
    }

    // סימון לפני השמדה, כדי ששום מערכת לא תתייחס אליו יותר כפעיל
    public void MarkRemoved()
    {
        if (_data.State == SpiderState.Removed) return;
        SetState(SpiderState.Removed);
    }

    private void SetState(SpiderState next)
    {
        if (_data.State == next) return;

        _data.State = next;
        EnterState(next);
    }

    // המקום היחיד שבו מצב מתורגם לתוצאות בפועל
    private void EnterState(SpiderState state)
    {
        switch (state)
        {
            case SpiderState.Ready:
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                // אנימציית הטיפוס היא מצב ברירת המחדל בבקר ורצה בלולאה מעצמה,
                // לכן אין כאן הפעלת פרמטר - רק קביעת כיוון הפנייה
                if (view != null) view.SetFacingLeft(_facingLeft);

                break;

            case SpiderState.Climbing:
                break;

            case SpiderState.ReachedTowerTop:
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                if (view != null) view.PlayEnterTower();
                break;

            case SpiderState.EnteredTower:
                // הבקר רק מדווח; ההחלטה מה קורה מרוכזת ב-InteractionsManager - GDD 7.2
                if (InteractionsManager.Instance != null)
                {
                    InteractionsManager.Instance.HandleSpiderEnteredTower(this);
                }
                break;

            case SpiderState.Falling:
                ApplyDeathCollider();
                ApplyDeathPhysics();
                if (view != null) view.PlayDie();
                break;

            case SpiderState.Removed:
                break;
        }
    }

    private void Update()
    {
        // סוף אנימציית ההיעלמות. נבדק מצב האנימטור ולא זמן קצוב, כדי שאורך
        // הרצף יישאר נגזר מהאנימציה עצמה גם אם היא תשתנה
        if (_data.State == SpiderState.ReachedTowerTop && (view == null || view.IsGone))
        {
            SetState(SpiderState.EnteredTower);
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (_data.State == SpiderState.Climbing)
        {
            // המכפיל נקרא חי ולא נשמר ב-Initialize, אחרת לחיצה על כפתור
            // ההאצה לא הייתה מזיזה אף עכביש שכבר מטפס
            float speed = _climbSpeed * (GameManager.Instance != null
                ? GameManager.Instance.SpiderSpeedMultiplier
                : 1f);

            Vector2 next = rb.position + Vector2.up * (speed * Time.fixedDeltaTime);

            // השמה ל-rb.position היא הזזה מיידית: הגוף מועתק, בלי תנועה סחופה.
            //
            // חשוב מאוד שלא להשתמש כאן ב-MovePosition. תפקידו לבצע תנועה עם
            // דחיפה, כלומר לדחוף גופים דינמיים לאורך המסלול ולהקנות להם את
            // מהירות התנועה. התוצאה הייתה שגופות של עכבישים מתים הוסעו כלפי
            // מעלה במהירות הטיפוס המדויקת ולא הגיעו לעולם לאזור הסכנה.
            // נמדד בהרצה: כל הגופות קראו בדיוק (0, 0.300) = spiderClimbSpeed.
            //
            // הפרויקט הישן CVS_ForSteam_v004 הזיז את ה-Transform ישירות
            // (BaseSpiderController.HandleMovement) ולכן מעולם לא נתקל בזה.
            rb.position = next;
            _data.Position = next;
            return;
        }

        if (_data.State == SpiderState.Falling) _data.Position = rb.position;
    }

    // כל התנגשות מדווחת ל-InteractionsManager, שהוא זה שמחליט מה התוצאה - GDD 7.2
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (InteractionsManager.Instance == null) return;
        InteractionsManager.Instance.ReportSpiderCollision(this, collision);
    }

    // ---------- פיזיקת המוות ----------

    // התאמת הקולידר לגופה. שני הערכים חשופים ב-Inspector כדי לכוונן בעין,
    // כי שניהם נגזרים ממה שרואים ולא ממספר "נכון" כלשהו.
    private void ApplyDeathCollider()
    {
        var circle = bodyCollider as CircleCollider2D;
        if (circle == null) return;

        if (deathColliderRadius > 0f) circle.radius = deathColliderRadius;
        if (zeroColliderOffsetOnDeath) circle.offset = Vector2.zero;
    }

    private void ApplyDeathPhysics()
    {
        if (rb == null) return;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = deathGravityScale;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        rb.AddForce(BuildDeathImpulse(_deathImpact), ForceMode2D.Impulse);
        rb.AddTorque(Random.Range(deathTorqueRange.x, deathTorqueRange.y), ForceMode2D.Impulse);
    }

    // דחף אלכסוני כלפי מעלה והצידה, אל הצד המנוגד לנקודת הפגיעה - GDD 3.3
    private Vector2 BuildDeathImpulse(Vector2 impactPoint)
    {
        float horizontalOffset = rb.position.x - impactPoint.x;
        float side = Mathf.Approximately(horizontalOffset, 0f)
            ? (Random.value < 0.5f ? -1f : 1f)
            : Mathf.Sign(horizontalOffset);

        float angle = Random.Range(deathAngleRange.x, deathAngleRange.y) * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Sin(angle) * side, Mathf.Cos(angle));

        float magnitude = deathForce * Random.Range(1f - deathForceVariance, 1f + deathForceVariance);
        return direction * magnitude;
    }

}
