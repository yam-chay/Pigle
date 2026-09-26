using UnityEngine;

// פתרון זווית השיגור לזריקה במהירות קבועה - GDD 3.1
//
// **הבעיה שזה פותר:** זריקה ישרה אל נקודת הלחיצה מחטיאה תמיד, כי הכבידה
// מושכת את הביצה מתחת לקו הישר, וההחטאה גדלה עם המרחק. נמדד בפרויקט:
// במהירות 10, מטרה במרחק 5 יחידות נופלת 1.23 יחידות מתחת לנקודה שנלחצה -
// קרוב לשלושה גבהי עכביש.
//
// **הפתרון שומר על GDD 3.1 מילה במילה:** "כוח הזריקה קבוע, לא נשלט ע"י
// השחקן". המהירות אכן נשארת קבועה - רק הזווית משתנה. וזה גם שומר על הקשת
// הפיזיקלית שסעיף 2 מבקש, במקום לשטח אותה ע"י העלאת המהירות.
//
// מחלקה טהורה בלי MonoBehaviour, כמו EggInventory: גם הזריקה וגם ציור
// המסלול קוראים לה, ולכן אסור שהמתמטיקה תשב בתוך אחד מהם.
public static class ThrowSolver
{
    // מהירות השיגור שתעביר את הקליע דרך נקודת המטרה.
    //
    // מחזיר false אם המטרה מחוץ לטווח במהירות הזו, ואז velocity מכיל את
    // הזריקה הרחוקה ביותר האפשרית לכיוון הזה - 45 מעלות, שנותנות טווח מרבי.
    public static bool TrySolve(Vector2 from, Vector2 to, float speed, float gravity, out Vector2 velocity)
    {
        Vector2 delta = to - from;
        float x = Mathf.Abs(delta.x);
        float y = delta.y;
        float dirX = delta.x < 0f ? -1f : 1f;

        // בלי כבידה אין פרבולה לפתור, והזריקה הישרה נכונה.
        // מטרה ישירות מעל או מתחת מתנוונת גם היא - אין רכיב אופקי לפתור לפיו
        if (gravity <= 0f || x < 0.0001f)
        {
            velocity = delta.sqrMagnitude > 0f ? delta.normalized * speed : Vector2.up * speed;
            return gravity <= 0f;
        }

        // מציבים y = x*tan - g*x^2 / (2*v^2*cos^2) ומשתמשים ב-1/cos^2 = 1+tan^2.
        // מתקבלת משוואה ריבועית ב-tan של הזווית:  k*tan^2 - x*tan + (y+k) = 0
        float k = gravity * x * x / (2f * speed * speed);
        float disc = x * x - 4f * k * (y + k);

        if (disc < 0f)
        {
            const float quarterPi = Mathf.PI / 4f;
            velocity = new Vector2(dirX * Mathf.Cos(quarterPi), Mathf.Sin(quarterPi)) * speed;
            return false;
        }

        // שני פתרונות: מסלול נמוך וישיר, ומסלול גבוה ומקושת.
        // השורש הקטן הוא הנמוך, והוא המתאים לזריקה על מטרה נראית -
        // הגבוה מיועד להעיף מעל מכשול, וזה לא המשחק הזה
        float tan = (x - Mathf.Sqrt(disc)) / (2f * k);
        float angle = Mathf.Atan(tan);

        velocity = new Vector2(dirX * Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        return true;
    }

    // כמה זמן ייקח לקליע לחצות את המרחק האופקי הזה. משמש לקטיעת ציור
    // המסלול בדיוק בנקודת המטרה, במקום באורך שרירותי
    public static float TimeToCross(Vector2 velocity, float horizontalDistance)
    {
        float vx = Mathf.Abs(velocity.x);
        if (vx < 0.0001f) return 0f;
        return Mathf.Abs(horizontalDistance) / vx;
    }

    // דגימת נקודות המסלול לציור. כותב לתוך מערך קיים כדי לא להקצות בכל פריים.
    // מחזיר כמה נקודות נכתבו בפועל
    public static int SamplePath(Vector2 from, Vector2 velocity, float gravity, float duration, Vector2[] into)
    {
        if (into == null || into.Length < 2 || duration <= 0f) return 0;

        int n = into.Length;
        float step = duration / (n - 1);

        for (int i = 0; i < n; i++)
        {
            float t = step * i;
            into[i] = new Vector2(
                from.x + velocity.x * t,
                from.y + velocity.y * t - 0.5f * gravity * t * t);
        }

        return n;
    }
}
