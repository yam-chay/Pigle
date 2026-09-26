public static class GameConst
{
    public const int MAX_EGGS_PER_RUN = 42;
}

public enum SpiderType
{
    Red
}

public enum GameOverReason
{
    ChickenFellOffscreen
}

// מכונת המצבים של העכביש - ראה הדיאגרמה ב-TASKS.md
//
//   Spawned -> Ready -> Climbing --+-- ReachedTowerTop -> EnteredTower -> Removed
//                                  |
//                                  +-- Falling -> Removed
//
// המצב הוא מקור האמת היחיד. כל מעבר הוא שמפעיל את השינויים הנלווים:
// סוג הגוף הפיזיקלי, האנימציה, והאינטראקציה עם שאר העכבישים.
public enum SpiderState
{
    // נוצר, לפני Initialize
    Spawned,

    // אותחל וחווט, רגע לפני תחילת הטיפוס
    Ready,

    // מטפס קינמטית כלפי מעלה
    Climbing,

    // נגע ב-TowerTop. עצר את הטיפוס ואנימציית ההיעלמות התחילה.
    // עדיין נראה על המסך, וכבר אינו ניתן להריגה
    ReachedTowerTop,

    // אנימציית ההיעלמות הסתיימה. זהו הרגע שבו התרנגול מת
    EnteredTower,

    // נהרג. גוף דינמי שנופל ומתגלגל עד לאזור הסכנה
    Falling,

    // סומן להשמדה
    Removed
}

// מי הרג את העכביש. ההתנהגות הפיזיקלית זהה בשני המקרים,
// אבל הספירה נפרדת לצורך הישגים ואתגרים - GDD 5.2
public enum SpiderKiller
{
    None,
    Egg,
    DeadChicken
}
