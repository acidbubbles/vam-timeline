namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAnimationSerializer<TAnimation> where TAnimation : class, IAnimation
    {
        TAnimation CreateDefaultAnimation();
        TAnimation DeserializeAnimation(string val);
        string SerializeAnimation(TAnimation animation);
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class AnimationSerializerBase
    {
        protected Atom _atom;

        protected AnimationSerializerBase(Atom atom)
        {
            _atom = atom;
        }
    }
}
