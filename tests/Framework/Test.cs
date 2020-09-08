using System;
using System.Collections;
using System.Text;
using UnityEngine;

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
            var go = new GameObject();
            go.transform.SetParent(testPlugin.gameObject.transform, false);

            var animation = go.AddComponent<AtomAnimation>();
            animation.AddClip(new AtomAnimationClip("Anim 1", AtomAnimationClip.DefaultAnimationLayer));
            animation.RebuildAnimationNow();

            var context = new TestContext(go, output, animation);

            foreach (var x in _run(context))
                yield return x;

            UnityEngine.Object.Destroy(go);
        }
    }
}
