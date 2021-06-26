namespace VamTimeline
{
    public class FreeControllerV3Ref : AnimatableRefBase
    {
        public readonly FreeControllerV3 controller;

        public FreeControllerV3Ref(FreeControllerV3 controller)
        {
            this.controller = controller;
        }

        public string GetShortName()
        {
            if (controller.name.EndsWith("Control"))
                return controller.name.Substring(0, controller.name.Length - "Control".Length);
            return controller.name;
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
