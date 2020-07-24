namespace VamTimeline
{
    public class HelpScreen : ScreenBase
    {
        public const string ScreenName = "Help";

        public const string HelpText = @"
<b>Welcome to Timeline!</b>

This plugin allows for advanced keyframe-based editing.

Remember that there is documentation available in the wiki (accessible from the More menu), as well as video tutorials.

<b>The UI</b>

On your left, you can find the list of animations. By default, there's only one: 'Anim 1'.

Then you have the <i>scrubber</i>. It shows the animation time.

Under, you have the frame navigation controls. The leftmost and righmost buttons go to the next/previous frame, the inner buttons let you move backward and forward, and the center button 'snaps' to the closed second.

There are two Play buttons. ""Seq"" is used to play all layers, and play sequences. ""Clip"" will only play the current clip.

Then you can see the <i>dope sheet</i>. This shows you all <i>targets</i> (e.g. a hand or a smile morph), and whether there's a keyframe at any point in time.

On the top you can find the <i>tabs</i>. This is how you'll navigate to the more advanced functions.

<b>Your first animation</b>

Select the add target button in the Edit panel.

Choose what you want to animate (for example, the right hand controller).

Move the right hand to a new position, move the scrubber to 1s, and move the right hand to another position.

Now, rewind (press stop) and play. You'll see the right hand move back and forth between the two positions. This is because the animation is <i>looping</i>.

<b>Learning</b>

Check out the wiki for more details and videos. There's a ton of things you can do! Now have fun!
";

        public override string screenId => ScreenName;

        public HelpScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            InitExplanation();
        }

        private void InitExplanation()
        {
            var textJSON = new JSONStorableString("Help", HelpText);
            var textUI = prefabFactory.CreateTextField(textJSON);
            textUI.height = 1070f;
        }
    }
}

