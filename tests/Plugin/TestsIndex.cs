using VamTimeline.Tests.Framework;
using VamTimeline.Tests.Specs;
using VamTimeline.Tests.Unit;

namespace VamTimeline.Tests.Plugin
{
    public static class TestsIndex
    {
        public static TestsEnumerator GetAllTests()
        {
            return new TestsEnumerator(new ITestClass[]{
                new AnimationCurveExtensionsTests(),
                new AnimationTests(),
                new ResizeAnimationOperationTests(),
                new ImportOperationsTests(),
            });
        }
    }
}
