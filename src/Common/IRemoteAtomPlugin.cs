
using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public interface IRemoteAtomPlugin
    {
        void VamTimelineConnectController(Dictionary<string, object> dict);
        void VamTimelineRequestControlPanel(GameObject container);
    }
}
