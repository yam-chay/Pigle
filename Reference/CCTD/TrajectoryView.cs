using UnityEngine;

// ציור המסלול החזוי של הזריקה - GDD 7.3
//
// תצוגה בלבד. אינה מחשבת דבר ואינה יודעת מה זו ביצה: ThrowController מוסר לה
// נקודת מוצא, מהירות ומשך, והיא מציירת את הפרבולה שנובעת מהם.
// המתמטיקה עצמה יושבת ב-ThrowSolver, כדי שהקו שמצויר יהיה בהכרח אותו מסלול
// שהביצה תעוף בו - ולא חישוב מקביל שעלול להיפרד ממנו.
[RequireComponent(typeof(LineRenderer))]
public class TrajectoryView : MonoBehaviour
{
    [SerializeField] private LineRenderer line;

    [Tooltip("כמה נקודות לדגום לאורך המסלול. יותר = קו חלק יותר")]
    [Min(2)] [SerializeField] private int sampleCount = 32;

    [Tooltip("צבע הקו כשהמטרה בטווח")]
    [SerializeField] private Color inRangeColor = new Color(1f, 1f, 1f, 0.55f);

    [Tooltip("צבע הקו כשהמטרה מחוץ לטווח, והזריקה לא תגיע אליה")]
    [SerializeField] private Color outOfRangeColor = new Color(1f, 0.35f, 0.3f, 0.55f);

    private Vector2[] _points;
    private Vector3[] _worldPoints;

    private void Awake()
    {
        if (line == null) line = GetComponent<LineRenderer>();

        _points = new Vector2[sampleCount];
        _worldPoints = new Vector3[sampleCount];

        line.useWorldSpace = true;
        line.positionCount = 0;
    }

    // ציור המסלול. inRange מגיע מ-ThrowSolver ומשנה רק את הצבע -
    // הקו עצמו מצויר בכל מקרה, כדי שרואים לאן הזריקה כן תגיע
    public void Show(Vector2 from, Vector2 velocity, float gravity, float duration, bool inRange)
    {
        if (line == null || _points == null) return;

        int n = ThrowSolver.SamplePath(from, velocity, gravity, duration, _points);
        if (n == 0) { Hide(); return; }

        for (int i = 0; i < n; i++)
        {
            _worldPoints[i] = new Vector3(_points[i].x, _points[i].y, 0f);
        }

        line.positionCount = n;
        line.SetPositions(_worldPoints);

        Color c = inRange ? inRangeColor : outOfRangeColor;
        line.startColor = c;
        line.endColor = new Color(c.r, c.g, c.b, c.a * 0.25f);
    }

    public void Hide()
    {
        if (line == null) return;
        line.positionCount = 0;
    }
}
