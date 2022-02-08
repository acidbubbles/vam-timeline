using System.Linq;

namespace VamTimeline
{
    public class FreeControllerV3Ref : AnimatableRefBase
    {
        public readonly FreeControllerV3 controller;
        public readonly Atom parentAtom;
        public readonly bool subscene;

        public FreeControllerV3Ref(FreeControllerV3 controller, bool subscene = false)
        {
            this.controller = controller;
            this.parentAtom = controller.containingAtom;
            this.subscene = subscene;
        }

        public override string name => subscene ? controller.name + "("+ this.parentAtom.name+")" : controller.name;

        protected string extractEffectorName(string atomName)
        {
            string effectorName = atomName.Contains('/') ? atomName.Split('/')[1] : atomName;
            if (effectorName.ToLower().Contains("effector") || effectorName.ToLower().Contains("bendgoal"))
            {
                if (effectorName.Contains("&")) //Full body effector
                {
                    string[] tokens = effectorName.Split('&');
                    if (tokens.Length > 2)
                    {
                        effectorName = tokens[1];
                    }
                }
                else if (effectorName.Contains("_")) //Fabbrik/LimbIK/CCD Effector
                {
                    string[] tokens = effectorName.Split('_');
                    if (tokens.Length > 2)
                    {
                        effectorName = tokens[2] + " " + tokens[0];
                    }
                }

            }

            return effectorName;
        }

        public override string GetShortName()
        {
            return  controller.name.EndsWith("Control")
                ? controller.name.Substring(0, controller.name.Length - "Control".Length) 
                : subscene ? extractEffectorName(this.parentAtom.name) : controller.name ;
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
