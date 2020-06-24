using System;
using System.Text;

namespace VamTimeline.Tests.Framework
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class TestContext
    {
        public readonly StringBuilder output;
        public readonly AtomAnimation animation;

        public TestContext(StringBuilder output, AtomAnimation animation)
        {
            this.output = output;
            this.animation = animation;
        }

        public void Assert(bool truthy, string message)
        {
            if (!truthy) output.AppendLine(message);
        }

        public void Assert(int actual, int expected, string message)
        {
            if (actual == expected) return;
            output.AppendLine(message);
            output.AppendLine($"Expected {expected}, received {actual}");
        }
    }
}
