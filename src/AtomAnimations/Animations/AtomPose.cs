using System.Linq;
using MeshVR;
using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    public class AtomPose
    {
        public const bool DefaultIncludeRoot = true;
        public const bool DefaultIncludePose = true;
        public const bool DefaultIncludeMorphs = true;

        public bool includeRoot { get; private set; }
        public bool includePose { get; private set; }
        public bool includeMorphs { get; private set; }

        private readonly Atom _atom;
        private readonly JSONClass _poseJSON;

        public static AtomPose FromAtom(Atom atom, AtomPose inheritSettingsFrom)
        {
            if (inheritSettingsFrom != null)
                return FromAtom(atom, inheritSettingsFrom.includeRoot, inheritSettingsFrom.includePose, inheritSettingsFrom.includeMorphs);
            else
                return FromAtom(atom, DefaultIncludeRoot, DefaultIncludePose, DefaultIncludeMorphs);
        }

        public static AtomPose FromAtom(Atom atom, bool includeRoot, bool includePose, bool includeMorphs)
        {
            if (atom.type != "Person") return null;

            var storables = atom.GetStorableIDs()
                .Select(atom.GetStorableByID)
                .Where(t => !t.exclude && t.gameObject.activeInHierarchy)
                .Where(t => ShouldStorableBeIncluded(t, includeRoot, includePose, includeMorphs));
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

            return new AtomPose(atom, poseJSON)
            {
                includeRoot = includeRoot,
                includePose = includePose,
                includeMorphs = includeMorphs
            };
        }

        private static bool ShouldStorableBeIncluded(JSONStorable t, bool includeRoot, bool includePose, bool includeMorphs)
        {
            if (includePose && t is FreeControllerV3) return includeRoot || ((FreeControllerV3) t).name != "control";
            if (includePose && t is DAZBone) return true;
            if (includeMorphs && t.storeId == "geometry" && t is DAZCharacterSelector) return true;
            return false;
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

        public PositionAndRotation GetControllerPose(string name)
        {
            var storable = _poseJSON["storables"]?.Childs.FirstOrDefault(c => c["id"].Value == name)?.AsObject;
            if (storable == null || !storable.HasKey("localPosition") || !storable.HasKey("localRotation")) return null;
            var position = storable["localPosition"].AsObject;
            var rotation = storable["localRotation"].AsObject;
            return new PositionAndRotation
            {
                position = new Vector3(
                    position["x"].AsFloat,
                    position["y"].AsFloat,
                    position["z"].AsFloat
                ),
                rotation = new Vector3(
                    rotation["x"].AsFloat,
                    rotation["y"].AsFloat,
                    rotation["z"].AsFloat
                )
            };
        }

        public class PositionAndRotation
        {
            public Vector3 position;
            public Vector3 rotation;
        }

        public JSONNode ToJSON()
        {
            return _poseJSON;
        }

        public AtomPose Clone()
        {
            // NOTE: We don't deep clone because the pose JSON is immutable currently
            return new AtomPose(_atom, _poseJSON)
            {
                includeMorphs = includeMorphs,
                includePose = includePose,
                includeRoot = includeRoot
            };
        }
    }
}
