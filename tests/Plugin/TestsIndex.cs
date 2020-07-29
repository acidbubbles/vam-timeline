using VamTimeline.Tests.Framework;

namespace VamTimeline.Tests.Plugin
{
    public static class TestsIndex
    {
        public static TestsEnumerator GetAllTests()
        {
            return new TestsEnumerator(new ITestClass[]{
                new Unit.BezierAnimationCurveTests(),
                new Specs.AnimationTests(),
                new Specs.ResizeAnimationOperationTests(),
                new Specs.ImportOperationsTests(),
            });
        }
    }
}
