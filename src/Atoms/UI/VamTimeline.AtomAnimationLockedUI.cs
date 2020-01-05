using System.Linq;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationLockedUI : AtomAnimationBaseUI
    {
        public AtomAnimationLockedUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            // Right side

            InitLockedUI(true);
        }
    }
}

