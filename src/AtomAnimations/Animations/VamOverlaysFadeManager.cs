using System.Linq;
using SimpleJSON;

namespace VamTimeline
{
    public class VamOverlaysFadeManager : IFadeManager
    {
        public bool black { get; private set; }
        public float fadeInTime { get; private set; }
        public float fadeOutTime { get; private set; }

        private string _atomUid;
        private Atom _atom;

        private JSONStorableAction _fadeIn;
        private JSONStorableAction _fadeOut;
        private JSONStorableFloat _fadeInTime;
        private JSONStorableFloat _fadeOutTime;

        public static IFadeManager FromAtomUid(string val)
        {
            return new VamOverlaysFadeManager { _atomUid = val };
        }

        public void SyncFadeTime()
        {
            fadeInTime = _fadeInTime?.val ?? 1f;
            fadeOutTime = _fadeOutTime?.val ?? 1f;
        }

        public void FadeIn()
        {
            black = false;
            _fadeIn?.actionCallback();
        }

        public void FadeOut()
        {
            black = true;
            _fadeOut?.actionCallback();
        }

        public string GetAtomUid()
        {
            return _atom != null ? _atom.uid : _atomUid;
        }

        public bool TryConnectNow()
        {
            _atom = SuperController.singleton.GetAtomByUid(_atomUid);
            if (_atom == null) return false;
            var overlays = _atom.GetStorableIDs().Select(_atom.GetStorableByID).FirstOrDefault(s => s.IsAction("Start Fade In"));
            if (overlays == null) return false;
            _fadeIn = overlays.GetAction("Start Fade In");
            _fadeOut = overlays.GetAction("Start Fade Out");
            _fadeInTime = overlays.GetFloatJSONParam("Fade in time");
            _fadeOutTime = overlays.GetFloatJSONParam("Fade out time");
            return _fadeIn != null && _fadeOut != null;
        }

        public JSONNode GetJSON()
        {
            return new JSONClass
            {
                ["Atom"] = GetAtomUid()
            };
        }

        public static IFadeManager FromJSON(JSONClass jc)
        {
            var atomUid = jc["Atom"].Value;
            if (atomUid == null) return null;
            return FromAtomUid(atomUid);
        }
    }

    public interface IFadeManager
    {
        bool black { get; }
        float fadeInTime { get; }
        float fadeOutTime { get; }

        string GetAtomUid();
        bool TryConnectNow();
        JSONNode GetJSON();
        void SyncFadeTime();
        void FadeIn();
        void FadeOut();
    }
}
