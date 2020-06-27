using System.Collections;
using System.Diagnostics;
using System.Text;

namespace VamTimeline.Tests.Plugin
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
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
            var resultUI = CreateTextField(_resultJSON, true);
            resultUI.height = 800f;

            Run();
        }

        private void Run()
        {
            _runUI.enabled = false;
            _resultBuilder = new StringBuilder();
            _resultJSON.val = "Running...";
            StartCoroutine(RunDeferred());
        }

        private IEnumerator RunDeferred()
        {
            var globalSW = Stopwatch.StartNew();
            yield return 0;
            foreach (var test in TestsIndex.GetAllTests())
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
                }
            }
            _resultBuilder.AppendLine($"DONE {globalSW.Elapsed}");
            _resultJSON.val = _resultBuilder.ToString();
        }
    }
}

