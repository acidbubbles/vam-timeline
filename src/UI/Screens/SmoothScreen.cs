using System;
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

        private JSONStorableFloat _timeSpanFs;
        private JSONStorableFloat _centerWeightFs;
        private JSONStorableFloat _startTimeFs;
        private JSONStorableFloat _endTimeFs;
        private UIDynamicButton _goButton;
        private JSONStorableString _statusLabel;

        private float _clipEndTime;
        private float _endTimeClamp;

        private float _timeSpanClamp;

        private int _macIndex = -1;
        private int _stepIndex = -1;
        private int _fwdIndex = -1;

        private FreeControllerV3AnimationTarget _currentTarget;
        TransformStruct[] _currentKeyframes;

        private Queue<TransformStruct> _rawQueue;

        private RunSettings _runSettings;
        private bool _running;
        private Stopwatch _stopWatch;

        private const long _maxWorkMS = 8;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

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
            _goButton.button.onClick.AddListener(() => { StartSmoothing(); });

            _statusLabel = new JSONStorableString("Status Label", "Select at least one control and press Apply.");
            prefabFactory.CreateTextField(_statusLabel);
        }

        private class RunSettings
        {
            // package up settings for the current run
            public float timeSpan;
            public float baseTimeSpan;
            public float startTime;
            public float endTime;
            public List<FreeControllerV3AnimationTarget> targets;
        }

        private void PrepareRunSettings()
        {
            _runSettings = new RunSettings
            {
                timeSpan = _timeSpanFs.val,
                baseTimeSpan = _timeSpanFs.val,
                startTime = _startTimeFs.val,
                endTime = _endTimeFs.val,
                targets = animationEditContext.GetSelectedTargets().OfType<FreeControllerV3AnimationTarget>().ToList()
            };
        }

        private void TweakTimeSpan()
        {
            // If time span is close to a multiple of the average timestep delta, we may tend to
            // have an unbalanced number of samples before and after current time and alternate
            // between, say, 7 before 8 after, 8 before 7 after -- which will tend to introduce
            // a small oscillation in the output timestep delta.  Tweak the timespan to improve
            // to the nearest half multiple of the average framerate to improve chances of
            // spanning the same number of steps left and right.

            var keyframes = animationEditContext.GetSelectedCurveTargets().First().GetAllKeyframesTime();

            var meandt = (_clipEndTime - keyframes[0]) / (keyframes.Length - 1f);

            var meanstepcount = _runSettings.baseTimeSpan / meandt;

            meanstepcount = Math.Max(1f, (float)Math.Floor(meanstepcount)) + 0.5f;
            // This also establishes a minimum average smoothing span of +- 1 step.  If you
            // click the button, you're going to get at least a little smoothing no matter how
            // low you set the time span.

            _runSettings.timeSpan = meanstepcount * meandt;
        }

        private void StartSmoothing()
        {

            if (_running)
            {
                // The button said Cancel when they clicked it
                _stopWatch.Reset();
                _stopWatch = null;
                _goButton.label = "Apply Smoothing";
                _statusLabel.val = "Smoothing cancelled while in progress.  Reloading from save is recommended.";
                _running = false;
                return;
            }

            _running = true;
            _goButton.label = "Cancel";

            PrepareRunSettings();

            _macIndex = -1;
            _stepIndex = -1;
            _fwdIndex = -1;

            _stopWatch = Stopwatch.StartNew();
        }

        private void ContinueSmoothing()
        {
            // Get in as much smoothing as possible in the configured time frame

            _stopWatch.Reset();
            _stopWatch.Start();

            var starttime = _runSettings.startTime;

            while (_stopWatch.ElapsedMilliseconds < _maxWorkMS)
            {

                float timenow; // (shut the mindless compiler up)

                // Update target clip if needed
                //
                if (_currentKeyframes == null)
                {

                    while (_currentKeyframes == null && ++_macIndex < _runSettings.targets.Count)
                    {
                        _currentTarget = _runSettings.targets[_macIndex];
                        if (_currentTarget != null) _currentKeyframes = _currentTarget.ToTransformArray();
                    }

                    if (_macIndex >= _runSettings.targets.Count)
                    {
                        // We're completely done
                        _running = false;
                        break;
                    }

                    // we've changed clips; initialize
                    //
                    _rawQueue = new Queue<TransformStruct>();
                    _clipEndTime = _currentKeyframes[_currentKeyframes.Length - 1].time;
                    _endTimeClamp = Math.Min(_runSettings.endTime, _clipEndTime);

                    TweakTimeSpan();

                    _stepIndex = 1;
                    _fwdIndex = 0;

                    timenow = _currentKeyframes[_stepIndex].time;

                    // Fast forward until we reach the smoothing span of the configured start time.
                    //
                    while (timenow <= starttime && timenow < _endTimeClamp && _stepIndex < _currentKeyframes.Length - 2)
                    {
                        _stepIndex++;
                        _fwdIndex++;
                        timenow = _currentKeyframes[_stepIndex].time;
                    }

                }
                else
                {

                    timenow = _currentKeyframes[_stepIndex].time;

                } // if updating clip

                if (timenow >= _endTimeClamp || _stepIndex >= _currentKeyframes.Length - 1)
                {
                    // We're done with the current clip
                    _currentKeyframes = null;
                    _currentTarget = null;
                    if (_rawQueue != null) _rawQueue.Clear();
                    continue;
                }

                // At each step, we smooth the clip step at position stepIndex while maintaining the
                // step range stored in rawQueue.

                _statusLabel.val = _currentTarget.GetFullName() + ": "
                                                       + ((int)Math.Round(100 * (timenow - starttime) / (_endTimeClamp - starttime))).ToString() + "%";
                // Provide progress information

                _timeSpanClamp = Math.Min(Math.Min(timenow - starttime, _runSettings.timeSpan), _endTimeClamp - timenow);

                while (_rawQueue.Count > 0 && _rawQueue.Peek().time < timenow - _timeSpanClamp)
                {
                    // Remove steps stored in rawQueue whose time position is earlier than
                    // the smoothee's time minus timeSpanClamp
                    _rawQueue.Dequeue();
                }

                while (_fwdIndex < _currentKeyframes.Length && (_currentKeyframes[_fwdIndex].time < timenow + _timeSpanClamp))
                {
                    // Add steps ahead of the current one whose time positions are within the timeSpanFS
                    // -- while making sure the current step, at least, always makes it into the queue
                    // (even when timeSpanFS is very small and/or time between steps is large).
                    _rawQueue.Enqueue(_currentKeyframes[_fwdIndex]);
                    _fwdIndex++;
                }

                TransformStruct mrsmooth;

                if (_rawQueue.Count > 2)
                {
                    // The smoothing function is undefined for fewer than 3 timesteps, so we may
                    // have to wait an extra timestep past the configured start time and/or cut out
                    // early at the end.

                    mrsmooth = SmoothQueue(timenow);

                    if (float.IsNaN(mrsmooth.position.x)
                        || float.IsNaN(mrsmooth.position.y)
                        || float.IsNaN(mrsmooth.position.z))
                    {
                        _currentKeyframes[_stepIndex] = mrsmooth;
                        if (_centerWeightFs.val >= 0) mrsmooth.time = _currentKeyframes[_stepIndex].time;
                    }
                }

                _stepIndex++;
                if (_stepIndex >= _currentKeyframes.Length - 1)
                {
                    // All done with this entire clip
                    _currentTarget.SetTransformArray(_currentKeyframes);
                    _currentKeyframes = null;
                    _currentTarget = null;
                    _rawQueue.Clear();
                }

            } // while stopWatch

            // Time's up! Return control to main thread & pick up where we left off on next call.
            // But hey: did we finish?
            //
            if (!_running)
            {
                // tidy up
                _rawQueue = null;
                _stopWatch.Reset();
                _stopWatch = null;
                _goButton.label = "Apply Smoothing";
                _statusLabel.val = "Smoothing complete.  You can remove this plugin when finished.";
                _runSettings = null;
            }

        } // ContinueSmoothing()

        private TransformStruct SmoothQueue(float centertime)
        {
            // Returns a new MotionAnimationStep resulting from smoothing of all
            // steps in rawQueue based on the given centertime

            float totalweight = 0;
            var weight = new float[_rawQueue.Count];
            var adjweight = new float[_rawQueue.Count];

            // Normalize the step weights (such that they add up to 1.0)
            //
            var i = 0;
            foreach (var step in _rawQueue)
            {
                var wt = StepWeight(step.time, centertime);
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

            for (i = 0; i < _rawQueue.Count; i++)
            {
                weight[i] /= totalweight;
                adjweight[i] = weight[i];
            }

            // Now compute adjusted weighting factors to apply to each Quaternion.Slerp
            // so that the final contribution of each rotation is equal to its intended StepWeight
            //
            for (i = _rawQueue.Count - 2; i > 0; i--)
            {
                for (var j = _rawQueue.Count - 1; j > i; j--)
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
            foreach (var step in _rawQueue)
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
#warning This is just the last curve type, maybe this doesn't make sense
                curveType = curveType
            };
            return smoothstep;
        }

        private float StepWeight(float steptime, float centertime)
        {
            // Returns the weighting factor for the given step time based on the given center time
            // and the configured timeSpanFS.

            var pct = Mathf.Abs((_timeSpanClamp - Math.Abs(centertime - steptime))) / _timeSpanClamp;
            // This function will only ever be evaluated for steptime values no further than
            // timeSpanFS from centertime; so this value must be between 0 and 1.  On the other
            // hand, unexpected conditions like out-of-order animation steps could violate
            // this assumption; so take a final Abs(), as we could end up with an imaginary
            // number with fractional or negative center weights if pct < 0.

            return Mathf.Pow(pct, _centerWeightFs.val);
        }

        // Update is called with each rendered frame by Unity
        public void Update()
        {
            try
            {
                if (_running) ContinueSmoothing();
            }
            catch (Exception e)
            {
                _running = false;
                SuperController.LogError("Timeline: Error during SmoothMoves: " + e);
            }
        }
    }
}
