using System.Collections;
using System.Diagnostics;
using System.Text;
using VamTimeline.Tests.Framework;

namespace VamTimeline.Tests.Plugin
{
    public class TestPlugin : MVRScript
    {
        private StringBuilder _resultBuilder;
        private JSONStorableString _resultJSON;
        private UIDynamicButton _runUI;

        public override void Init()
        {
            base.Init();

            _runUI = CreateButton("Run", false);
            _runUI.button.onClick.AddListener(Run);

            _resultJSON = new JSONStorableString("Test Results", "Running...");

            Run();
        }

        public override void InitUI()
        {
            base.InitUI();
            if (UITransform == null) return;
            var scriptUI = UITransform.GetComponentInChildren<MVRScriptUI>();

            _runUI.transform.SetParent(scriptUI.fullWidthUIContent.transform, false);

            var resultUI = CreateTextField(_resultJSON, true);
            resultUI.height = 800f;
            resultUI.transform.SetParent(scriptUI.fullWidthUIContent.transform, false);
        }

        private void Run()
        {
            _runUI.enabled = false;
            _resultBuilder = new StringBuilder();
            _resultJSON.val = "Running...";
            pluginLabelJSON.val = "Running...";
            StartCoroutine(RunDeferred());
        }

        private IEnumerator RunDeferred()
        {
            var globalSW = Stopwatch.StartNew();
            var success = true;
            yield return 0;
            foreach (Test test in TestsIndex.GetAllTests())
            {
                var output = new StringBuilder();
                var sw = Stopwatch.StartNew();
                foreach (var x in test.Run(this, output))
                    yield return x;
                if (output.Length == 0)
                {
                    _resultBuilder.AppendLine($"{test.name} PASS {sw.ElapsedMilliseconds:0}ms");
                    _resultJSON.val = _resultBuilder.ToString();
                }
                else
                {

                    _resultBuilder.AppendLine($"{test.name} FAIL {sw.ElapsedMilliseconds:0}ms]");
                    _resultBuilder.AppendLine(output.ToString());
                    _resultJSON.val = _resultBuilder.ToString();
                    _resultBuilder.AppendLine($"FAIL [{globalSW.Elapsed}]");
                    success = false;
                    break;
                }
            }
            globalSW.Stop();
            _resultBuilder.AppendLine($"DONE {globalSW.Elapsed}");
            _resultJSON.val = _resultBuilder.ToString();
            pluginLabelJSON.val = (success ? "Success" : "Failed") + $" (ran in {globalSW.Elapsed.TotalSeconds:0.00}s)";
        }
    }
}

