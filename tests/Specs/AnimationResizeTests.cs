using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VamTimeline.Tests.Framework;

namespace VamTimeline.Tests.Specs
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AnimationResizeTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(StretchLonger), StretchLonger);
        }

        public IEnumerable StretchLonger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframes(context, clip);

            new OperationsFactory(clip).Resize().StretchLength(4f);

            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 2f, 4f }, "Keyframes after resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 2000, 4000 }, "Settings after resize");
            yield break;
        }

        public IEnumerable StretchShorter(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframes(context, clip);

            new OperationsFactory(clip).Resize().StretchLength(1f);

            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 0.5f, 1f }, "Keyframes after resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 500, 1000 }, "Settings after resize");
            yield break;
        }

        private static FreeControllerAnimationTarget GivenThreeKeyframes(TestContext context, AtomAnimationClip clip)
        {
            var target = clip.Add(new FreeControllerV3());
            target.SetKeyframe(0f, Vector3.zero, Quaternion.identity);
            target.SetKeyframe(1f, Vector3.one, Quaternion.identity);
            target.SetKeyframe(2f, Vector3.zero, Quaternion.identity);
            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 1f, 2f }, "Keyframes before resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000, 2000 }, "Settings before resize");
            return target;
        }
    }
}
