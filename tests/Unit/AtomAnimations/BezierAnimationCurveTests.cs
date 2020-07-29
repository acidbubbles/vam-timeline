using System.Collections;
using System.Collections.Generic;
using VamTimeline.Tests.Framework;

namespace VamTimeline.Tests.Unit
{
    public class BezierAnimationCurveTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(AddAndRemoveFrames), AddAndRemoveFrames);
        }

        public IEnumerable AddAndRemoveFrames(TestContext context)
        {
            var curve = new BezierAnimationCurve();

            {
                var key = curve.SetKeyframe(0, 123);
                if (!context.Assert(key, 0, "First key is zero")) yield break;
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0)).value, 123, "Set and get at time 0");
            }

            {
                var key = curve.SetKeyframe(0.499999f, 456);
                if (!context.Assert(key, 1, "Second key is one")) yield break;
                context.Assert(curve.GetKeyframe(curve.KeyframeBinarySearch(0.499999f)).value, 456, "Set and get at time 0.499999");
            }

            yield break;
        }
    }
}
