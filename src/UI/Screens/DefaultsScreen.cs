using System;
using System.Text;

namespace VamTimeline
{
    public class DefaultsScreen : ScreenBase
    {
        public const string ScreenName = "Defaults";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            var createDefaultsBtn = prefabFactory.CreateButton("Save as default");
            createDefaultsBtn.button.onClick.AddListener(() =>
            {
                TimelineDefaults.singleton.Save();
                SuperController.LogMessage($"Timeline: Settings saved to '{TimelineDefaults.DefaultsPath}'.");
            });

            var deleteDefaultsBtn = prefabFactory.CreateButton("Delete current defaults");
            deleteDefaultsBtn.button.onClick.AddListener(() =>
            {
                TimelineDefaults.singleton.Delete();
                SuperController.LogMessage("Timeline: Settings deleted. Reload to revert to defaults options.");
            });

            prefabFactory.CreateHeader("Current", 2);
            {
                var sb = new StringBuilder();
                TimelineDefaults.singleton.GetJSON().ToString("", sb);
                var currentDefaultsJSON = new JSONStorableString("Live", sb.ToString());
                var currentDefaultsUI = prefabFactory.CreateTextField(currentDefaultsJSON);
                currentDefaultsUI.height = 400f;
            }

            prefabFactory.CreateHeader("On Disk", 2);
            {
                if (TimelineDefaults.singleton.Exists())
                {
                    var sb = new StringBuilder();
                    try
                    {
                        var json = SuperController.singleton.LoadJSON(TimelineDefaults.DefaultsPath);

                        json.ToString("", sb);
                    }
                    catch (Exception exc)
                    {
                        sb.AppendLine($"An error occured while trying to open the file: {exc}");
                    }
                    var currentDefaultsJSON = new JSONStorableString("OnDisk", sb.ToString());
                    var currentDefaultsUI = prefabFactory.CreateTextField(currentDefaultsJSON);
                    currentDefaultsUI.height = 400f;
                }
                else
                {
                    var currentDefaultsJSON = new JSONStorableString("OnDisk", "[FILE NOT FOUND]");
                    var currentDefaultsUI = prefabFactory.CreateTextField(currentDefaultsJSON);
                    currentDefaultsUI.height = 60f;
                }
            }
        }
    }
}

