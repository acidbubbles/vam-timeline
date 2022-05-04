namespace VamTimeline
{
    public class FreeControllerV3Ref : AnimatableRefBase
    {
        public readonly bool owned;
        public readonly FreeControllerV3 controller;

        public FreeControllerV3Ref(FreeControllerV3 controller, bool owned)
        {
            this.controller = controller;
            this.owned = owned;
        }

        public override string name => controller.name;

        public override string GetShortName()
        {
            return controller.name.EndsWith("Control")
                ? controller.name.Substring(0, controller.name.Length - "Control".Length)
                : controller.name;
        }

        public bool Targets(FreeControllerV3 otherController)
        {
            return controller == otherController;
        }
    }
}
