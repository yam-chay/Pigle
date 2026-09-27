namespace Piglings.Rules
{
    /// <summary>
    /// Score arithmetic shared by the Rules. Scores can get huge (compounding curve, long overtime):
    /// saturate at int.MaxValue rather than wrap into a negative score.
    /// </summary>
    internal static class ScoreMath
    {
        public static int AddClamped(int a, int b) => (int)System.Math.Min(int.MaxValue, (long)a + b);
    }
}
