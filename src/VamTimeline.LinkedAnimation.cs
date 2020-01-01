using System;
using System.Linq;

namespace VamTimeline
{
    public class LinkedAnimation
    {
        public Atom Atom;

        public LinkedAnimation(Atom atom)
        {
            Atom = atom;
            if (GetStorableId() == null)
                throw new InvalidOperationException($"Atom {atom.uid} does not have VamTimeline.AtomPlugin configured.");
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
            return Atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.AtomPlugin"));
        }

        public JSONStorableFloat Scrubber { get { return Storable.GetFloatJSONParam(StorableNames.Time); } }
        public JSONStorableStringChooser Animation { get { return Storable.GetStringChooserJSONParam(StorableNames.Animation); } }
        public JSONStorableStringChooser SelectedController { get { return Storable.GetStringChooserJSONParam(StorableNames.SelectedController); } }
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
