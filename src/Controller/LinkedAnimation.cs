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

        public Atom atom;
        public string label => atom.uid;

        private JSONStorable _storable;
        private JSONStorableFloat _scrubber;

        public LinkedAnimation(Atom atom)
        {
            this.atom = atom;
        }

        public JSONStorable Storable
        {
            get
            {
                if (_storable != null)
                {
                    if (_storable.containingAtom == null)
                    {
                        _storable = null;
                    }
                    else
                    {
                        return _storable;
                    }
                }
                var storableId = GetStorableId();
                if (storableId == null) return null;
                _storable = atom.GetStorableByID(storableId);
                return _storable;
            }
        }

        private string GetStorableId()
        {
            return GetStorableId(atom);
        }

        public JSONStorableFloat Scrubber
        {
            get
            {
                var previous = _storable;
                if (_scrubber != null && Storable == previous) return _scrubber;
                _scrubber = Storable?.GetFloatJSONParam(StorableNames.Scrubber);
                if(_scrubber == null) throw new NullReferenceException($"Scrubber of atom '{_storable?.containingAtom.name}' is null.");
                return _scrubber;
            }
        }
        public JSONStorableBool Locked { get { return Storable?.GetBoolJSONParam(StorableNames.Locked); } }
        public JSONStorableFloat Time { get { return Storable?.GetFloatJSONParam(StorableNames.Time); } }
        public JSONStorableStringChooser Animation { get { return Storable?.GetStringChooserJSONParam(StorableNames.Animation); } }
        public JSONStorableBool IsPlaying { get { return Storable?.GetBoolJSONParam(StorableNames.IsPlaying); } }

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

        public void StopIfPlaying()
        {
            Storable.CallAction(StorableNames.StopIfPlaying);
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
