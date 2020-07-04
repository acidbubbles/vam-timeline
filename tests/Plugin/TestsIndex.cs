using VamTimeline.Tests.Framework;
using VamTimeline.Tests.Specs;

namespace VamTimeline.Tests.Plugin
{
    public static class TestsIndex
    {
        public static TestsEnumerator GetAllTests()
        {
            return new TestsEnumerator(new ITestClass[]{
                new AnimationTests(),
                new ResizeAnimationOperationTests()
            });
        }
    }
}
