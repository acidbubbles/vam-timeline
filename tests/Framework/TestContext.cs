using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VamTimeline.Tests.Framework
{
    public class TestContext
    {
        public readonly GameObject gameObject;
        public readonly StringBuilder output;
        public readonly AtomAnimation animation;

        public TestContext(GameObject gameObject, StringBuilder output, AtomAnimation animation)
        {
            this.gameObject = gameObject;
            this.output = output;
            this.animation = animation;
        }

        public bool Assert(bool truthy, string message)
        {
            if (!truthy) output.AppendLine(message);
            return truthy;
        }

        public bool Assert<T>(T actual, T expected, string message) where T : struct
        {
            if (actual.Equals(expected)) return true;
            output.AppendLine(message);
            output.AppendLine($"Expected '{expected}', received '{actual}'");
            return false;
        }

        public bool Assert(string actual, string expected, string message)
        {
            if (actual == expected) return true;
            output.AppendLine(message);
            output.AppendLine($"Expected '{expected}', received '{actual}'");
            return false;
        }

        public bool Assert<T>(IEnumerable<T> actual, IEnumerable<T> expected, string message)
        {
            var actualStr = string.Join(", ", actual.Select(v => v.ToString()).ToArray());
            var expectedStr = string.Join(", ", expected.Select(v => v.ToString()).ToArray());
            return Assert(actualStr, expectedStr, message);
        }
    }
}
