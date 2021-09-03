using System.Text.RegularExpressions;
using UnityEngine;

namespace VamTimeline
{
    public class Logger
    {
        public bool clearOnPlay { get; set; }

        public bool general { get; set; }
        public readonly string generalCategory = "gen";
        public bool triggers { get; set; }
        public readonly string triggersCategory = "trig";
        public bool sequencing { get; set; }
        public readonly string sequencingCategory = "seq";
        public bool peersSync { get; set; }
        public readonly string peersSyncCategory = "peer";
        public bool blending { get; set; }
        public readonly string blendingCategory = "blend";

        public Regex filter { get; set; }

        private readonly Atom _containingAtom;

        public Logger(Atom containingAtom)
        {
            _containingAtom = containingAtom;
        }

        public void Begin()
        {
            if(clearOnPlay) SuperController.singleton.ClearMessages();
        }

        public void Log(string category, string message)
        {
            if (filter != null && !filter.IsMatch(message)) return;
            SuperController.LogMessage($"[{Time.time:0.000} {_containingAtom.name} {category}] {message}");
        }
    }
}
