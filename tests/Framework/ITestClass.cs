using System.Collections.Generic;

namespace VamTimeline.Tests.Framework
{
    public interface ITestClass
    {
        IEnumerable<Test> GetTests();
    }
}
