namespace VamTimeline
{
    public abstract class AddScreenBase : ScreenBase
    {
        private static bool _previousCreateInOtherAtoms;
        protected JSONStorableBool createInOtherAtoms;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton($"<b><</b> <i>Back to {AddAnimationsScreen.ScreenName}</i>", AddAnimationsScreen.ScreenName);
        }

        protected void InitCreateInOtherAtomsUI()
        {
            createInOtherAtoms = new JSONStorableBool("Create in other atoms", _previousCreateInOtherAtoms, val => _previousCreateInOtherAtoms = val);
            prefabFactory.CreateToggle(createInOtherAtoms);
        }

        #endregion
    }
}
