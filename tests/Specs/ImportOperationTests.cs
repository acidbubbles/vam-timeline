using System.Collections;
using System.Collections.Generic;

namespace VamTimeline
{
    public class ImportOperationTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(CanAddTarget_PerfectMatch), CanAddTarget_PerfectMatch);
        }

        private IEnumerable CanAddTarget_PerfectMatch(TestContext context)
        {
            var ctx = new ImportTestContext(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim 1", "Layer 1", "Segment 1", context.logger);
                clip.Add(ctx.helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            {
                var clip = ctx.imported;
                clip.Add(ctx.helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.PopulateValidChoices();

            context.Assert(ctx.okJSON.val, true, "Not OK");
            context.Assert(ctx.nameJSON.val, "Anim ?", "Name");
            context.Assert(ctx.layerJSON.val, "Layer 1", "Layer");
            context.Assert(ctx.layerJSON.choices, new[]{"Layer 1"}, "Layer choices");
            context.Assert(ctx.segmentJSON.val, "Segment 1", "Segment");
            context.Assert(ctx.segmentJSON.choices, new[]{"Segment 1", ImportOperations.NewSegmentValue}, "Segment choices");
            context.Assert(ctx.statusJSON.val, @"", "Status");


            yield break;
        }

        private class ImportTestContext
        {
            private readonly ImportOperations _op;

            public readonly JSONStorableBool okJSON;
            public readonly JSONStorableStringChooser segmentJSON;
            public readonly JSONStorableStringChooser layerJSON;
            public readonly JSONStorableString nameJSON;
            public readonly JSONStorableString statusJSON;
            public readonly TargetsHelper helper;
            public readonly AtomAnimationClip imported;

            public ImportTestContext(TestContext context)
            {
                helper = new TargetsHelper(context);
                statusJSON = new JSONStorableString("Status", "");
                nameJSON = new JSONStorableString("Name", "");
                layerJSON = new JSONStorableStringChooser("Layer", new List<string>(), "", "");
                segmentJSON = new JSONStorableStringChooser("Layer", new List<string>(), "", "");
                okJSON = new JSONStorableBool("Ok", false);
                _op = new ImportOperations(context.animation);
                imported = new AtomAnimationClip("Anim ?", "Layer ?", "Segment ?", context.logger);

            }

            public void PopulateValidChoices()
            {
                _op.PopulateValidChoices(imported, statusJSON, nameJSON, layerJSON, segmentJSON, okJSON);
            }
        }
    }
}
