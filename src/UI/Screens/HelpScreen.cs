namespace VamTimeline
{
    public class HelpScreen : ScreenBase
    {
        public const string ScreenName = "Help";

        private const string _helpText = @"
<b>Welcome to Timeline!</b>

This plugin allows creating advanced keyframe-based animations.

There is documentation available in the wiki (accessible from the More menu), as well as video tutorials.

<b>The UI</b>

On your left, you can find the list of animations. By default, there's only one: 'Anim 1'.

Under, you have the keyframe navigation controls. The leftmost and righmost buttons go to the next/previous frame, the inner buttons let you move backward and forward, and the center button 'snaps' to the closest second.

There are two Play buttons. ""All"" is used to play all layers at once, and will play sequences. The second (named after the current animation) will only play the current clip.

Then you have the <i>scrubber</i>. It shows the animation time and where you currently are. You can move the time back and forth, and zoom in/out.

Underneath, you have the <i>dope sheet</i>. This shows you all <i>targets</i> (e.g. a hand or a smile morph), and whether there's a keyframe at any point in time. You can switch to the Curves view using the top-left button.

On the top you can find the <i>tabs</i>. This is how you'll navigate to the more advanced functions.

<b>Your first animation</b>

Select the 'Add/remove targets' button in the Targets panel.

Choose what you want to animate (for example, the right hand controller).

Move the right hand to a new position, move the scrubber to 1s, and move the right hand to another position.

Now, rewind (press stop) and play. You'll see the right hand moves back and forth between the two positions. This is because the animation is <i>looping</i>.

<b>Learning</b>

Check out the wiki (link in the More menu) for more documentation and videos. There's a ton of things you can do! Now have fun!
";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            InitExplanation();
        }

        private void InitExplanation()
        {
            var textJSON = new JSONStorableString("Help", _helpText);
            var textUI = prefabFactory.CreateTextField(textJSON);
            textUI.height = 1070f;
        }
    }
}

