using System;
using System.Collections.Generic;

namespace AcidBubbles.VamTimeline.Tools
{
    /// <summary>
    /// VaM Timeline Controller
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class VamTimelineBackup : MVRScript
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
                SuperController.LogError("VamTimelineBackup Init: " + exc);
            }
        }
    }
}
