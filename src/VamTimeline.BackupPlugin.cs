using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class BackupPlugin : MVRScript
    {
        private bool _locked;
        private JSONStorableString _backupJSON;
        private JSONStorableString _backupHistoryJSON;

        public override void Init()
        {
            try
            {
                {
                    // Actual backup
                    _backupJSON = new JSONStorableString(StorableNames.AtomAnimationBackup, "");
                    RegisterString(_backupJSON);

                    // Backup history
                    _backupHistoryJSON = new JSONStorableString("History", "Loading...")
                    {
                        isStorable = false
                    };
                    var lastBackupUI = CreateTextField(_backupHistoryJSON, true);
                    lastBackupUI.height = 40f;

                    // Pushed backups from atom
                    var updateBackupJSON = new JSONStorableString(StorableNames.PushAtomAnimationBackup, "", val =>
                    {
                        if (!_locked)
                        {
                            _backupJSON.val = val;
                            _backupHistoryJSON.val = $"{DateTime.Now.ToLongTimeString()} -> {val.Length} characters.";
                        }
                    })
                    {
                        isStorable = false
                    };
                    RegisterString(updateBackupJSON);

                    // Keep current backup for the current session
                    var lockedJSON = new JSONStorableBool("Session Locked", false, (bool val) => _locked = val)
                    {
                        isStorable = false
                    };
                    CreateToggle(lockedJSON, true);

                    StartCoroutine(InitDeferred());
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.BackupPlugin.Init: " + exc);
            }
        }

        private IEnumerator InitDeferred()
        {
            yield return new WaitForEndOfFrame();
            _backupHistoryJSON.val = !string.IsNullOrEmpty(_backupJSON.val) ? "Restored backup from save" : "No backup yet";
        }
    }
}
