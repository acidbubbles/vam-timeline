namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MorphsAnimationSerializer : AnimationSerializerBase, IAnimationSerializer<MorphsAnimation>
    {
        public MorphsAnimationSerializer(Atom atom)
            : base(atom)
        {
        }

        public MorphsAnimation CreateDefaultAnimation()
        {
            return new MorphsAnimation();
        }

        public MorphsAnimation DeserializeAnimation(string val)
        {
            throw new System.NotImplementedException();
        }

        public string SerializeAnimation(MorphsAnimation animation)
        {
            // TODO: Implement
            return "";
        }
    }
}
