using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        public void Assert<T>(T actual, T expected, string message) where T : struct
        {
            if (actual.Equals(expected)) return;
            output.AppendLine(message);
            output.AppendLine($"Expected '{expected}', received '{actual}'");
        }

        public void Assert(string actual, string expected, string message)
        {
            if (actual == expected) return;
            output.AppendLine(message);
            output.AppendLine($"Expected '{expected}', received '{actual}'");
        }

        public void Assert<T>(IEnumerable<T> actual, IEnumerable<T> expected, string message)
        {
            var actualStr = string.Join(", ", actual.Select(v => v.ToString()).ToArray());
            var expectedStr = string.Join(", ", expected.Select(v => v.ToString()).ToArray());
            Assert(actualStr, expectedStr, message);
        }
    }
}
