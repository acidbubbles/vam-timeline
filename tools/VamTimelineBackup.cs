using System;
using System.Collections.Generic;

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
            CreateTextField(_backupJSON);
        }
        catch (Exception exc)
        {
            SuperController.LogError("VamTimelineBackup Init: " + exc);
        }
    }
}