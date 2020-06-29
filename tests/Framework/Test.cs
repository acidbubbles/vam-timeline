using System;
using System.Collections;
using System.Text;

namespace VamTimeline.Tests.Framework
{
    public class Test
    {
        public readonly string name;
        private readonly Func<TestContext, IEnumerable> _run;

        public Test(string name, Func<TestContext, IEnumerable> run)
        {
            this.name = name;
            _run = run;
        }

        public IEnumerable Run(MVRScript testPlugin, StringBuilder output)
        {
            var animation = testPlugin.gameObject.AddComponent<AtomAnimation>();
            animation.Initialize();
            var context = new TestContext(output, animation);
            foreach (var x in _run(context))
                yield return x;
        }
    }
}
