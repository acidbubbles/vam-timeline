using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class PluginImplBase
    {
        protected readonly IAnimationPlugin _plugin;

        protected PluginImplBase(IAnimationPlugin plugin)
        {
            _plugin = plugin;
        }
    }
}
