namespace VamTimeline
{
    public class TriggersTrackRef : AnimatableRefBase
    {
        public override string name => _name;

        public bool live;
        private string _name;

        public TriggersTrackRef(string triggerTrackName)
        {
            _name = triggerTrackName;
        }

        public override string GetShortName() => _name;
        public override string GetFullName() => _name;

        public void SetName(string value) => _name = value;

        public bool Targets(string triggerTrackName)
        {
            return _name == triggerTrackName;
        }
    }
}
