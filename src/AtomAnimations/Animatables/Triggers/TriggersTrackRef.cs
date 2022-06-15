namespace VamTimeline
{
    public class TriggersTrackRef : AnimatableRefBase
    {
        public override string name => _name;
        public override object groupKey => null;
        public override string groupLabel => "Triggers";

        public bool live;
        public int animationLayerQualifiedId;
        private string _name;

        public TriggersTrackRef(int layerQualifiedId, string triggerTrackName)
        {
            animationLayerQualifiedId = layerQualifiedId;
            _name = triggerTrackName;
        }

        public override string GetShortName() => _name;
        public override string GetFullName() => _name;

        public void SetName(string value) => _name = value;

        public bool Targets(int layerQualifiedId, string triggerTrackName)
        {
            return animationLayerQualifiedId == layerQualifiedId && _name == triggerTrackName;
        }
    }
}
