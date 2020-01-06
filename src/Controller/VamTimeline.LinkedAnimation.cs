using System;
using System.Linq;

namespace VamTimeline
{
    public class LinkedAnimation
    {
        public static LinkedAnimation TryCreate(Atom atom)
        {
            if (GetStorableId(atom) == null)
                return null;

            return new LinkedAnimation(atom);
        }

        private static string GetStorableId(Atom atom)
        {
            return atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.AtomPlugin"));
        }

        public Atom Atom;
        public string Label => Atom.uid;

        public LinkedAnimation(Atom atom)
        {
            Atom = atom;
        }

        private JSONStorable Storable
        {
            get
            {
                var storableId = GetStorableId();
                if (storableId == null) return null;
                return Atom.GetStorableByID(storableId);
            }
        }

        private string GetStorableId()
        {
            return GetStorableId(Atom);
        }

        public JSONStorableBool Locked { get { return Storable?.GetBoolJSONParam(StorableNames.Locked); } }
        public JSONStorableFloat Scrubber { get { return Storable?.GetFloatJSONParam(StorableNames.Time); } }
        public JSONStorableStringChooser Animation { get { return Storable?.GetStringChooserJSONParam(StorableNames.Animation); } }
        public JSONStorableStringChooser FilterAnimationTarget { get { return Storable?.GetStringChooserJSONParam(StorableNames.FilterAnimationTarget); } }
        public JSONStorableString Display { get { return Storable?.GetStringJSONParam(StorableNames.Display); } }

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
