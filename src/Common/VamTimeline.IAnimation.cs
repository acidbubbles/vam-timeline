using System.Collections.Generic;
using System.Linq;

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

        void Initialize();

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
        IEnumerable<IAnimationTarget> GetAllOrSelectedTargets();

        IClipboardEntry Copy();
        void Paste(IClipboardEntry clipboard);
    }
}
