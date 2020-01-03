using System.Collections.Generic;

namespace VamTimeline
{

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAnimation<TClip, TTarget>
        where TClip : class, IAnimationClip<TTarget>
        where TTarget : class, IAnimationTarget
    {
        float Time { get; set; }
        float AnimationLength { get; set; }
        float Speed { get; set; }
        float BlendDuration { get; set; }
        List<TClip> Clips { get; }

        void Initialize();
        void AddClip(TClip clip);
        void RebuildAnimation();

        void Play();
        bool IsPlaying();
        void Stop();

        float GetNextFrame();
        float GetPreviousFrame();
        void DeleteFrame();

        bool IsEmpty();

        IEnumerable<string> GetAnimationNames();
        string AddAnimation();
        void ChangeAnimation(string animationName);

        IEnumerable<string> GetTargetsNames();
        void SelectTargetByName(string name);
        IEnumerable<TTarget> GetAllOrSelectedTargets();

        IClipboardEntry Copy();
        void Paste(IClipboardEntry clipboard);
    }
}
