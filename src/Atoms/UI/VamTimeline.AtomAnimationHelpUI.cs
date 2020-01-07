using System.Collections.Generic;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationHelpUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Help";
        public override string Name => ScreenName;

        public AtomAnimationHelpUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            var helpJSON = new JSONStorableString("Page", "");
            var pagesJSON = new JSONStorableStringChooser(
                "Pages", new List<string>{
                "Basic setup"
            },
            "",
            "Pages",
            (string val) =>
            {
                switch (val)
                {
                    case "Basic setup":
                        helpJSON.val = @"
It is expected that you have some basic knowledge of how Virt-A-Mate works before getting started. Basic knowledge of keyframe based animation is also useful. In a nutshell, you specify some positions at certain times, and all positions in between will be interpolated using curves (linear, smooth, etc.).

You can find out more on the project site: https://github.com/acidbubbles/vam-timeline

Building your first animation:

1. Add the VamTimeline.AtomAnimation.cslist plugin on atoms you want to animate, and open the plugin settings (Open Custom UI in the Atom's Plugin section).
2. In Animation Settings screen, select a controller you want to animate in the Animate Controller drop down, and select Add Controller to include it. This will turn on the ""position"" and ""rotation"" controls for that controller if that's not already done.
3. You can now select the Controllers tab by using the top-left drop-down. Your controller is checked, that means there is a keyframe at this time in the timeline.
4. To add a keyframe, move the Time slider to where you want to create a keyframe, and move the controller you have included before. This will create a new keyframe. You can also check the controller's toggle. Unchecking the toggle will delete that keyframe for that controller. Try navigating using the Next Frame and Previous Frame buttons, and try your animation using the Play button.
5. There is a text box on the top right; this shows all frames, and for the current frame (the current frame is shown using square brackets), the list of affected controllers. This is not as good as an actual curve, but you can at least visualize your timeline.
".Trim();
                        break;

                    case "Multiple animations":
                        helpJSON.val = @"
You can add animations with Add New Animation button in the Animatin Settings tab. This will port over all controller positions from the currently displayed keyframe, as well as the length of the current animation. Note that if you later add more controllers, they will not be assigned to all animations. This means that when you switch between animations, controllers that were not added in the second animation will simply stay where they currently are.

You can switch between animations using the Animation drop down. When the animation is playing, it will smoothly blend between animations during the value specified in Blend Duration.
".Trim();
                        break;

                    case "Morphs and params":
                        helpJSON.val = @"
You can animate morphs and any other float param, such as light intensity, skin specular, etc. You can add them like controllers in the Animation Settings tab. Then, in the Params tab, you can use the toggle to create keyframes, or use the sliders to change values and create keyframes at the current time.
".Trim();
                        break;

                    case "Performance":
                        helpJSON.val = @"
o gain a little bit of performance, you can use the Locked screen. It will reduce processing a little bit, and prevent moving controllers by accident.
".Trim();
                        break;

                    case "Triggering events":
                        helpJSON.val = @"
To use events, you can use an AnimationPattern of the same length as the animation. When an Animation Pattern is linked, it will play, stop and scrub with the VamTimeline animation.
".Trim();
                        break;

                    case "External controller":
                        helpJSON.val = @"
This allows creating a floating payback controller, and control multiple atoms together. Create a Simple Sign atom and add the script to it. This is optional, you only need this if you want to animate more than one atom, or if you want the floating playback controls.

Add the VamTimeline.Controller.cslist plugin on a Simple Sign atom.

In the plugin settings, select the animations you want to control and select Link.

You can now control the animations in the floating panel; you can also select which atom and animation to play.

Note that all specified atoms must contain the same animations, and animations must have the same length.
".Trim();
                        break;

                    case "Keyboard shortcuts":
                        helpJSON.val = @"
When the Controller Plugin has been added, you can use the left/right keyboard arrows to move between keyframes, up/down to move between filter targets, and spacebar to play/stop the animation.
".Trim();
                        break;

                    case "Interacting with scenes":
                        helpJSON.val = @"
Playing, stopping and otherwise interacting with this plugin is possible using storables. For example, you can play a specific animation when a mouth colliders triggers, or when an animation patterns reaches a certain point. This can create some intricate relationships between animations and interactivity.
".Trim();
                        break;

                    case "About":
                        helpJSON.val = @"
Plugin developed by Acid Bubbles in January 2020.

Built because I miss Source Filmmaker!

Please report any issues or suggestions to https://github.com/acidbubbles/vam-timeline or on Discord, make sure to tag @Acidbubbles!
".Trim();
                        break;

                    case "__Template":
                        helpJSON.val = @"
".Trim();
                        break;

                    default:
                        helpJSON.val = "Page Not Found";
                        break;
                }
            });

            Plugin.CreateScrollablePopup(pagesJSON);
            _linkedStorables.Add(pagesJSON);

            var helpUI = Plugin.CreateTextField(helpJSON, true);
            helpUI.height = 1200;
            _linkedStorables.Add(helpJSON);

            pagesJSON.val = "Basic setup";
        }
    }
}

