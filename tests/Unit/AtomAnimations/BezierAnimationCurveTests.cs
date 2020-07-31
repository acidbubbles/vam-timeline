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
            yield return new Test(nameof(Evaluate), Evaluate);
            yield return new Test(nameof(RepairBrokenCurve), RepairBrokenCurve);
        }

        public IEnumerable AddAndRemoveFrames(TestContext context)
        {
            var curve = new BezierAnimationCurve();

            {
                var key = curve.SetKeyframe(0, 123, CurveTypeValues.Linear);
                if (!context.Assert(key, 0, "First key is zero")) yield break;
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f }, "Expected one frame")) yield break;
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0)).value, 123, "Set and get at time 0");
            }

            {
                var key = curve.SetKeyframe(0.499999f, 456, CurveTypeValues.Linear);
                if (!context.Assert(key, 1, "Second key is one")) yield break;
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 0.5f }, "Expected two frames")) yield break;
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0.000001f)).value, 123, "Set and get at time 0.000001");
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0.499999f)).value, 456, "Set and get at time 0.499999");
            }

            {
                var key = curve.SetKeyframe(0.250f, 789, CurveTypeValues.Linear);
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

        public IEnumerable Evaluate(TestContext context)
        {
            var curve = new BezierAnimationCurve();
            curve.SetKeyframe(0, 10, CurveTypeValues.Linear);
            curve.SetKeyframe(1, 20, CurveTypeValues.Linear);
            curve.SetKeyframe(2, 30, CurveTypeValues.Linear);

            if (!context.Assert(curve.Evaluate(0.0f), 10f, "Linear/0")) yield break;
            if (!context.Assert(curve.Evaluate(0.5f), 15f, "Linear/1")) yield break;
            if (!context.Assert(curve.Evaluate(1.0f), 20f, "Linear/2")) yield break;
            if (!context.Assert(curve.Evaluate(1.5f), 25f, "Linear/3")) yield break;
            if (!context.Assert(curve.Evaluate(2.0f), 30f, "Linear/4")) yield break;
        }

        public IEnumerable RepairBrokenCurve(TestContext context)
        {
            var curve = new BezierAnimationCurve();
            curve.SetKeyframe(1, 2, CurveTypeValues.Linear);
            curve.SetKeyframe(2, 3, CurveTypeValues.Linear);
            curve.SetKeyframe(3, 4, CurveTypeValues.Linear);
            if (!context.Assert(curve.keys.Select(k => k.time), new[] { 1f, 2f, 3f }, "Expected broken curve")) yield break;

            curve.AddEdgeFramesIfMissing(5f);
            if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 1f, 2f, 3f, 5f }, "Expected repaired curve")) yield break;
        }
    }
}
