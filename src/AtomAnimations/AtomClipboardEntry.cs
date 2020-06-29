using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    public class AtomClipboard
    {
        public float time;
        public readonly IList<AtomClipboardEntry> entries = new List<AtomClipboardEntry>();

        public void Clear()
        {
            time = 0f;
            entries.Clear();
        }
    }

    public class AtomClipboardEntry
    {
        public float time;
        public List<FreeControllerV3ClipboardEntry> controllers;
        public List<FloatParamValClipboardEntry> floatParams;
        public List<TriggersClipboardEntry> triggers;
    }

    public class FloatParamValClipboardEntry
    {
        public JSONStorable storable;
        public JSONStorableFloat floatParam;
        public Keyframe snapshot;
    }

    public class FreeControllerV3ClipboardEntry
    {
        public FreeControllerV3 controller;
        public FreeControllerV3Snapshot snapshot;
    }

    public class FreeControllerV3Snapshot
    {
        public Keyframe x;
        public Keyframe y;
        public Keyframe z;
        public Keyframe rotX;
        public Keyframe rotY;
        public Keyframe rotZ;
        public Keyframe rotW;

        public string curveType;
    }

    public class TriggersClipboardEntry
    {
        public string name;
        public JSONClass json;
    }
}
