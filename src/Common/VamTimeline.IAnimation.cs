using System.Collections.Generic;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAnimation
    {
        float Time { get; set; }
        float AnimationLength { get; set; }
        float Speed { get; set; }
        float BlendDuration { get; set; }

        void Play();
        bool IsPlaying();
        void Stop();
        float GetNextFrame();
        float GetPreviousFrame();
        bool IsEmpty();
        void Initialize();
        string AddAnimation();
        void ChangeAnimation(string animationName);
        void DeleteFrame();
        IEnumerable<string> GetTargetsNames();
        void SelectTargetByName(string name);
        IEnumerable<string> GetAnimationNames();
        IEnumerable<IAnimationTarget> GetAllOrSelectedControllers();
        IClipboardEntry Copy();
        void Paste(IClipboardEntry clipboard);
    }
}
