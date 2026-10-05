namespace Piglings.Simulation
{
    /// <summary>What a post-run button does (M10.E).</summary>
    public enum PostRunAction
    {
        None,
        Retry,       // the same night again, fast: straight into the night (no barn room)
        ToBarn,      // down to the doors, then the barn room of the same night (the day phase)
        NextNight,   // down to the doors, then the barn room of the next night
    }

    /// <summary>
    /// Which two buttons the post-run shows (M10.E, PROTOTYPE_V2.md ▸ PR E). "To the barn" and "Next night" are never shown
    /// together: a loss → Retry (primary) + To the barn; a dawn → Next night (primary) + Retry; a dawn on the last night has
    /// no next night → To the barn (primary) + Retry. Engine-free (CoreCheck runs it).
    /// </summary>
    public static class PostRunChoices
    {
        /// <param name="dawn">Tonight was won.</param>
        /// <param name="nextNight">There's a next night to go to (CampaignPlan.CanGoNext: a dawn here and a night after it).</param>
        public static void For(bool dawn, bool nextNight, out PostRunAction primary, out PostRunAction secondary)
        {
            if (!dawn) { primary = PostRunAction.Retry; secondary = PostRunAction.ToBarn; return; }
            primary = nextNight ? PostRunAction.NextNight : PostRunAction.ToBarn;
            secondary = PostRunAction.Retry;
        }
    }
}
