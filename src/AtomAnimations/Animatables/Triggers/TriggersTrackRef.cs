namespace VamTimeline
{
    public class TriggersTrackRef : AnimatableRefBase
    {
        public override string name { get; }

        public TriggersTrackRef(string triggerTrackName)
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
