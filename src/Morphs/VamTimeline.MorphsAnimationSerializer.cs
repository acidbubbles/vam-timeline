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
            throw new System.NotImplementedException();
        }

        public MorphsAnimation DeserializeAnimation(string val)
        {
            throw new System.NotImplementedException();
        }

        public string SerializeAnimation(MorphsAnimation animation)
        {
            throw new System.NotImplementedException();
        }
    }
}
