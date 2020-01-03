
using System.Collections.Generic;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAnimationClip
    {
        string AnimationName { get; }
        float AnimationLength { get; set; }

        bool IsEmpty();
        float GetNextFrame(float time);
        float GetPreviousFrame(float time);
        void DeleteFrame(float time);
        void SelectTargetByName(string name);
        IEnumerable<string> GetTargetsNames();
        IEnumerable<IAnimationTarget> GetAllOrSelectedTargets();
    }
}
