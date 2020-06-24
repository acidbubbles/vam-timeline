using System.Collections.Generic;
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
        public static IEnumerable<Test> GetAllTests()
        {
            yield return new Test(nameof(AnimationTests.EmptyAnimation), new AnimationTests().EmptyAnimation);
        }
    }
}
