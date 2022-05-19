namespace VamTimeline
{
    public class HelpScreen : ScreenBase
    {
        public const string ScreenName = "Help";

        private const string _helpText = @"
<b>Welcome to Timeline!</b>

Create advanced and dynamic curve-based animations using keyframes and triggers.

There is documentation available in the wiki (accessible from the More menu), as well as video tutorials.

<b>The UI</b>

At the top you can find <i>tabs</i>. This is how you'll navigate between screens.

On your left, you can find the list of animations. By default, there's only one: 'Anim 1'. You can also have Layers and Segments (see below) if you use them.

Under, you have the keyframe navigation controls. The leftmost and rightmost buttons go to the next/previous frame, the inner buttons let you move backward and forward, and the center button 'snaps' to the closest second.

There are two Play buttons. ""All"" is used to play all layers at once, and will play sequences. The second (named after the current animation) will only play the current clip.

Then you have the <i>scrubber</i>. It shows the animation time and where you currently are. You can move the time back and forth, and zoom in/out.

Underneath, you have the <i>dope sheet</i>. This shows you all <i>targets</i> (e.g. a hand control or a smile morph), and whether there's a keyframe at any point in time. You can switch to the Curves view using the top-left button.

Finally, you have buttons to delete, copy and paste; they will affect the currently selected keyframes in the dope sheet.

<b>Your first animation</b>

Select the 'Add/remove targets' button in the Targets panel.

Choose what you want to animate. For example, the right hand controller: select rHandControl in the Control drop down, and press Add. You'll see the target added on the dope sheet.

Move the right hand to a new position, move the scrubber to 1s, and move the right hand to another position.

Now, rewind (press Stop) and Play. You'll see the right hand move back and forth between the two positions. This is because the animation is <i>looping</i>. You can change this behavior in the Edit tab.

<b>Sequencing</b>

You can create new animations and automatically blend between them. Create another animation (Animations, Create, Create animation) and go to the Sequence tab. You can see in the Play next drop down the other animation. If you select it, it will automatically play. Check out the wiki for more information on sequencing features.

You can name your animations 'prefix/something' to play a subset of animations, they will show up as 'prefix/*' in the Play next drop down.

<b>Layers and segments</b>

You can create layers in Animations, Create. Layers allow you to have multiple animations running at the same time, each affecting its own targets. For example, you could have a layer to animate breathing, and another layer to animate the hands. Each will have its own animations and sequencing.

You can also have multiple animations sharing the same name across layers; they will always play and scrub together.

If you need to create animations that use different targets, you can create segments. Segments are like completely independent animations, with their own layers. Only one segment can play at a time, except the shared segment.

<b>Learning</b>

Check out the wiki (link in the More menu) for more documentation and videos. There are a ton of things you can do! Now have fun!
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

