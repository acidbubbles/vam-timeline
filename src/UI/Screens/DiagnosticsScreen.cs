using System;
using System.Collections.Generic;
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
        private StringBuilder _resultBuffer;
        private HashSet<string> _versions;
        private Dictionary<string, bool> _looping;
        private HashSet<string> _loopingMismatches;
        private int _masters;

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
            _masters = 0;
            _resultBuffer = new StringBuilder();
            _versions = new HashSet<string>();
            _looping = new Dictionary<string, bool>();
            _loopingMismatches = new HashSet<string>();

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
            {
                _resultBuffer.AppendLine($"- [{++issues}] <color=yellow>More than one version were found</color>");
            }

            if (_masters > 0)
            {
                _resultBuffer.AppendLine($"- [{++issues}] <color=red>More than one 'master' found. Only one Timeline instance can have the master option enabled.</color>");
            }

            foreach (var loopingMismatch in _loopingMismatches)
            {
                _resultBuffer.AppendLine($"- [{++issues}] <color=red>Clip '{loopingMismatch}' is shared between multiple instances and does not have the same value for 'loop'. This will cause the looping clip to stop when the non-looping clip stops.</color>");
            }

            if (issues == 0)
            {
                _resultBuffer.AppendLine($"- No issues found");
            }

            _resultJSON.val = _resultBuffer.ToString();

            _masters = 0;
            _resultBuffer = null;
            _versions = null;
            _looping = null;
            _loopingMismatches = null;
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
            var version = !match.Success ? path : match.Groups[1].Value;
            var master = timelineJSON["Master"].Value == "1";
            if (master) _masters++;
            var syncWithPeers = timelineJSON["SyncWithPeers"].Value != "0";
            var clipsJSON = timelineJSON["Clips"].AsArray;
            string hasZeroSpeed = null;
            foreach (JSONClass clipJSON in clipsJSON)
            {
                var name = clipJSON["AnimationName"].Value;
                var loop = clipJSON["Loop"].Value == "1";
                var speed = clipJSON["Speed"].AsFloat;
                if (speed == 0) hasZeroSpeed = name;
                if (syncWithPeers)
                {
                    bool otherLoop;
                    if (_looping.TryGetValue(name, out otherLoop))
                    {
                        if (loop != otherLoop) _loopingMismatches.Add(name);
                    }
                    else
                    {
                        _looping.Add(name, loop);
                    }
                }
            }

            _resultBuffer.AppendLine($"<b>{timelineScript.containingAtom.uid} {pluginId}</b>");
            _resultBuffer.AppendLine($"- version: {version}");
            _resultBuffer.AppendLine($"- clips: {clipsJSON.Count}");
            if(!match.Success)
                _resultBuffer.AppendLine($"- <color=yellow>Not from a known var package</color>");
            if(hasZeroSpeed != null)
                _resultBuffer.AppendLine($"- <color=yellow>Clip '{hasZeroSpeed}' has a speed of zero, which means it will not play</color>");
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

