namespace Content.Server.Speech.Components
{
    [RegisterComponent]
    public sealed partial class ScrambledAccentComponent : Component
    {
        // Aurora - allow option to bypass the single word filter.
        /// <summary>
        /// If set, will change single word messages to a randomly selected word in a preset table.
        /// Prevents circumvention of the filter by sending a series of single word messages.
        /// </summary>
        [DataField]
        public bool ScrambleSingleWords = false;
    }
}
