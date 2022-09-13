using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class ImportOperationTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(CanAddTarget_PerfectMatch_ExistingSegment), CanAddTarget_PerfectMatch_ExistingSegment);
            yield return new Test(nameof(CanAddTarget_PerfectMatch_NewSegment), CanAddTarget_PerfectMatch_NewSegment);
            yield return new Test(nameof(CanAddTarget_Mismatch_NewSegment), CanAddTarget_Mismatch_NewSegment);
            yield return new Test(nameof(CanAddTarget_Conflict_SegmentName), CanAddTarget_Conflict_SegmentName);
            yield return new Test(nameof(CanAddTarget_Conflict_LayerName), CanAddTarget_Conflict_LayerName);
            yield return new Test(nameof(CanAddTarget_Conflict_AnimName), CanAddTarget_Conflict_AnimName);
        }

        // TODO: Non-segment import
        // TODO: Import from shared
        // TODO: Import into shared

        private IEnumerable CanAddTarget_PerfectMatch_ExistingSegment(TestContext context)
        {
            var helper = new TargetsHelper(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim 1", "Layer 1", "Segment 1", context.logger);
                clip.Add(helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            var ctx = new ImportTestContext(context);
            {
                var clip = ctx.imported;
                clip.Add(helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.op.PopulateValidChoices();
            ctx.segmentJSON.val = "Segment 1";

            context.Assert(ctx.okJSON.val, true, "OK");
            context.Assert(ctx.nameJSON.val, "Anim IMPORTED", "Name");
            context.Assert(ctx.layerJSON.val, "Layer 1", "Layer");
            context.Assert(ctx.layerJSON.choices, new[] { "Layer 1" }, "Layer choices");
            context.Assert(ctx.segmentJSON.val, "Segment 1", "Segment");
            context.Assert(ctx.segmentJSON.choices, new[] { "Segment 1", "Segment IMPORTED" }, "Segment choices");
            context.Assert(ctx.statusJSON.val, @"", "Status");

            ctx.op.ImportClip();

            context.Assert(ctx.imported.animationSegment, "Segment 1", "Processed segment name");
            context.Assert(ctx.imported.animationLayer, "Layer 1", "Processed layer name");
            context.Assert(ctx.imported.animationName, "Anim IMPORTED", "Processed animation name");
            AtomAnimationsClipsIndex.IndexedSegment segmentIndex;
            List<AtomAnimationClip> layerClips = null;
            context.Assert(context.animation.index.segmentNames, new[] { "Segment 1" }, "Segments once imported");
            context.Assert(context.animation.index.segmentsById.TryGetValue("Segment 1".ToId(), out segmentIndex), true, "Segment imported exists");
            context.Assert(segmentIndex?.layerNames, new[] { "Layer 1" }, "Layers once imported");
            context.Assert(segmentIndex?.layersMapById.TryGetValue("Layer 1".ToId(), out layerClips), true, "Layer imported exists");
            context.Assert(layerClips?.Select(c => c.animationName), new[] { "Anim 1", "Anim IMPORTED" }, "Layers once imported");

            yield break;
        }

        private IEnumerable CanAddTarget_PerfectMatch_NewSegment(TestContext context)
        {
            var helper = new TargetsHelper(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim 1", "Layer 1", "Segment 1", context.logger);
                clip.Add(helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            var ctx = new ImportTestContext(context);
            {
                var clip = ctx.imported;
                clip.Add(helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.op.PopulateValidChoices();

            context.Assert(ctx.okJSON.val, true, "OK");
            context.Assert(ctx.nameJSON.val, "Anim IMPORTED", "Name");
            context.Assert(ctx.layerJSON.val, "Layer IMPORTED", "Layer");
            context.Assert(ctx.layerJSON.choices, new[] { "Layer IMPORTED" }, "Layer choices");
            context.Assert(ctx.segmentJSON.val, "Segment IMPORTED", "Segment");
            context.Assert(ctx.segmentJSON.choices, new[] { "Segment 1", "Segment IMPORTED" }, "Segment choices");
            context.Assert(ctx.statusJSON.val, @"", "Status");

            ctx.op.ImportClip();

            context.Assert(ctx.imported.animationSegment, "Segment IMPORTED", "Processed segment name");
            context.Assert(ctx.imported.animationLayer, "Layer IMPORTED", "Processed layer name");
            context.Assert(ctx.imported.animationName, "Anim IMPORTED", "Processed animation name");
            AtomAnimationsClipsIndex.IndexedSegment segmentIndex;
            List<AtomAnimationClip> layerClips = null;
            context.Assert(context.animation.index.segmentNames, new[] { "Segment 1", "Segment IMPORTED" }, "Segments once imported");
            context.Assert(context.animation.index.segmentsById.TryGetValue("Segment IMPORTED".ToId(), out segmentIndex), true, "Segment imported exists");
            context.Assert(segmentIndex?.layerNames, new[] { "Layer IMPORTED" }, "Layers once imported");
            context.Assert(segmentIndex?.layersMapById.TryGetValue("Layer IMPORTED".ToId(), out layerClips), true, "Layer imported exists");
            context.Assert(layerClips?.Select(c => c.animationName), new[] { "Anim IMPORTED" }, "Layers once imported");

            yield break;
        }

        private IEnumerable CanAddTarget_Mismatch_NewSegment(TestContext context)
        {
            var helper = new TargetsHelper(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim 1", "Layer 1", "Segment 1", context.logger);
                clip.Add(helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            var ctx = new ImportTestContext(context);
            {
                var clip = ctx.imported;
                clip.Add(helper.GivenFreeController("C2")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F2")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T2")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.op.PopulateValidChoices();

            context.Assert(ctx.okJSON.val, true, "OK");
            context.Assert(ctx.nameJSON.val, "Anim IMPORTED", "Name");
            context.Assert(ctx.layerJSON.val, "Layer IMPORTED", "Layer");
            context.Assert(ctx.layerJSON.choices, new[] { "Layer IMPORTED" }, "Layer choices");
            context.Assert(ctx.segmentJSON.val, "Segment IMPORTED", "Segment");
            context.Assert(ctx.segmentJSON.choices, new[] { "Segment IMPORTED" }, "Segment choices");
            context.Assert(ctx.statusJSON.val, @"", "Status");

            ctx.op.ImportClip();

            context.Assert(ctx.imported.animationSegment, "Segment IMPORTED", "Processed segment name");
            context.Assert(ctx.imported.animationLayer, "Layer IMPORTED", "Processed layer name");
            context.Assert(ctx.imported.animationName, "Anim IMPORTED", "Processed animation name");
            AtomAnimationsClipsIndex.IndexedSegment segmentIndex;
            List<AtomAnimationClip> layerClips = null;
            context.Assert(context.animation.index.segmentNames, new[] { "Segment 1", "Segment IMPORTED" }, "Segments once imported");
            context.Assert(context.animation.index.segmentsById.TryGetValue("Segment IMPORTED".ToId(), out segmentIndex), true, "Segment imported exists");
            context.Assert(segmentIndex?.layerNames, new[] { "Layer IMPORTED" }, "Layers once imported");
            context.Assert(segmentIndex?.layersMapById.TryGetValue("Layer IMPORTED".ToId(), out layerClips), true, "Layer imported exists");
            context.Assert(layerClips?.Select(c => c.animationName), new[] { "Anim IMPORTED" }, "Layers once imported");

            yield break;
        }

        private IEnumerable CanAddTarget_Conflict_SegmentName(TestContext context)
        {
            var helper = new TargetsHelper(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim 1", "Layer 1", "Segment IMPORTED", context.logger);
                clip.Add(helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            var ctx = new ImportTestContext(context);
            {
                var clip = ctx.imported;
                clip.Add(helper.GivenFreeController("C2")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F2")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T2")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.op.PopulateValidChoices();

            context.Assert(ctx.okJSON.val, true, "OK");
            context.Assert(ctx.nameJSON.val, "Anim IMPORTED", "Name");
            context.Assert(ctx.layerJSON.val, "Layer IMPORTED", "Layer");
            context.Assert(ctx.layerJSON.choices, new[] { "Layer IMPORTED" }, "Layer choices");
            context.Assert(ctx.segmentJSON.val, "Segment IMPORTED 2", "Segment");
            context.Assert(ctx.segmentJSON.choices, new[] { "Segment IMPORTED 2" }, "Segment choices");
            context.Assert(ctx.statusJSON.val, @"", "Status");

            yield break;
        }

        private IEnumerable CanAddTarget_Conflict_LayerName(TestContext context)
        {
            var helper = new TargetsHelper(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim 1", "Layer IMPORTED", "Segment 1", context.logger);
                clip.Add(helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            var ctx = new ImportTestContext(context);
            {
                var clip = ctx.imported;
                clip.Add(helper.GivenFreeController("C2")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F2")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T2")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.op.PopulateValidChoices();
            ctx.op.segmentJSON.val = "Segment 1";

            context.Assert(ctx.segmentJSON.val, "Segment IMPORTED", "Segment");

            yield break;
        }

        private IEnumerable CanAddTarget_Conflict_AnimName(TestContext context)
        {
            var helper = new TargetsHelper(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim IMPORTED", "Layer 1", "Segment 1", context.logger);
                clip.Add(helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            var ctx = new ImportTestContext(context);
            {
                var clip = ctx.imported;
                clip.Add(helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.op.PopulateValidChoices();
            ctx.op.segmentJSON.val = "Segment 1";

            context.Assert(ctx.segmentJSON.val, "Segment 1", "Segment");
            context.Assert(ctx.okJSON.val, false, "Ok");
            context.Assert(ctx.statusJSON.val, "Animation name not available on layer.", "Status");

            yield break;
        }

        private class ImportTestContext
        {
            public AtomAnimationClip imported => op.clip;
            public JSONStorableBool okJSON => op.okJSON;
            public JSONStorableStringChooser segmentJSON => op.segmentJSON;
            public JSONStorableStringChooser layerJSON => op.layerJSON;
            public JSONStorableString nameJSON => op.nameJSON;
            public JSONStorableString statusJSON => op.statusJSON;
            public readonly ImportOperationClip op;

            public ImportTestContext(TestContext context)
            {
                var clip = new AtomAnimationClip("Anim IMPORTED", "Layer IMPORTED", "Segment IMPORTED", context.logger);
                op = new ImportOperationClip(context.animation, clip);
            }
        }
    }
}
