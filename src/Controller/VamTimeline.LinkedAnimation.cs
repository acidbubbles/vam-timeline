using System.Linq;

namespace VamTimeline
{
    public class LinkedAnimation
    {
        public static LinkedAnimation TryCreate(Atom atom, string pluginSuffix)
        {
            if (GetStorableId(atom, pluginSuffix) == null)
                return null;

            return new LinkedAnimation(atom, pluginSuffix);
        }

        private static string GetStorableId(Atom atom, string pluginSuffix)
        {
            return atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith(pluginSuffix));
        }

        public Atom Atom;
        private readonly string _pluginSuffix;

        public LinkedAnimation(Atom atom, string pluginSuffix)
        {
            Atom = atom;
            _pluginSuffix = pluginSuffix;
        }

        private JSONStorable Storable
        {
            get
            {
                var storableId = GetStorableId();
                return Atom.GetStorableByID(storableId);
            }
        }

        private string GetStorableId()
        {
            return GetStorableId(Atom, _pluginSuffix);
        }

        public JSONStorableFloat Scrubber { get { return Storable.GetFloatJSONParam(StorableNames.Time); } }
        public JSONStorableStringChooser Animation { get { return Storable.GetStringChooserJSONParam(StorableNames.Animation); } }
        public JSONStorableStringChooser SelectedController { get { return Storable.GetStringChooserJSONParam(StorableNames.FilterAnimationTarget); } }
        public JSONStorableString Display { get { return Storable.GetStringJSONParam(StorableNames.Display); } }

        public void Play()
        {
            Storable.CallAction(StorableNames.Play);
        }

        public void PlayIfNotPlaying()
        {
            Storable.CallAction(StorableNames.PlayIfNotPlaying);
        }

        public void Stop()
        {
            Storable.CallAction(StorableNames.Stop);
        }

        public void NextFrame()
        {
            Storable.CallAction(StorableNames.NextFrame);
        }

        public void PreviousFrame()
        {
            Storable.CallAction(StorableNames.PreviousFrame);
        }

        public void ChangeAnimation(string name)
        {
            if (Animation.choices.Contains(name))
                Animation.val = name;
        }
    }
}
