namespace VamTimeline
{
    public class FreeControllerV3Ref : AnimatableRefBase, IAnimatableRefWithTransform
    {
        public readonly bool owned;
        public readonly string lastKnownAtomUid;
        public readonly string lastKnownControllerName;
        public readonly FreeControllerV3 controller;
        public readonly JSONStorableFloat weightJSON;
        public float scaledWeight = 1f;

        public FreeControllerV3Ref(FreeControllerV3 controller, bool owned)
        {
            this.controller = controller;
            if (!owned)
                lastKnownAtomUid = controller.containingAtom.uid;
            lastKnownControllerName = controller.name;
            this.owned = owned;
            var weightJSONName = owned
                ? $"Controller Weight {controller.name}"
                : $"External Controller Weight {controller.containingAtom.name} / {controller.name}";
            weightJSON = new JSONStorableFloat(weightJSONName, 1f, val => scaledWeight = val.ExponentialScale(0.1f, 1f), 0f, 1f)
            {
                isStorable = false
            };
        }

        public bool selectedPosition { get; set; }
        public bool selectedRotation { get; set; }

        public override string name
        {
            get
            {
                if (!owned && controller == null)
                    return "[Missing]";

                return controller.name;
            }
        }

        public override object groupKey => controller != null ? (object)controller.containingAtom : 0;

        public override string groupLabel => owned
            ? "Controls"
            : $"{(controller != null ? controller.containingAtom.name : lastKnownAtomUid)} controls";

        #warning Mark with or without rotation
        public override string GetShortName()
        {
            if (!owned && controller == null)
                return $"[Missing: {lastKnownControllerName}]";

            return controller.name.EndsWith("Control")
                ? controller.name.Substring(0, controller.name.Length - "Control".Length)
                : controller.name;
        }

        public override string GetFullName()
        {
            if (!owned)
            {
                if (controller == null)
                    return $"[Missing: {lastKnownAtomUid} {lastKnownControllerName}]";
                return $"{controller.containingAtom.name} {controller.name}";
            }

            return controller.name;
        }

        public bool Targets(FreeControllerV3 otherController)
        {
            return controller == otherController;
        }
    }
}
