using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace VamTimeline
{
    [SuppressMessage("ReSharper", "RedundantJumpStatement")]
    public class BezierAnimationCurveTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(AddAndRemoveFrames), AddAndRemoveFrames);
            yield return new Test(nameof(KeyframeBinarySearch), KeyframeBinarySearch);
            yield return new Test(nameof(EvaluateLinear), EvaluateLinear);
            yield return new Test(nameof(EvaluateSmoothGlobalLooping), EvaluateSmoothGlobalLooping);
            yield return new Test(nameof(EvaluateSmoothGlobalNonLooping), EvaluateSmoothGlobalNonLooping);
            yield return new Test(nameof(RepairBrokenCurve), RepairBrokenCurve);
        }

        public IEnumerable AddAndRemoveFrames(TestContext context)
        {
            var curve = new BezierAnimationCurve();

            {
                var key = curve.SetKeyframe(0, 123, CurveTypeValues.Linear);
                if (!context.Assert(key, 0, "First key is zero")) yield break;
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f }, "Expected one frame")) yield break;
                context.Assert(curve.GetKeyframeByKey(curve.KeyframeBinarySearch(0)).value, 123, "Set and get at time 0");
            }

            {
                var key = curve.SetKeyframe(0.499999f, 456, CurveTypeValues.Linear);
                if (!context.Assert(key, 1, "Second key is one")) yield break;
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 0.5f }, "Expected two frames")) yield break;
                context.Assert(curve.GetKeyframeByKey(curve.KeyframeBinarySearch(0.000001f)).value, 123, "Set and get at time 0.000001");
                context.Assert(curve.GetKeyframeByKey(curve.KeyframeBinarySearch(0.499999f)).value, 456, "Set and get at time 0.499999");
            }

            {
                var key = curve.SetKeyframe(0.250f, 789, CurveTypeValues.Linear);
                if (!context.Assert(key, 1, "Third key is one")) yield break;
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 0.250f, 0.5f }, "Expected three frames")) yield break;
                context.Assert(curve.GetKeyframeByKey(curve.KeyframeBinarySearch(0.000001f)).value, 123, "Set and get at time 0.000001");
                context.Assert(curve.GetKeyframeByKey(curve.KeyframeBinarySearch(0.250f)).value, 789, "Set and get at time 0.250f");
                context.Assert(curve.GetKeyframeByKey(curve.KeyframeBinarySearch(0.499999f)).value, 456, "Set and get at time 0.499999");
            }

            {
                curve.RemoveKey(1);
                if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 0.5f }, "Expected two frames after remove")) yield break;
            }

            yield break;
        }

        public IEnumerable KeyframeBinarySearch(TestContext context)
        {
            var curve = new BezierAnimationCurve();
            curve.SetKeyframe(0, 10, CurveTypeValues.Linear);
            curve.SetKeyframe(1, 20, CurveTypeValues.Linear);
            curve.SetKeyframe(2, 30, CurveTypeValues.Linear);

            if (!context.Assert(curve.KeyframeBinarySearch(0.0f), 0, "0.0f")) yield break;
            if (!context.Assert(curve.KeyframeBinarySearch(0.5f, true), 1, "0.5f")) yield break;
            if (!context.Assert(curve.KeyframeBinarySearch(1.0f), 1, "1.0f")) yield break;
            if (!context.Assert(curve.KeyframeBinarySearch(1.5f, true), 2, "1.5f")) yield break;
            if (!context.Assert(curve.KeyframeBinarySearch(2.0f), 2, "2.0f")) yield break;
        }

        public IEnumerable EvaluateLinear(TestContext context)
        {
            var curve = new BezierAnimationCurve { loop = false };
            curve.SetKeyframe(0, 10, CurveTypeValues.Linear);
            curve.SetKeyframe(1, 20, CurveTypeValues.Linear);
            curve.SetKeyframe(2, 30, CurveTypeValues.Linear);

            if (!context.Assert(curve.Evaluate(0.0f), 10f, "Linear/0")) yield break;
            if (!context.Assert(curve.Evaluate(0.5f), 15f, "Linear/1")) yield break;
            if (!context.Assert(curve.Evaluate(1.0f), 20f, "Linear/2")) yield break;
            if (!context.Assert(curve.Evaluate(1.5f), 25f, "Linear/3")) yield break;
            if (!context.Assert(curve.Evaluate(2.0f), 30f, "Linear/4")) yield break;
        }

        public IEnumerable EvaluateSmoothGlobalLooping(TestContext context)
        {
            var curve = new BezierAnimationCurve { loop = true };
            curve.SetKeyframe(0, 100, CurveTypeValues.SmoothGlobal);
            curve.SetKeyframe(1, 200, CurveTypeValues.SmoothGlobal);
            curve.SetKeyframe(2, 300, CurveTypeValues.SmoothGlobal);
            curve.SetKeyframe(3, 200, CurveTypeValues.SmoothGlobal);
            curve.SetKeyframe(4, 100, CurveTypeValues.SmoothGlobal);
            curve.ComputeCurves();

            if (!context.Assert(curve.Evaluate(0.0f), 100f, "0.0")) yield break;
            if (!context.Assert(curve.Evaluate(0.5f), 131.25f, "0.5")) yield break;
            if (!context.Assert(curve.Evaluate(1.0f), 200f, "1.0")) yield break;
            if (!context.Assert(curve.Evaluate(1.5f), 268.75f, "1.5")) yield break;
            if (!context.Assert(curve.Evaluate(2.0f), 300f, "2.0")) yield break;
            if (!context.Assert(curve.Evaluate(2.5f), 268.75f, "2.5")) yield break;
            if (!context.Assert(curve.Evaluate(3.0f), 200f, "3.0")) yield break;
            if (!context.Assert(curve.Evaluate(3.5f), 131.25f, "3.5")) yield break;
            if (!context.Assert(curve.Evaluate(4.0f), 100f, "4.0")) yield break;
        }

        public IEnumerable EvaluateSmoothGlobalNonLooping(TestContext context)
        {
            var curve = new BezierAnimationCurve { loop = false };
            curve.SetKeyframe(0, 100, CurveTypeValues.SmoothGlobal);
            curve.SetKeyframe(1, 200, CurveTypeValues.SmoothGlobal);
            curve.SetKeyframe(2, 300, CurveTypeValues.SmoothGlobal);
            curve.SetKeyframe(3, 200, CurveTypeValues.SmoothGlobal);
            curve.SetKeyframe(4, 100, CurveTypeValues.SmoothGlobal);
            curve.ComputeCurves();

            if (!context.Assert(curve.Evaluate(0.0f), 100f, "0.0")) yield break;
            if (!context.Assert(curve.Evaluate(0.5f).Snap(0.001f), 144.643f, "0.5")) yield break;
            if (!context.Assert(curve.Evaluate(1.0f), 200f, "1.0")) yield break;
            if (!context.Assert(curve.Evaluate(1.5f).Snap(0.001f), 266.071f, "1.5")) yield break;
            if (!context.Assert(curve.Evaluate(2.0f), 300f, "2.0")) yield break;
            if (!context.Assert(curve.Evaluate(2.5f).Snap(0.001f), 266.071f, "2.5")) yield break;
            if (!context.Assert(curve.Evaluate(3.0f), 200f, "3.0")) yield break;
            if (!context.Assert(curve.Evaluate(3.5f).Snap(0.001f), 144.643f, "3.5")) yield break;
            if (!context.Assert(curve.Evaluate(4.0f), 100f, "4.0")) yield break;
        }

        public IEnumerable RepairBrokenCurve(TestContext context)
        {
            var curve = new BezierAnimationCurve();
            curve.SetKeyframe(1, 2, CurveTypeValues.Linear);
            curve.SetKeyframe(2, 3, CurveTypeValues.Linear);
            curve.SetKeyframe(3, 4, CurveTypeValues.Linear);
            if (!context.Assert(curve.keys.Select(k => k.time), new[] { 1f, 2f, 3f }, "Expected broken curve")) yield break;

            curve.AddEdgeFramesIfMissing(5f, CurveTypeValues.Linear);
            if (!context.Assert(curve.keys.Select(k => k.time), new[] { 0f, 1f, 2f, 3f, 5f }, "Expected repaired curve")) yield break;
        }
    }
}
