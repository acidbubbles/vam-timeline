using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerAnimationTargetTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(AddEdgeFramesIfMissing_SameLengthStaysUntouched), AddEdgeFramesIfMissing_SameLengthStaysUntouched);
            yield return new Test(nameof(AddEdgeFramesIfMissing_WithTwoKeyframes_Moves), AddEdgeFramesIfMissing_WithTwoKeyframes_Moves);
            yield return new Test(nameof(AddEdgeFramesIfMissing_WithThreeKeyframes_Adds), AddEdgeFramesIfMissing_WithThreeKeyframes_Adds);
            yield return new Test(nameof(AddEdgeFramesIfMissing_WithCopyPrevious_AlwaysExtends), AddEdgeFramesIfMissing_WithCopyPrevious_AlwaysExtends);
        }

        public IEnumerable AddEdgeFramesIfMissing_SameLengthStaysUntouched(TestContext context)
        {
            var target = GivenAFreeController(context);
            target.SetKeyframeByTime(0, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);
            target.SetKeyframeByTime(1, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);
            target.SetKeyframeByTime(2, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);

            target.AddEdgeFramesIfMissing(2f);

            context.Assert(target.rotX.keys.Select(k => k.curveType),
                new[] {CurveTypeValues.Linear, CurveTypeValues.Linear, CurveTypeValues.Linear}
            );
            yield break;
        }

        public IEnumerable AddEdgeFramesIfMissing_WithTwoKeyframes_Moves(TestContext context)
        {
            var target = GivenAFreeController(context);
            target.SetKeyframeByTime(0, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);
            target.SetKeyframeByTime(1, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);

            target.AddEdgeFramesIfMissing(2f);

            context.Assert(target.rotX.keys.Select(k => k.curveType),
                new[] {CurveTypeValues.Linear, CurveTypeValues.Linear}
            );
            yield break;
        }

        public IEnumerable AddEdgeFramesIfMissing_WithThreeKeyframes_Adds(TestContext context)
        {
            var target = GivenAFreeController(context);
            target.SetKeyframeByTime(0, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);
            target.SetKeyframeByTime(1, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);
            target.SetKeyframeByTime(2, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);

            target.AddEdgeFramesIfMissing(3f);

            context.Assert(target.rotX.keys.Select(k => k.curveType),
                new[] {CurveTypeValues.Linear, CurveTypeValues.Linear, CurveTypeValues.Linear, CurveTypeValues.Linear}
            );
            yield break;
        }

        public IEnumerable AddEdgeFramesIfMissing_WithCopyPrevious_AlwaysExtends(TestContext context)
        {
            var target = GivenAFreeController(context);
            target.SetKeyframeByTime(0, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);
            target.SetKeyframeByTime(1, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);
            target.SetKeyframeByTime(2, Vector3.zero, Quaternion.identity, CurveTypeValues.CopyPrevious);

            target.AddEdgeFramesIfMissing(3f);

            context.Assert(target.rotX.keys.Select(k => k.curveType),
                new[] {CurveTypeValues.Linear, CurveTypeValues.Linear, CurveTypeValues.CopyPrevious}
            );
            yield break;
        }

        private static FreeControllerV3AnimationTarget GivenAFreeController(TestContext context)
        {
            var controller = new GameObject("Test Controller");
            controller.SetActive(false);
            controller.transform.SetParent(context.gameObject.transform, false);
            var fc = controller.AddComponent<FreeControllerV3>();
            fc.UITransforms = new Transform[0];
            fc.UITransformsPlayMode = new Transform[0];
            var target = new FreeControllerV3AnimationTarget(new FreeControllerV3Ref(fc));
            return target;
        }
    }
}
