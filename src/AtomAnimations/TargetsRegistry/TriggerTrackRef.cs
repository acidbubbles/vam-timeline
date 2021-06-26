namespace VamTimeline
{
    public class TriggerTrackRef : AnimatableRefBase
    {
        public string name { get; }

        public TriggerTrackRef(string triggerTrackName)
        {
            name = triggerTrackName;
        }

        public bool Targets(string triggerTrackName)
        {
            return name == triggerTrackName;
        }
    }
}
