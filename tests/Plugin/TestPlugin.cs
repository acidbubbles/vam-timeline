using System.Collections;
using System.Diagnostics;
using System.Text;
using VamTimeline.Tests.Framework;

namespace VamTimeline.Tests.Plugin
{
    public class TestPlugin : MVRScript
    {
        private StringBuilder _resultBuilder;
        private JSONStorableString _testFilterJSON;
        private JSONStorableString _resultJSON;
        private UIDynamicButton _runUI;

        public override void Init()
        {
            base.Init();

            _testFilterJSON = new JSONStorableString("Test Filter", "");
            _resultJSON = new JSONStorableString("Test Results", "Running...");

            _runUI = CreateButton("Run", false);
            _runUI.button.onClick.AddListener(Run);

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
            var counter = 0;
            yield return 0;
            foreach (Test test in TestsIndex.GetAllTests())
            {
                if (!string.IsNullOrEmpty(_testFilterJSON.val) && !test.name.Contains(_testFilterJSON.val)) continue;

                var output = new StringBuilder();
                var sw = Stopwatch.StartNew();
                foreach (var x in test.Run(this, output))
                    yield return x;
                counter++;
                if (output.Length == 0)
                {
                    if (sw.ElapsedMilliseconds > 20)
                    {
                        _resultBuilder.AppendLine($"{test.name} PASS {sw.ElapsedMilliseconds:0}ms (LONG)");
                        _resultJSON.val = _resultBuilder.ToString();
                    }
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
            _resultBuilder.AppendLine($"{(success ? "SUCCESS" : "FAIL")}; ran {counter} tests in {globalSW.Elapsed}");
            _resultJSON.val = _resultBuilder.ToString();
            pluginLabelJSON.val = (success ? "Success" : "Failed") + $" (ran in {globalSW.Elapsed.TotalSeconds:0.00}s)";
        }
    }
}

