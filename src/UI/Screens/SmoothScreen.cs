using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace VamTimeline
{
    public class SmoothScreen : ScreenBase
    {
        public const string ScreenName = "Smooth";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);
        }
    }

  public class SmoothMoves : MVRScript {

    #region "Local classes"

    protected class FloatSlider { // your typical slider-controlled float

      public float val;
      public JSONStorableFloat jFloat;
      public UIDynamicSlider slider;

      public FloatSlider(MVRScript hostscript, string title, float startval,
        JSONStorableFloat.SetFloatCallback cb,
        float minval, float maxval, bool constrain = true, bool rightside = false) {

        val = startval;

        jFloat = new JSONStorableFloat(title, startval, cb, minval, maxval, constrain);
        jFloat.storeType = JSONStorableParam.StoreType.Full;
        hostscript.RegisterFloat(jFloat);
        slider = hostscript.CreateSlider(jFloat,rightside);
      }
    }

    protected class StringPopup { // your typical string selector

      public JSONStorableStringChooser chooser;
      public UIDynamicPopup popup;

      public StringPopup(MVRScript hostscript, string name,
        List<string> choiceslist, string initval, string displayname,
        JSONStorableStringChooser.SetStringCallback cb,
        bool rightside = false) {

        chooser = new JSONStorableStringChooser(name, choiceslist, initval, displayname, cb);

        chooser.storeType = JSONStorableParam.StoreType.Full;
        hostscript.RegisterStringChooser(chooser);
        popup = hostscript.CreateScrollablePopup(chooser,rightside);
      }
    }

    protected class LabelField { // a non-registered text field

      public JSONStorableString jstring;
      public UIDynamicTextField field;

      public LabelField(MVRScript hostscript, string name, string text, bool rightside = false) {

        jstring = new JSONStorableString(name, text);
				field = hostscript.CreateTextField(jstring,rightside);
      }
    }

    protected class RunSettings { // package up settings for the current run
      public float timeSpan;
      public float baseTimeSpan;
      public float startTime;
      public float endTime;
      public float centerWeight;
      public List<MotionAnimationControl> targets;
    }

    #endregion // classes

    #region "Data members"

    protected MotionAnimationMaster MaM; // shorthand

    // GUI elements
    //
    protected FloatSlider timeSpanFS;
      // Each step is averaged with neighboring steps within +- timeSpanFS/2 seconds

    protected FloatSlider centerWeightFS;
      // Weighting factor for the step at the current time when smoothing.

    protected FloatSlider startTimeFS;
    protected FloatSlider endTimeFS;

    protected UIDynamicButton goButton;
    protected const string GOBUTTON_GO = "Apply Smoothing";
    protected const string GOBUTTON_CANCEL = "Cancel";

    protected const string STATUS_UNLOADME =
      "Save scene before smoothing!  You can remove this plugin when finished.";
    protected const string STATUS_COMPLETE =
      "Smoothing complete.  You can remove this plugin when finished.";
    protected const string STATUS_CANCELLED =
      "Smoothing cancelled while in progress.  Reloading from save is recommended.";
    protected LabelField statusLabel;

    protected StringPopup animationChooser;
    protected List<MotionAnimationControl> allAnimations;
    protected MotionAnimationControl targetAnimation = null;
    protected Dictionary<string, MotionAnimationControl> controlNameToMac;

    // Smoothing computation
    //
    protected float clipEndTime = 0f;
    protected float endTimeClamp = 0f;
    protected float timeSpanClamp = 0f;
      // When we're closer than timeSpanFS to the start & end, we must use a restricted span

    protected int macIndex = -1;
    protected int stepIndex = -1;
    protected int fwdIndex = -1;

    protected MotionAnimationControl maCtl = null;
    protected MotionAnimationClip maClip = null;
    List<MotionAnimationStep> clipSteps = null;

    protected Queue<MotionAnimationStep> rawQueue = null;
      // Original steps surrounding the step we're currently smoothing

    // Smoothing task control.  Could do the entire smoothing process in the button callback; but no
    // new frame can be rendered until we return from the callback, so if the user smooths a very
    // long animation with a large smoothing span, VaM will appear to freeze or crash for several
    // seconds (they may see the SteamVR construct).  Also, no progress indicator we try to display
    // in the form or scene will ever render.  So instead, we perform the smoothing a bit at a time
    // during as many calls to Update() as it takes.
    //
    protected RunSettings runSettings;
    protected bool running = false;
    protected Stopwatch stopWatch;

    protected const long MAX_WORK_MS = 8;
      // Maximum time to do smoothing work before suspending the calculations and returning from
      // Update().  Note that this should be on the order of 11, since headsets like
      // can achieve up to 90 Hz, which is 11.11 ms per frame.  A 5 ms delay will make the
      // frame period 16.11, yielding 62 fps.  An 8 ms delay will yield 52 fps.

    #endregion // data

    #region "Private methods"

    protected void BuildCustomUI() {

      // Title
      //
      LabelField titlefield = new LabelField(
        this,
        "plugintitle",
        "Smooth Moves"
      );
      titlefield.field.UItext.fontSize = (int) Math.Floor((float) titlefield.field.UItext.fontSize * 1.5f);
      titlefield.field.UItext.fontStyle = FontStyle.Bold;
      titlefield.field.height = (float) titlefield.field.UItext.fontSize * 1.1f;

      LabelField titlefield2 = new LabelField(
        this,
        "plugintitle",
        "Recorded Animation Smoother",
        true
      );
      titlefield2.field.UItext.fontSize = (int) Math.Floor((float) titlefield2.field.UItext.fontSize * 1.25f);
      titlefield2.field.height = titlefield.field.height;

      UIDynamic spacer = CreateSpacer(false);
      spacer.height = titlefield.field.height * 0.15f;
      spacer = CreateSpacer(true);
      spacer.height = titlefield.field.height * 0.15f;

      // The smoothing parameter sliders
      //
      float maxts = MaM.GetTotalTime();

      timeSpanFS = new FloatSlider(this, "Time Span", 0.25f,
        (float t) => { timeSpanFS.val = Math.Max(Math.Min(t,MaM.GetTotalTime()*0.5f),0.001f); },
        0f, maxts*0.5f, true
      );

      centerWeightFS = new FloatSlider(this, "Center Weight", 1f,
        (float w) => { centerWeightFS.val = w; },
        -10f, 10f, false, true
      );

      // Brief instructions
      //
      LabelField tshints = new LabelField(
        this,
        "hints",
        "Blend each step's position and rotation with steps within this time on both sides",
        false
      );
      LabelField cwhints = new LabelField(
        this,
        "hints",
        ">0\tfavor steps near current\n=0\tfavor all steps equally\n<0\tfavor edges (not recommended)",
        true
      );
      cwhints.field.height = (float) cwhints.field.UItext.fontSize * 3.75f;
      tshints.field.height = cwhints.field.height;

      spacer = CreateSpacer(false);
      spacer.height = titlefield.field.height * 0.15f;
      spacer = CreateSpacer(true);
      spacer.height = titlefield.field.height * 0.15f;

      // The time segment sliders
      //
      startTimeFS = new FloatSlider(this, "Start Time", 0f,
        (float t) => { startTimeFS.val = Math.Max(0f,Math.Min(t,MaM.GetTotalTime())); },
        0f, maxts, true
      );

      endTimeFS = new FloatSlider(this, "End Time", maxts,
        (float t) => { endTimeFS.val = Math.Max(0f,Math.Min(t,MaM.GetTotalTime())); },
        0f, maxts, true, true
      );

      // The controller selector
      //
      MotionAnimationControl[] atommacs = containingAtom.motionAnimationControls;
      List<string> choicelist = new List<string>();
      controlNameToMac = new Dictionary<string, MotionAnimationControl>();
      allAnimations = new List<MotionAnimationControl>();

      choicelist.Add("(All)");

      foreach (MotionAnimationControl mac in atommacs) {
        if (mac.clip != null && mac.clip.steps.Count > 2) {
          choicelist.Add(mac.controller.name);
          controlNameToMac.Add(mac.controller.name, mac);
          allAnimations.Add(mac);
        }
      }

      animationChooser = new StringPopup(this, "animation", choicelist, choicelist[0], "Recorded Animation",
        (string macname) => {
          if (macname != null) {
            MotionAnimationControl mac;
            if (controlNameToMac.TryGetValue(macname, out mac)) {
              targetAnimation = mac;
            } else {
              targetAnimation = null;
            }
          } else {
            targetAnimation = null;
          }
        }
      );

      goButton = CreateButton(GOBUTTON_GO,true);
      goButton.button.onClick.AddListener( () => { StartSmoothing(); } );

      goButton.buttonText.fontSize = (int) Math.Floor((float) goButton.buttonText.fontSize * 1.5f);

      goButton.height = Math.Max(goButton.height,animationChooser.popup.height);
      animationChooser.popup.height = goButton.height;

      // A label for status information
      //
      statusLabel = new LabelField(this,"statuslabel",STATUS_UNLOADME,true);

      statusLabel.field.UItext.fontSize = (int) Math.Floor((float)statusLabel.field.UItext.fontSize * 1.1f);
      statusLabel.field.UItext.fontStyle = FontStyle.Italic;
      statusLabel.field.UItext.alignment = TextAnchor.MiddleCenter;
      statusLabel.field.height = goButton.height * 1.25f;
      statusLabel.field.textColor = animationChooser.popup.labelTextColor;
      statusLabel.field.backgroundColor = animationChooser.popup.popup.normalBackgroundColor;

      // Last minute styling based on colors that have become available
      //
      titlefield.field.textColor = animationChooser.popup.labelTextColor;
      titlefield.field.backgroundColor = animationChooser.popup.popup.normalBackgroundColor;
      titlefield2.field.textColor = animationChooser.popup.labelTextColor;
      titlefield2.field.backgroundColor = animationChooser.popup.popup.normalBackgroundColor;
      cwhints.field.textColor = animationChooser.popup.labelTextColor;
      cwhints.field.backgroundColor = animationChooser.popup.popup.normalBackgroundColor;
      tshints.field.textColor = animationChooser.popup.labelTextColor;
      tshints.field.backgroundColor = animationChooser.popup.popup.normalBackgroundColor;
    }

    protected void prepareRunSettings() {

      runSettings = new RunSettings();
      runSettings.timeSpan = timeSpanFS.val;
      runSettings.baseTimeSpan = timeSpanFS.val;
      runSettings.startTime = startTimeFS.val;
      runSettings.endTime = endTimeFS.val;
      runSettings.centerWeight = centerWeightFS.val;

      if (targetAnimation == null) {
        runSettings.targets = allAnimations;
      } else {
        runSettings.targets = new List<MotionAnimationControl>();
        runSettings.targets.Add(targetAnimation);
      }
    }

    protected void tweakTimeSpan() {
      // If time span is close to a multiple of the average timestep delta, we may tend to
      // have an unbalanced number of samples before and after current time and alternate
      // between, say, 7 before 8 after, 8 before 7 after -- which will tend to introduce
      // a small oscillation in the output timestep delta.  Tweak the timespan to improve
      // to the nearest half multiple of the average framerate to improve chances of
      // spanning the same number of steps left and right.

      if (clipSteps.Count < 2) return; // nice animation mon

      float meandt = (clipEndTime - clipSteps[0].timeStep) / (float) (clipSteps.Count-1);

      float meanstepcount = runSettings.baseTimeSpan / meandt;

      meanstepcount = Math.Max(1f, (float) Math.Floor(meanstepcount)) + 0.5f;
        // This also establishes a minimum average smoothing span of +- 1 step.  If you
        // click the button, you're going to get at least a little smoothing no matter how
        // low you set the time span.

      runSettings.timeSpan = meanstepcount * meandt;
    }

    protected void StartSmoothing() {

      if (running) {
        // The button said Cancel when they clicked it
        stopWatch.Reset();
        stopWatch = null;
        goButton.label = GOBUTTON_GO;
        statusLabel.field.text = STATUS_CANCELLED;
        running = false;
        return;
      }

      running = true;
      goButton.label = GOBUTTON_CANCEL;

      MaM.StopPlayback();
      MaM.SeekToBeginning();

      prepareRunSettings();

      macIndex = -1;
      stepIndex = -1;
      fwdIndex = -1;
      maCtl = null;
      maClip = null;
      clipSteps = null;

      stopWatch = Stopwatch.StartNew();
    }

    protected void ContinueSmoothing() {
      // Get in as much smoothing as possible in the configured time frame

      stopWatch.Reset();
      stopWatch.Start();

      float starttime = runSettings.startTime;

      while (stopWatch.ElapsedMilliseconds < MAX_WORK_MS) {

        float timenow = 0; // (shut the mindless compiler up)

        // Update target clip if needed
        //
        if (clipSteps == null) {

          while (clipSteps == null && ++macIndex < runSettings.targets.Count) {
            maCtl = runSettings.targets[macIndex];
            maClip = maCtl.clip;
            if (maClip != null) clipSteps = maClip.steps;
          }

          if (macIndex >= runSettings.targets.Count) { // We're completely done
            running = false;
            break;
          }

          // we've changed clips; initialize
          //
          rawQueue = new Queue<MotionAnimationStep>();
          clipEndTime = clipSteps[clipSteps.Count-1].timeStep;
          endTimeClamp = Math.Min(runSettings.endTime,clipEndTime);

          tweakTimeSpan();

          stepIndex = 1;
          fwdIndex = 0;

          timenow = clipSteps[stepIndex].timeStep;

          // Fast forward until we reach the smoothing span of the configured start time.
          //
          while (timenow <= starttime && timenow < endTimeClamp && stepIndex < clipSteps.Count-2) {
            stepIndex++;
            fwdIndex++;
            timenow = clipSteps[stepIndex].timeStep;
          }

        } else {

          timenow = clipSteps[stepIndex].timeStep;

        } // if updating clip

        if (timenow >= endTimeClamp || stepIndex >= clipSteps.Count-1) {
          // We're done with the current clip
          clipSteps = null;
          maClip = null;
          maCtl = null;
          if (rawQueue != null) rawQueue.Clear();
          continue;
        }

        // At each step, we smooth the clip step at position stepIndex while maintaining the
        // step range stored in rawQueue.

        statusLabel.field.text = maCtl.controller.name + ": "
          + ((int) Math.Round( 100*(timenow - starttime) / (endTimeClamp - starttime) )).ToString() + "%";
          // Provide progress information

        timeSpanClamp = Math.Min(Math.Min(timenow-starttime,runSettings.timeSpan),endTimeClamp-timenow);

        while (rawQueue.Count > 0 && rawQueue.Peek().timeStep < timenow - timeSpanClamp) {
          // Remove steps stored in rawQueue whose time position is earlier than
          // the smoothee's time minus timeSpanClamp
          rawQueue.Dequeue();
        }

        while ( fwdIndex < clipSteps.Count && (clipSteps[fwdIndex].timeStep < timenow + timeSpanClamp) ) {
          // Add steps ahead of the current one whose time positions are within the timeSpanFS
          // -- while making sure the current step, at least, always makes it into the queue
          // (even when timeSpanFS is very small and/or time between steps is large).
          rawQueue.Enqueue(CloneStep(clipSteps[fwdIndex]));
          fwdIndex++;
        }

        MotionAnimationStep mrsmooth;

        if (rawQueue.Count > 2) {
          // The smoothing function is undefined for fewer than 3 timesteps, so we may
          // have to wait an extra timestep past the configured start time and/or cut out
          // early at the end.

          mrsmooth = SmoothQueue(timenow);

          OverwriteStep(clipSteps[stepIndex],mrsmooth);
        }

        stepIndex++;
        if (stepIndex >= clipSteps.Count-1) {
          // All done with this entire clip
          clipSteps = null;
          maClip = null;
          maCtl = null;
          rawQueue.Clear();
        }

      } // while stopWatch

      // Time's up! Return control to main thread & pick up where we left off on next call.
      // But hey: did we finish?
      //
      if (!running) { // tidy up
        rawQueue = null;
        stopWatch.Reset();
        stopWatch = null;
        goButton.label = GOBUTTON_GO;
        statusLabel.field.text = STATUS_COMPLETE;
        runSettings = null;
      }

    } // ContinueSmoothing()

    protected MotionAnimationStep SmoothQueue(float centertime) {
      // Returns a new MotionAnimationStep resulting from smoothing of all
      // steps in rawQueue based on the given centertime

      float totalweight = 0;
      float[] weight = new float[rawQueue.Count];
      float[] adjweight = new float[rawQueue.Count];

      // Normalize the step weights (such that they add up to 1.0)
      //
      int i = 0;
      foreach (MotionAnimationStep step in rawQueue) {
        float wt = StepWeight(step.timeStep, centertime);
        // if (float.IsNaN(wt)) SuperController.LogError("** GOT NaN : timeSpanClamp=" + timeSpanClamp.ToString()
          // + "  step.timeStep=" + step.timeStep.ToString()
          // + "  centertime=" + centertime.ToString()
          // + "  rawQueue.Count=" + rawQueue.Count.ToString()
        // );
        weight[i] = wt;
        adjweight[i] = wt;
        totalweight += wt;
        i++;
      }

      for (i = 0; i<rawQueue.Count; i++) {
        weight[i] /= totalweight;
        adjweight[i] = weight[i];
      }

      // Now compute adjusted weighting factors to apply to each Quaternion.Slerp
      // so that the final contribution of each rotation is equal to its intended StepWeight
      //
      for (i = rawQueue.Count-2; i > 0; i--) {
        for (int j = rawQueue.Count-1; j > i; j--) {

          // Since sum(adjweight)=1, it can be shown that each of these adjusted values is
          // between 0 and 1 (hence a valid slerp factor).  It can also be shown that when
          // the first step's rotation is used as the starting point, its intended StepWeight
          // factor has been applied once the slerping is done (tho the value goes unused).
          // One last thing: adjweight[0]=1.0 always (but we won't actually need it, and
          // so won't compute it).
          //
          adjweight[i] /= (1-adjweight[j]);
        }
      }

      // Finally, the lerping & slerping
      //
      Vector3 smoothpos = new Vector3(0,0,0);
      Quaternion smoothrot = new Quaternion();
      float smoothtime = 0;

      i = 0;
      foreach (MotionAnimationStep step in rawQueue) {

        if (i < 1) {
          smoothpos = step.position;
          smoothrot = step.rotation;
        } else {
          smoothpos = Vector3.Lerp(smoothpos,step.position,adjweight[i]);
          smoothrot = Quaternion.Slerp(smoothrot,step.rotation,adjweight[i]);
        }

        smoothtime += step.timeStep * weight[i];

        i++;
      }

      MotionAnimationStep smoothstep = new MotionAnimationStep();
      smoothstep.position = smoothpos;
      smoothstep.rotation = smoothrot;
      smoothstep.timeStep = smoothtime;

      return smoothstep;
    }

    protected float StepWeight(float steptime, float centertime) {
      // Returns the weighting factor for the given step time based on the given center time
      // and the configured timeSpanFS.

      float pct = Math.Abs((timeSpanClamp - Math.Abs(centertime - steptime))) / timeSpanClamp;
        // This function will only ever be evaluated for steptime values no further than
        // timeSpanFS from centertime; so this value must be between 0 and 1.  On the other
        // hand, unexpected conditions like out-of-order animation steps could violate
        // this assumption; so take a final Abs(), as we could end up with an imaginary
        // number with fractional or negative center weights if pct < 0.

      return (float) Math.Pow((double) pct,(double) centerWeightFS.val);
    }

    protected MotionAnimationStep CloneStep(MotionAnimationStep instep) {
      // Returns a copy of the given step
      MotionAnimationStep outstep = new MotionAnimationStep();
      outstep.position = instep.position;
      outstep.positionOn = instep.positionOn;
      outstep.rotation = instep.rotation;
      outstep.rotationOn = instep.rotationOn;
      outstep.timeStep = instep.timeStep;

      return outstep;
    }

    protected void OverwriteStep(MotionAnimationStep tostep, MotionAnimationStep fromstep) {
      // Replace all values in tostep with those from fromstep

      // Have not been able to identify conditions which spoil the position averaging; but
      // it's probably just as cheap to just let it happen & check for NaNs here.  Seems
      // to be associated with extreme center weights (negative especially) and/or excessive
      // re-smoothing.  Probably uncommon in practical cases.  Have not observed NaNs in
      // the rotation outcome.
      //
      if ( float.IsNaN(fromstep.position.x)
        || float.IsNaN(fromstep.position.y)
        || float.IsNaN(fromstep.position.z) ) {
        // SuperController.LogError("Warning: discarding smoothed step with invalid position (NaN) at timeStep ="
          // + tostep.timeStep.ToString());
        return;
      }

      tostep.position = fromstep.position;
      tostep.rotation = fromstep.rotation;

      if (centerWeightFS.val >= 0) tostep.timeStep = fromstep.timeStep;
        // When negative center weights are used, smoothing of the timestep can lead to
        // alterations in the order of timesteps.  This is obviously undesireable; causes
        // problems with the smoothing queue; and and could lead to instability.  So users
        // who actually wish to try this weird kind of smoothing (where the step being
        // smoothed counts *less* than any of its neighbors) will have to accept this
        // compromise.

      // Leave .positionOn and .rotationOn alone.
    }

    #endregion // private methods

    #region "VaM Plugin Methods"

    public override void Init() {
      try {
        MaM = SuperController.singleton.motionAnimationMaster;
        BuildCustomUI();
      } catch (Exception e) {
        SuperController.LogError("Exception caught in Init(): " + e);
      }
    }

    // Start is called once before Update or FixedUpdate is called and after Init()
    void Start() {
    }

    // Update is called with each rendered frame by Unity
    void Update() {
      try {
        if (running) ContinueSmoothing();
      } catch (Exception e) {
        running = false;
        SuperController.LogError("Exception caught in Update(): " + e);
      }
    }

    // FixedUpdate is called with each physics simulation frame by Unity
    void FixedUpdate() {
    }

    // OnDestroy is where you should put any cleanup
    // if you registered objects to supercontroller or atom, you should unregister them here
    void OnDestroy() {
    }

    #endregion // plugin methods

  }
}
