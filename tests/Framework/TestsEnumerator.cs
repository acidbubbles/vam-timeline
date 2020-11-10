using System.Collections;
using System.Collections.Generic;

namespace VamTimeline
{
    public class TestsEnumerator : IEnumerable<Test>
    {
        private readonly ITestClass[] _testClasses;

        public TestsEnumerator(ITestClass[] testClasses)
        {
            _testClasses = testClasses;
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable<Test>)this).GetEnumerator();
        }

        IEnumerator<Test> IEnumerable<Test>.GetEnumerator()
        {
            foreach (var testClass in _testClasses)
            {
                foreach (var test in testClass.GetTests())
                {
                    yield return test;
                }
            }
        }
    }
}
