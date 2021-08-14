using System.Globalization;
using System.Linq;
using SimpleJSON;

namespace VamTimeline
{
    public class VamOverlaysFadeManager : IFadeManager
    {
        public bool black { get; private set; }
        public float blackTime { get; set; }
        public float fadeInTime { get; private set; }
        public float fadeOutTime { get; private set; }

        private bool _connected;
        private string _atomUid;
        private Atom _atom;

        private JSONStorableAction _fadeIn;
        private JSONStorableAction _fadeOut;
        private JSONStorableFloat _fadeInTime;
        private JSONStorableFloat _fadeOutTime;

        public static IFadeManager FromAtomUid(string atomUid, float blackTime)
        {
            return new VamOverlaysFadeManager { _atomUid = atomUid, blackTime = blackTime};
        }

        public void SyncFadeTime()
        {
            fadeInTime = _fadeInTime?.val ?? 1f;
            fadeOutTime = _fadeOutTime?.val ?? 1f;
        }

        public void FadeIn()
        {
            black = false;
            if (TryConnectNow())
                _fadeIn.actionCallback();
        }

        public void FadeOut()
        {
            if (TryConnectNow())
            {
                black = true;
                _fadeOut.actionCallback();
            }
        }

        public string GetAtomUid()
        {
            return _atom != null ? _atom.uid : _atomUid;
        }

        public bool TryConnectNow()
        {
            if (_connected) return true;
            _atom = SuperController.singleton.GetAtomByUid(_atomUid);
            if (_atom == null) return false;
            var overlays = _atom.GetStorableIDs().Select(_atom.GetStorableByID).FirstOrDefault(s => s.IsAction("Start Fade In"));
            if (overlays == null) return false;
            _fadeIn = overlays.GetAction("Start Fade In");
            _fadeOut = overlays.GetAction("Start Fade Out");
            _fadeInTime = overlays.GetFloatJSONParam("Fade in time");
            _fadeOutTime = overlays.GetFloatJSONParam("Fade out time");
            return _connected = (_fadeIn != null && _fadeOut != null);
        }

        public JSONNode GetJSON()
        {
            return new JSONClass
            {
                ["Atom"] = GetAtomUid(),
                ["BlackTime"] = blackTime.ToString(CultureInfo.InvariantCulture)
            };
        }

        public static IFadeManager FromJSON(JSONClass jc)
        {
            var atomUid = jc["Atom"].Value;
            if (atomUid == null) return null;
            return FromAtomUid(atomUid, jc["BlackTime"].AsFloat);
        }
    }

    public interface IFadeManager
    {
        float blackTime { get; set; }
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
