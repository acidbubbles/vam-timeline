using System.Collections.Generic;
using System.Text;

namespace VamTimeline
{
    public class DiagnosticsScreen : ScreenBase
    {
        public const string ScreenName = "Diagnostics";

        public override string screenId => ScreenName;

        private JSONStorableString _resultJSON;
        private readonly StringBuilder _resultBuffer = new StringBuilder();
        private readonly HashSet<string> _versions = new HashSet<string>();

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            _resultJSON = new JSONStorableString("Result", "Analyzing...");

            DoAnalysis();
        }

        private void DoAnalysis()
        {
            _resultBuffer.Clear();
            _versions.Clear();
            _resultBuffer.AppendLine("<b>INSTANCES</b>");

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

            var issues = 0;

            if (_versions.Count > 1)
            {
                _resultBuffer.AppendLine($"{++issues} More than one version were found");
            }

            if (issues == 0)
            {
                _resultBuffer.AppendLine($"No issues found");
            }

            _resultBuffer.AppendLine("Done");
            _resultJSON.val = _resultBuffer.ToString();
            _resultBuffer.Clear();
        }

        private void DoAnalysisTimeline(JSONStorable timelineStorable)
        {
            _resultBuffer.AppendLine($"Timeline: {timelineStorable}");
        }

        private void DoAnalysisController(JSONStorable controllerStorable)
        {
            _resultBuffer.AppendLine($"Controller: {controllerStorable}");
        }

        #endregion
    }
}

