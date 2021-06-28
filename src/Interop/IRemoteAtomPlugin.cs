
using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public interface IRemoteAtomPlugin : ITimelineListener
    {
        void VamTimelineConnectController(Dictionary<string, object> dict);
        void VamTimelineRequestControlPanel(GameObject container);
        void OnTimelineEvent(object[] e);
    }
}
