using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SimpleJSON;

namespace VamTimeline
{
    public class DiagnosticsScreen : ScreenBase
    {
        public const string ScreenName = "Diagnostics";

        public override string screenId => ScreenName;

        private JSONStorableString _resultJSON;
        private StringBuilder _resultBuffer = new StringBuilder();
        private HashSet<string> _versions = new HashSet<string>();
        private readonly List<InstanceSettings> _instances = new List<InstanceSettings>();
        private readonly List<ClipSettings> _clips = new List<ClipSettings>();

        private class InstanceSettings
        {
            public string atomId;
            public string pluginId;
            public string version;
            public bool master;
            public int timeMode;
            public float speed;

            public string label => $"{atomId} {pluginId}";
        }

        private class ClipSettings
        {
            public bool loop;
            public float speed;
            public float animationLength;
            public bool preserveLoop;
            public string animationName;
        }

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            _resultJSON = new JSONStorableString("Result", "Analyzing...");
            prefabFactory.CreateTextField(_resultJSON).height = 1200f;

            DoAnalysis();
        }

        private void DoAnalysis()
        {
            _resultBuffer.Length = 0;
            _versions.Clear();
            _instances.Clear();
            _clips.Clear();

            _resultBuffer.AppendLine("<b>SCAN RESULT</b>");
            _resultBuffer.AppendLine();

            foreach (var atom in SuperController.singleton.GetAtoms())
            {
                foreach (var storableId in atom.GetStorableIDs())
                {
                    if (storableId.EndsWith("VamTimeline.AtomPlugin"))
                    {
                        DoAnalysisTimeline(atom.GetStorableByID(storableId));
                    }
                    if (storableId.EndsWith("VamTimeline.ControllerPlugin"))
                    {
                        DoAnalysisController(atom.GetStorableByID(storableId));
                    }
                }
            }

            _resultBuffer.AppendLine("<b>ISSUES</b>");
            _resultBuffer.AppendLine();

            var issues = 0;

            if (_versions.Count > 1)
                _resultBuffer.AppendLine($"- [{++issues}] <color=yellow>More than one version were found</color>");

            if (_instances.Count(i => i.master) > 1)
                _resultBuffer.AppendLine($"- [{++issues}] <color=red>More than one 'master' found. Only one Timeline instance can have the master option enabled.</color>");

            if (_instances.GroupBy(i => i.timeMode).Count() > 1)
                _resultBuffer.AppendLine($"- [{++issues}] <color=yellow>Different time modes used. This will cause uneven delays in animations under load.</color>");

            if(_instances.GroupBy(i => i.speed).Count() > 1)
                _resultBuffer.AppendLine($"- [{++issues}] <color=yellow>Different global speeds are used; animations will be out of sync and transitions may run at unexpected times</color>");

            var clips = _clips.GroupBy(a => a.animationName);

            foreach (var clip in clips)
            {
                if(clip.GroupBy(c => c.loop).Count() > 1)
                    _resultBuffer.AppendLine($"- [{++issues}] <color=yellow>Clip '{clip.Key}' is synced and has both looping and non-looping instances; a non looping animation can halt other animations when it stops.</color>");

                if(clip.GroupBy(c => c.speed).Count() > 1)
                    _resultBuffer.AppendLine($"- [{++issues}] <color=yellow>Clip '{clip.Key}' is synced and has different local speeds; animations will be out of sync and transitions may run at unexpected times</color>");

                if(clip.GroupBy(c => c.animationLength).Count() > 1)
                    _resultBuffer.AppendLine($"- [{++issues}] <color=yellow>Clip '{clip.Key}' is synced and has different animation lengths; loops will not be synchronized</color>");

                if(clip.GroupBy(c => c.preserveLoop).Count() > 1)
                    _resultBuffer.AppendLine($"- [{++issues}] <color=yellow>Clip '{clip.Key}' is synced and has different preserve loop settings; loops will not be synchronized</color>");
            }

            if (issues == 0)
            {
                _resultBuffer.AppendLine($"- No issues found");
            }

            _resultJSON.val = _resultBuffer.ToString();

            _resultBuffer = null;
            _versions = null;
            _instances.Clear();
            _clips.Clear();
        }

        private void DoAnalysisTimeline(JSONStorable timelineStorable)
        {
            var timelineScript = timelineStorable as MVRScript;
            if (timelineScript == null) return;
            var timelineJSON = timelineStorable.GetJSON()["Animation"];
            var pluginsJSON = timelineScript.manager.GetJSON();
            var pluginId = timelineScript.storeId.Substring(0, timelineScript.storeId.IndexOf("_", StringComparison.Ordinal));
            var path = pluginsJSON["plugins"][pluginId].Value;
            var regex = new Regex(@"^[^.]+\.[^.]+\.([0-9]+):/");
            var match = regex.Match(path);
            var syncWithPeers = timelineJSON["SyncWithPeers"].Value != "0";

            var instanceInfo = new InstanceSettings
            {
                atomId = timelineStorable.containingAtom.uid,
                pluginId = pluginId,
                master = timelineJSON["Master"].Value == "1",
                version = !match.Success ? path : match.Groups[1].Value,
                timeMode = timelineJSON["TimeMode"].AsInt,
                speed = timelineJSON["Speed"].AsInt,
            };
            _instances.Add(instanceInfo);

            var clipsJSON = timelineJSON["Clips"].AsArray;
            _resultBuffer.AppendLine($"<b>{instanceInfo.label}</b>");
            _resultBuffer.AppendLine($"- version: {instanceInfo.version}");
            if(!match.Success)
                _resultBuffer.AppendLine($"- <color=yellow>Not from a known var package</color>");
            _resultBuffer.AppendLine($"- clips: {clipsJSON.Count}");

            foreach (JSONClass clipJSON in clipsJSON)
            {
                var animationName = clipJSON["AnimationName"].Value;

                var animationInfo = new ClipSettings
                {
                    animationName = animationName,
                    loop = clipJSON["Loop"].Value == "1",
                    speed = clipJSON["Speed"].AsFloat,
                    animationLength = clipJSON["AnimationLength"].AsFloat,
                    preserveLoop = clipJSON["SyncTransitionTime"].AsBool,
                };

                if (animationInfo.speed == 0)
                    _resultBuffer.AppendLine($"- <color=yellow>Clip '{animationInfo.animationName}' has a speed of zero, which means it will not play</color>");

                if (syncWithPeers)
                    _clips.Add(animationInfo);
            }

            _resultBuffer.AppendLine();
        }

        private void DoAnalysisController(JSONStorable controllerStorable)
        {
            _resultBuffer.AppendLine($"<b>Controller: {controllerStorable}</b>");
            _resultBuffer.AppendLine();
        }

        #endregion
    }
}

