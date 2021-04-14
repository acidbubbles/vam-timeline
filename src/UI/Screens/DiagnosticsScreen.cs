using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace VamTimeline
{
    public class DiagnosticsScreen : ScreenBase
    {
        public const string ScreenName = "Diagnostics";

        public override string screenId => ScreenName;

        private JSONStorableString _resultJSON;
        private StringBuilder _resultBuffer;
        private readonly HashSet<string> _versions = new HashSet<string>();

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
            _resultBuffer = new StringBuilder();
            _versions.Clear();
            _resultBuffer.AppendLine("<b>INSTANCES</b>");
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
                _resultBuffer.AppendLine($"- <color=yellow>{++issues} More than one version were found</color>");
            }

            if (issues == 0)
            {
                _resultBuffer.AppendLine($"- No issues found");
            }

            _resultJSON.val = _resultBuffer.ToString();
            _resultBuffer = null;
        }

        private void DoAnalysisTimeline(JSONStorable timelineStorable)
        {
            var timelineScript = timelineStorable as MVRScript;
            if (timelineScript == null) return;
            var timelineJSON = timelineStorable.GetJSON();
            var pluginsJSON = timelineScript.manager.GetJSON();
            var pluginId = timelineScript.storeId.Substring(0, timelineScript.storeId.IndexOf("_", StringComparison.Ordinal));
            var path = pluginsJSON["plugins"][pluginId].Value;
            var regex = new Regex(@"^[^.]+\.^[^.]+\.([0-9]+):/");
            var match = regex.Match(path);
            string version;
            if (!match.Success)
            {
                version = path;
            }
            else
            {
                version = match.Groups[1].Value;
            }

            _resultBuffer.AppendLine($"<b>{timelineScript.containingAtom.uid} {pluginId}</b>");
            _resultBuffer.AppendLine($"- version: {version}");
            if(!match.Success)
                _resultBuffer.AppendLine($"- <color=yellow>Not from a known var package</color>");
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

