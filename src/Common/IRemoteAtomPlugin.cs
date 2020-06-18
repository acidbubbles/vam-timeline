
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
    public interface IRemoteAtomPlugin
    {
        void VamTimelineConnectController(Dictionary<string, object> dict);
        void VamTimelineRequestControlPanel(GameObject container);
    }
}
