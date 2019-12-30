using System;

namespace AcidBubbles.VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class Backup : MVRScript
    {
        private JSONStorableString _backupJSON;

        public override void Init()
        {
            try
            {
                _backupJSON = new JSONStorableString("Backup", "");
                RegisterString(_backupJSON);
                var textfield = CreateTextField(_backupJSON);
                textfield.height = 1200;
            }
            catch (Exception exc)
            {
                SuperController.LogError("Backup Init: " + exc);
            }
        }
    }
}
