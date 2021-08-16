using System.Globalization;
using System.Linq;
using SimpleJSON;

namespace VamTimeline
{
    public interface IFadeManager
    {
        float blackTime { get; set; }
        float halfBlackTime { get; }
        bool black { get; }
        float fadeInTime { get; }
        float fadeOutTime { get; }

        string GetAtomUid();
        bool TryConnectNow();
        JSONNode GetJSON();
        void SyncFadeTime();
        void FadeIn();
        void FadeOut();
        void FadeOutInstant();
        bool ShowText(string text);
    }

    public class VamOverlaysFadeManager : IFadeManager
    {
        public float blackTime
        {
            get { return _blackTime; }
            set
            {
                _blackTime = value;
                halfBlackTime = blackTime / 2f;
            }
        }
        public float halfBlackTime { get; private set; }
        public bool black { get; private set; }

        public float fadeInTime { get; private set; }
        public float fadeOutTime { get; private set; }

        private float _blackTime;
        private JSONStorable _overlays;
        private string _atomUid;
        private Atom _atom;

        private JSONStorableAction _fadeIn;
        private JSONStorableAction _fadeOut;
        private JSONStorableAction _fadeOutInstant;
        private JSONStorableFloat _fadeInTime;
        private JSONStorableFloat _fadeOutTime;
        private JSONStorableString _showText;
        private JSONStorableAction _hideText;

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

        public void FadeOutInstant()
        {
            if (TryConnectNow() && _fadeOutInstant != null)
            {
                black = true;
                _fadeOutInstant.actionCallback();
            }
        }

        public bool ShowText(string text)
        {
            if (!TryConnectNow())
                return false;

            if (text != null)
            {
                if (_showText == null) return false;
                _showText.val = text;
            }
            else
            {
                if (_hideText == null) return false;
                _hideText.actionCallback.Invoke();
            }

            return true;
        }

        public string GetAtomUid()
        {
            return _atom != null ? _atom.uid : _atomUid;
        }

        public bool TryConnectNow()
        {
            if (_overlays != null) return true;
            _atom = SuperController.singleton.GetAtomByUid(_atomUid);
            if (_atom == null) return false;
            _overlays = _atom.GetStorableIDs().Select(_atom.GetStorableByID).FirstOrDefault(s => s.IsAction("Start Fade In"));
            if (_overlays == null) return false;
            _fadeIn = _overlays.GetAction("Start Fade In");
            _fadeOut = _overlays.GetAction("Start Fade Out");
            _fadeOutInstant = _overlays.GetAction("Fade Out Instant");
            _fadeInTime = _overlays.GetFloatJSONParam("Fade in time");
            _fadeOutTime = _overlays.GetFloatJSONParam("Fade out time");
            _showText = _overlays.GetStringJSONParam("Set and show subtitles instant");
            _hideText = _overlays.GetAction("Hide subtitles instant");
            if (_fadeIn != null && _fadeOut != null) return true;
            _overlays = null;
            return false;
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
}
