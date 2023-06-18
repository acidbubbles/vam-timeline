using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VamTimeline
{
    public class TestContext
    {
        private const string _assertionFailedMessage = "Assertion failed";

        public readonly GameObject gameObject;
        public readonly AtomAnimation animation;
        public readonly Logger logger;

        private readonly StringBuilder _output;

        public TestContext(GameObject gameObject, StringBuilder output, AtomAnimation animation)
        {
            this.gameObject = gameObject;
            logger = new Logger(null);
            this.animation = animation;
            this.animation.logger = logger;
            _output = output;
        }

        public bool Assert(bool truthy, string message)
        {
            if (!truthy) _output.AppendLine(message);
            return truthy;
        }

        public bool Assert<T>(T? actual, T expected, string message = _assertionFailedMessage) where T : struct
        {
            if (actual.Equals(expected)) return true;
            _output.AppendLine(message);
            _output.AppendLine($"Expected '{expected}', received '{actual}'");
            return false;
        }

        public bool Assert<T>(T actual, T expected, string message = _assertionFailedMessage) where T : struct
        {
            if (actual.Equals(expected)) return true;
            _output.AppendLine(message);
            _output.AppendLine($"Expected '{expected}', received '{actual}'");
            return false;
        }

        public bool Assert(string actual, string expected, string message = _assertionFailedMessage)
        {
            if (actual == expected) return true;
            _output.AppendLine(message);
            _output.AppendLine($"Expected '{expected}', received '{actual}'");
            return false;
        }

        public bool AssertList<T>(IEnumerable<T> actual, IEnumerable<T> expected, string message = _assertionFailedMessage)
        {
            if (actual == null)
            {
                _output.AppendLine(message);
                _output.AppendLine($"Expected '{expected}', received null");
                return false;
            }
            var actualStr = string.Join(", ", actual.Select(v => v.ToString()).ToArray());
            var expectedStr = string.Join(", ", expected.Select(v => v.ToString()).ToArray());
            return Assert(actualStr, expectedStr, message);
        }
    }
}
