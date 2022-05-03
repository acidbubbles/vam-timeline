namespace VamTimeline
{
    public static class TestsIndex
    {
        public static TestsEnumerator GetAllTests()
        {
            return new TestsEnumerator(new ITestClass[]{
                new BezierAnimationCurveTests(),
                new FreeControllerAnimationTargetTests(),
                new AnimationTests(),
                new ResizeAnimationOperationTests()
            });
        }
    }
}
