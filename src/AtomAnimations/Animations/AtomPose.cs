using System.Linq;
using MeshVR;
using SimpleJSON;

namespace VamTimeline
{
    public class AtomPose
    {
        private readonly Atom _atom;
        private readonly JSONClass _poseJSON;

        public static AtomPose FromAtom(Atom atom)
        {
            if (atom.type != "Person") return null;

            var storables = atom.GetStorableIDs()
                .Select(atom.GetStorableByID)
                .Where(t => !t.exclude && t.gameObject.activeInHierarchy)
                .Where(t => t is FreeControllerV3 || t is DAZBone);
            var storablesJSON = new JSONArray();
            foreach (var storable in storables)
            {
                var storableJSON = storable.GetJSON(true, true, true);
                storablesJSON.Add(storableJSON);
            }

            var poseJSON = new JSONClass
            {
                ["setUnlistedParamsToDefault"] = {AsBool = true},
                ["V2"] = {AsBool = true},
                ["storables"] = storablesJSON
            };

            return new AtomPose(atom, poseJSON);
        }

        public static AtomPose FromJSON(Atom atom, JSONNode jsonNode)
        {
            var jc = jsonNode.AsObject;
            return jc.HasKey("storables") ? new AtomPose(atom, jc) : null;
        }

        private AtomPose(Atom atom, JSONClass poseJSON)
        {
            _atom = atom;
            _poseJSON = poseJSON;
        }

        public void Apply()
        {
            if (_atom.type != "Person") return;
            var posePresetsManagerControls = _atom.presetManagerControls.First(pmc => pmc.name == "PosePresets");
            var posePresetsManager = posePresetsManagerControls.GetComponent<PresetManager>();
            posePresetsManager.LoadPresetFromJSON(_poseJSON);
        }

        public JSONNode ToJSON()
        {
            return _poseJSON;
        }

        public AtomPose Clone()
        {
            return new AtomPose(_atom, _poseJSON);
        }
    }
}
