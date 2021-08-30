using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class AtomAnimationBackup
    {
        public static readonly AtomAnimationBackup singleton = new AtomAnimationBackup();

        private AtomAnimationClip _owner;
        private List<ICurveAnimationTarget> _backup;
        public string backupTime { get; private set; }

        public bool HasBackup(AtomAnimationClip owner)
        {
            return _backup != null && _owner == owner;
        }

        public void TakeBackup(AtomAnimationClip owner)
        {
            ClearBackup();
            _owner = owner;
            _backup = owner.GetAllCurveTargets().Where(t => t.selected).Select(t => t.Clone(true)).ToList();
            backupTime = DateTime.Now.ToShortTimeString();
        }

        public void RestoreBackup(AtomAnimationClip owner)
        {
            if (!HasBackup(owner)) return;
            var targets = owner.GetAllCurveTargets().Where(t => t.selected).ToList();
            foreach (var backup in _backup)
            {
                var target = targets.FirstOrDefault(t => t.TargetsSameAs(backup));
                target?.RestoreFrom(backup);
            }
        }

        public void ClearBackup()
        {
            _owner = null;
            _backup = null;
            backupTime = null;
        }
    }
}
