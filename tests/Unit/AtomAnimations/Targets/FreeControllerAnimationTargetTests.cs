using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VamTimeline.Tests.Framework;

namespace VamTimeline.Tests.Unit
{
    public class FreeControllerAnimationTargetTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(AddEdgeFramesIfMissing), AddEdgeFramesIfMissing);
        }

        public IEnumerable AddEdgeFramesIfMissing(TestContext context)
        {
            var target = GivenAFreeController(context);
            target.SetKeyframe(0, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);
            target.SetKeyframe(1, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);
            target.SetKeyframe(2, Vector3.zero, Quaternion.identity, CurveTypeValues.Linear);

            target.AddEdgeFramesIfMissing(2f);

            context.Assert(target.rotX.keys.Select(k => k.curveType),
                new[] {CurveTypeValues.Linear, CurveTypeValues.Linear, CurveTypeValues.Linear},
                "Same length stays untouched");
            yield break;
        }

        private static FreeControllerAnimationTarget GivenAFreeController(TestContext context)
        {
            var controller = new GameObject("Test Controller");
            controller.SetActive(false);
            controller.transform.SetParent(context.gameObject.transform, false);
            var fc = controller.AddComponent<FreeControllerV3>();
            fc.UITransforms = new Transform[0];
            fc.UITransformsPlayMode = new Transform[0];
            var target = new FreeControllerAnimationTarget(fc);
            return target;
        }
    }
}
