using System.Collections;
using System.Collections.Generic;

namespace VamTimeline
{
    public class AnimationTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(EmptyAnimation), EmptyAnimation);
        }

        public IEnumerable EmptyAnimation(TestContext context)
        {
            context.Assert(context.animation.clips.Count, 1, "Only one clip");
            context.Assert(context.animation.clips.Count, 1, "Only one clip state");
            context.animation.PlayClip(context.animation.GetDefaultClip(), true);
            yield return 0f;
            context.Assert(context.animation.isPlaying, "Play should set isPlaying to true");
            context.Assert(context.animation.clips[0].playbackEnabled, "Clips is enabled");
            context.animation.StopAll();
            yield return 0f;
            context.Assert(!context.animation.isPlaying, "Stop should set isPlaying to false");
            context.Assert(!context.animation.clips[0].playbackEnabled, "Clip is disabled");
        }
    }
}
