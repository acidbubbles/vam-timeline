using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class SmoothScreen : ScreenBase
    {
        public const string ScreenName = "Smooth";

        public override string screenId => ScreenName;

        private UIDynamicButton _backupUI;
        private UIDynamicButton _restoreUI;
        private JSONStorableFloat _timeSpanFs;
        private JSONStorableFloat _centerWeightFs;
        private JSONStorableFloat _startTimeFs;
        private JSONStorableFloat _endTimeFs;
        private UIDynamicButton _goButton;
        private JSONStorableString _statusLabel;

        private Coroutine _co;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            _backupUI = prefabFactory.CreateButton("Backup");
            _backupUI.button.onClick.AddListener(TakeBackup);
            _restoreUI = prefabFactory.CreateButton("Restore");
            _restoreUI.button.onClick.AddListener(RestoreBackup);
            _restoreUI.button.interactable = HasBackup();
            if (HasBackup()) _restoreUI.label = $"Restore [{AtomAnimationBackup.singleton.backupTime}]";

            prefabFactory.CreateSpacer();

            var jss = new JSONStorableString("Title", "SmoothMoves by GargChow, adapted to process Timeline keyframes by AcidBubbles");
            prefabFactory.CreateTextField(jss);

            prefabFactory.CreateSpacer();

            _timeSpanFs = new JSONStorableFloat("Time Span", 0.25f,
                t => { _timeSpanFs.val = Math.Max(Math.Min(t, current.animationLength * 0.5f), 0.001f); },
                0f, current.animationLength * 0.5f, true);
            prefabFactory.CreateSlider(_timeSpanFs);

            _centerWeightFs = new JSONStorableFloat("Center Weight", 1f,
                w => { _centerWeightFs.val = w; },
                -10f, 10f, false, true
            );
            prefabFactory.CreateSlider(_centerWeightFs);

            prefabFactory.CreateSpacer();

            _startTimeFs = new JSONStorableFloat("Start Time", 0f,
                t => { _startTimeFs.val = Math.Max(0f, Math.Min(t, current.animationLength)); },
                0f, current.animationLength, true
            );
            prefabFactory.CreateSlider(_startTimeFs);

            _endTimeFs = new JSONStorableFloat("End Time", current.animationLength,
                t => { _endTimeFs.val = Math.Max(0f, Math.Min(t, current.animationLength)); },
                0f, current.animationLength, true, true
            );
            prefabFactory.CreateSlider(_endTimeFs);

            _goButton = prefabFactory.CreateButton("Apply Smoothing");
            _goButton.button.onClick.AddListener(StartSmoothing);

            _statusLabel = new JSONStorableString("Status Label", "Select at least one control and press Apply.");
            prefabFactory.CreateTextField(_statusLabel);
        }

        private void StartSmoothing()
        {
            if (_co != null)
            {
                StopCoroutine(_co);
                _co = null;
                // The button said Cancel when they clicked it
                _goButton.label = "Apply Smoothing";
                _statusLabel.val = "Smoothing cancelled.";
            }
            else if (!animationEditContext.GetSelectedTargets().OfType<FreeControllerV3AnimationTarget>().Any())
            {
                _statusLabel.val = "You must select at least one control in the dope sheet.";
            }
            else
            {
                _goButton.label = "Cancel";
                _co = StartCoroutine(DoSmoothingCo());
            }
        }

        private IEnumerator DoSmoothingCo()
        {
            yield return 0;

            var stopWatch = Stopwatch.StartNew();
            var rawQueue = new Queue<TransformStruct>();
            var targets = animationEditContext.GetSelectedTargets().OfType<FreeControllerV3AnimationTarget>().ToList();
            foreach (var currentTarget in targets)
            {
                rawQueue.Clear();

                var currentKeyframes = currentTarget.ToTransformArray();

                var startTime = _startTimeFs.val;
                var clipEndTime = currentKeyframes[currentKeyframes.Length - 1].time;
                var endTimeClamp = Math.Min(_endTimeFs.val, clipEndTime);

                // If time span is close to a multiple of the average timestep delta, we may tend to
                // have an unbalanced number of samples before and after current time and alternate
                // between, say, 7 before 8 after, 8 before 7 after -- which will tend to introduce
                // a small oscillation in the output timestep delta.  Tweak the timespan to improve
                // to the nearest half multiple of the average framerate to improve chances of
                // spanning the same number of steps left and right.

                var meanDelta = (clipEndTime - currentKeyframes[0].time) / (currentKeyframes.Length - 1f);

                var meanStepCount = _timeSpanFs.val / meanDelta;

                meanStepCount = Math.Max(1f, (float)Math.Floor(meanStepCount)) + 0.5f;
                // This also establishes a minimum average smoothing span of +- 1 step.  If you
                // click the button, you're going to get at least a little smoothing no matter how
                // low you set the time span.

                var timeSpan = meanStepCount * meanDelta;

                // Fast forward until we reach the smoothing span of the configured start time.
                var stepIndex = 1;
                var fwdIndex = 0;
                var timeNow = currentKeyframes[stepIndex].time;
                while (timeNow <= startTime && timeNow < endTimeClamp && stepIndex < currentKeyframes.Length - 2)
                {
                    stepIndex++;
                    fwdIndex++;
                    timeNow = currentKeyframes[stepIndex].time;
                }

                for (; stepIndex < currentKeyframes.Length; stepIndex++)
                {
                    timeNow = currentKeyframes[stepIndex].time;

                    if (timeNow >= endTimeClamp)
                        continue;

                    // At each step, we smooth the clip step at position stepIndex while maintaining the
                    // step range stored in rawQueue.

                    // Provide progress information
                    if (stopWatch.Elapsed.TotalSeconds > 1f / 30)
                    {
                        _statusLabel.val = $"{currentTarget.GetFullName()}: {((int)Math.Round(100 * (timeNow - startTime) / (endTimeClamp - startTime)))}%";
                        yield return 0;
                    }

                    var timeSpanClamp = Math.Min(Math.Min(timeNow - startTime, timeSpan), endTimeClamp - timeNow);

                    while (rawQueue.Count > 0 && rawQueue.Peek().time < timeNow - timeSpanClamp)
                    {
                        // Remove steps stored in rawQueue whose time position is earlier than
                        // the smoothee's time minus timeSpanClamp
                        rawQueue.Dequeue();
                    }

                    while (fwdIndex < currentKeyframes.Length && (currentKeyframes[fwdIndex].time < timeNow + timeSpanClamp))
                    {
                        // Add steps ahead of the current one whose time positions are within the timeSpanFS
                        // -- while making sure the current step, at least, always makes it into the queue
                        // (even when timeSpanFS is very small and/or time between steps is large).
                        rawQueue.Enqueue(currentKeyframes[fwdIndex]);
                        fwdIndex++;
                    }

                    if (rawQueue.Count > 2)
                    {
                        // The smoothing function is undefined for fewer than 3 timesteps, so we may
                        // have to wait an extra timestep past the configured start time and/or cut out
                        // early at the end.

                        var mrsmooth = SmoothQueue(rawQueue, timeNow, timeSpanClamp);

                        if (!float.IsNaN(mrsmooth.position.x) && !float.IsNaN(mrsmooth.position.y) && !float.IsNaN(mrsmooth.position.z))
                        {
                            if (_centerWeightFs.val >= 0) mrsmooth.time = currentKeyframes[stepIndex].time;
                            currentKeyframes[stepIndex] = mrsmooth;
                        }
                    }
                }

                currentTarget.SetTransformArray(currentKeyframes);
                rawQueue.Clear();
            }

            _goButton.label = "Apply Smoothing";
            _statusLabel.val = "Smoothing complete.";
            _co = null;
        }

        private TransformStruct SmoothQueue(Queue<TransformStruct> rawQueue, float centertime, float timeSpanClamp)
        {
            // Returns a new MotionAnimationStep resulting from smoothing of all
            // steps in rawQueue based on the given centertime

            float totalweight = 0;
            var weight = new float[rawQueue.Count];
            var adjweight = new float[rawQueue.Count];

            // Normalize the step weights (such that they add up to 1.0)
            //
            var i = 0;
            foreach (var step in rawQueue)
            {
                var wt = StepWeight(step.time, centertime, timeSpanClamp);
                // if (float.IsNaN(wt)) SuperController.LogError("** GOT NaN : timeSpanClamp=" + timeSpanClamp.ToString()
                // + "  step.time=" + step.time.ToString()
                // + "  centertime=" + centertime.ToString()
                // + "  rawQueue.Count=" + rawQueue.Count.ToString()
                // );
                weight[i] = wt;
                adjweight[i] = wt;
                totalweight += wt;
                i++;
            }

            for (i = 0; i < rawQueue.Count; i++)
            {
                weight[i] /= totalweight;
                adjweight[i] = weight[i];
            }

            // Now compute adjusted weighting factors to apply to each Quaternion.Slerp
            // so that the final contribution of each rotation is equal to its intended StepWeight
            //
            for (i = rawQueue.Count - 2; i > 0; i--)
            {
                for (var j = rawQueue.Count - 1; j > i; j--)
                {
                    // Since sum(adjweight)=1, it can be shown that each of these adjusted values is
                    // between 0 and 1 (hence a valid slerp factor).  It can also be shown that when
                    // the first step's rotation is used as the starting point, its intended StepWeight
                    // factor has been applied once the slerping is done (tho the value goes unused).
                    // One last thing: adjweight[0]=1.0 always (but we won't actually need it, and
                    // so won't compute it).
                    //
                    adjweight[i] /= (1 - adjweight[j]);
                }
            }

            // Finally, the lerping & slerping
            //
            var smoothpos = new Vector3(0, 0, 0);
            var smoothrot = new Quaternion();
            float smoothtime = 0;
            var curveType = CurveTypeValues.SmoothLocal;

            i = 0;
            foreach (var step in rawQueue)
            {
                curveType = step.curveType;
                if (i < 1)
                {
                    smoothpos = step.position;
                    smoothrot = step.rotation;
                }
                else
                {
                    smoothpos = Vector3.Lerp(smoothpos, step.position, adjweight[i]);
                    smoothrot = Quaternion.Slerp(smoothrot, step.rotation, adjweight[i]);
                }

                smoothtime += step.time * weight[i];

                i++;
            }

            var smoothstep = new TransformStruct
            {
                position = smoothpos,
                rotation = smoothrot,
                time = smoothtime,
                curveType = curveType
            };
            return smoothstep;
        }

        private float StepWeight(float steptime, float centertime, float timeSpanClamp)
        {
            // Returns the weighting factor for the given step time based on the given center time
            // and the configured timeSpanFS.

            var pct = Mathf.Abs((timeSpanClamp - Math.Abs(centertime - steptime))) / timeSpanClamp;
            // This function will only ever be evaluated for steptime values no further than
            // timeSpanFS from centertime; so this value must be between 0 and 1.  On the other
            // hand, unexpected conditions like out-of-order animation steps could violate
            // this assumption; so take a final Abs(), as we could end up with an imaginary
            // number with fractional or negative center weights if pct < 0.

            return Mathf.Pow(pct, _centerWeightFs.val);
        }

        private bool HasBackup()
        {
            return AtomAnimationBackup.singleton.HasBackup(current);
        }

        private void TakeBackup()
        {
            AtomAnimationBackup.singleton.TakeBackup(current);
            _restoreUI.label = $"Restore [{AtomAnimationBackup.singleton.backupTime}]";
            _restoreUI.button.interactable = true;
        }

        private void RestoreBackup()
        {
            AtomAnimationBackup.singleton.RestoreBackup(current);
        }
    }
}
