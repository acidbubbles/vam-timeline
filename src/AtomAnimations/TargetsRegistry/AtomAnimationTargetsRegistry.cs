using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class AtomAnimationTargetsRegistry
    {
        public List<AtomAnimationStorableFloatParamTargetReference> storableFloats = new List<AtomAnimationStorableFloatParamTargetReference>();

        public AtomAnimationStorableFloatParamTargetReference GetOrCreateStorableFloat(Atom atom, string storableId, string floatParamName)
        {
            var s = storableFloats.FirstOrDefault(x => x.Targets(storableId, floatParamName));
            if (s != null) return s;
            s = new AtomAnimationStorableFloatParamTargetReference(atom, storableId, floatParamName);
            storableFloats.Add(s);
            return s;
        }

        public AtomAnimationStorableFloatParamTargetReference GetOrCreateStorableFloat(JSONStorable storable, JSONStorableFloat floatParam)
        {
            var s = storableFloats.FirstOrDefault(x => x.Targets(storable, floatParam));
            if (s != null) return s;
            s = new AtomAnimationStorableFloatParamTargetReference(storable, floatParam);
            storableFloats.Add(s);
            return s;
        }
    }
}
