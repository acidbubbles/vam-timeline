namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class JSONStorableFloatAnimationSerializer : AnimationSerializerBase, IAnimationSerializer<JSONStorableFloatAnimation>
    {
        public JSONStorableFloatAnimationSerializer(Atom atom)
            : base(atom)
        {
        }

        public JSONStorableFloatAnimation CreateDefaultAnimation()
        {
            return new JSONStorableFloatAnimation();
        }

        public JSONStorableFloatAnimation DeserializeAnimation(string val)
        {
            throw new System.NotImplementedException();
        }

        public string SerializeAnimation(JSONStorableFloatAnimation animation)
        {
            // TODO: Implement
            return "";
        }
    }
}
