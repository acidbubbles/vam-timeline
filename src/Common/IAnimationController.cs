using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAnimationController
    {
        void VamTimelineAnimationModified(string uid);
        void VamTimelineAnimationFrameUpdated(string uid);
        void VamTimelineAnimationTargetsModified(List<KeyValuePair<string, List<KeyValuePair<string, List<float>>>>> targets);
    }
}
