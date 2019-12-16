using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VaM Utilities
/// By Acidbubbles
/// Control for sliding between values
/// Source: https://github.com/acidbubbles/vam-utilities
/// </summary>
public class VamTimelineController : MVRScript
{
    public override void Init()
    {
        try
        {
        }
        catch (Exception exc)
        {
            SuperController.LogError("VamTimelineController Enable: " + exc);
        }
    }

    public void OnEnable()
    {
        try
        {
        }
        catch (Exception exc)
        {
            SuperController.LogError("VamTimelineController Enable: " + exc);
        }
    }

    public void OnDisable()
    {
        try
        {
        }
        catch (Exception exc)
        {
            SuperController.LogError("VamTimelineController Disable: " + exc);
        }
    }

    public void OnDestroy()
    {
        OnDisable();
    }
}