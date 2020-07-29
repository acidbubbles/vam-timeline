using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VamTimeline.Tests.Framework;

namespace VamTimeline.Tests.Unit
{
    public class BezierAnimationCurveTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(AddAndRemoveFrames), AddAndRemoveFrames);
            yield return new Test(nameof(RepairBrokenCurveTests), RepairBrokenCurveTests);
        }

        public IEnumerable AddAndRemoveFrames(TestContext context)
        {
            var curve = new BezierAnimationCurve();

            {
                var key = curve.SetKeyframe(0, 123);
                if (!context.Assert(key, 0, "First key is zero")) yield break;
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f }, "Expected one frame")) yield break;
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0)).value, 123, "Set and get at time 0");
            }

            {
                var key = curve.SetKeyframe(0.499999f, 456);
                if (!context.Assert(key, 1, "Second key is one")) yield break;
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 0.5f }, "Expected two frames")) yield break;
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0.000001f)).value, 123, "Set and get at time 0.000001");
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0.499999f)).value, 456, "Set and get at time 0.499999");
            }

            {
                var key = curve.SetKeyframe(0.250f, 789);
                if (!context.Assert(key, 1, "Third key is one")) yield break;
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 0.250f, 0.5f }, "Expected three frames")) yield break;
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0.000001f)).value, 123, "Set and get at time 0.000001");
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0.250f)).value, 789, "Set and get at time 0.250f");
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0.499999f)).value, 456, "Set and get at time 0.499999");
            }

            {
                curve.RemoveKey(1);
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 0.5f }, "Expected two frames after remove")) yield break;
            }

            yield break;
        }

        public IEnumerable RepairBrokenCurveTests(TestContext context)
        {
            var curve = new BezierAnimationCurve();
            curve.SetKeyframe(1, 2);
            curve.SetKeyframe(2, 3);
            curve.SetKeyframe(3, 4);
            if (!context.Assert(curve.keys.Select(k => k.time), new[] { 1f, 2f, 3f }, "Expected broken curve")) yield break;

            curve.AddEdgeFramesIfMissing(5f);
            if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 1f, 2f, 3f, 5f }, "Expected repaired curve")) yield break;
        }
    }
}
