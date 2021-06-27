namespace VamTimeline
{
    public class TriggerTrackRef : AnimatableRefBase
    {
        public override string name { get; }

        public TriggerTrackRef(string triggerTrackName)
        {
            name = triggerTrackName;
        }

        public override string GetShortName() => name;

        public bool Targets(string triggerTrackName)
        {
            return name == triggerTrackName;
        }
    }
}
