using UnityEngine;

// טריגר פיזיקלי, קבוע בתחתית המסך; מסיר אובייקטים שנכנסים אליו - GDD 4.1, 8.1, 8.3
//
// עכביש חי עובר דרך האזור בלי להיפגע, וההחלטה על כך מתקבלת ב-InteractionsManager.
// בגלל זה לא די בכניסה בלבד: עכביש שנולד בתוך האזור ונהרג בעודו בתוכו לא ייצר
// אירוע כניסה חדש, ולכן נבדק גם כל עוד הוא שוהה בתוך האזור.
public class DangerZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        Report(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Report(other);
    }

    private void Report(Collider2D other)
    {
        if (other == null || InteractionsManager.Instance == null) return;
        InteractionsManager.Instance.HandleDangerZoneEntry(other.gameObject);
    }
}
