namespace VamTimeline
{
    public class FreeControllerV3Ref : AnimatableRefBase
    {
        public readonly FreeControllerV3 controller;

        public FreeControllerV3Ref(FreeControllerV3 controller)
        {
            this.controller = controller;
        }

        public override string name => controller.name;

        public override string GetShortName()
        {
            return controller.name.EndsWith("Control")
                ? controller.name.Substring(0, controller.name.Length - "Control".Length)
                : controller.name;
        }

        public bool Targets(string controllerName)
        {
            return controller.name == controllerName;
        }

        public bool Targets(FreeControllerV3 otherController)
        {
            return controller == otherController;
        }
    }
}
