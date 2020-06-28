using VamTimeline.Tests.Framework;
using VamTimeline.Tests.Specs;

namespace VamTimeline.Tests.Plugin
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public static class TestsIndex
    {
        public static TestsEnumerator GetAllTests()
        {
            return new TestsEnumerator(new ITestClass[]{
                new AnimationTests(),
                new AnimationResizeTests()
            });
        }
    }
}
