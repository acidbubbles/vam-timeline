using System;
using System.Linq;

namespace AcidBubbles.VamTimeline
{
    public class LinkedAnimation
    {
        public Atom Atom;

        public LinkedAnimation(Atom atom)
        {
            Atom = atom;
            if (GetStorableId() == null)
                throw new InvalidOperationException($"Atom {atom.uid} does not have AcidBubbles.VamTimeline.AtomPlugin configured.");
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

        public JSONStorableFloat Scrubber { get { return Storable.GetFloatJSONParam(AtomPluginStorableNames.Time); } }
        public JSONStorableStringChooser Animation { get { return Storable.GetStringChooserJSONParam(AtomPluginStorableNames.Animation); } }
        public JSONStorableStringChooser SelectedController { get { return Storable.GetStringChooserJSONParam(AtomPluginStorableNames.SelectedController); } }
        public JSONStorableString Display { get { return Storable.GetStringJSONParam(AtomPluginStorableNames.Display); } }

        public void Play()
        {
            Storable.CallAction("Play");
        }

        public void Stop()
        {
            Storable.CallAction("Stop");
        }

        public void NextFrame()
        {
            Storable.CallAction("Next Frame");
        }

        public void PreviousFrame()
        {
            Storable.CallAction("Previous Frame");
        }

        public void ChangeAnimation(string name)
        {
            if (Animation.choices.Contains(name))
                Animation.val = name;
        }
    }
}
