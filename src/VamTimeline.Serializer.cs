using System;
using SimpleJSON;

namespace AcidBubbles.VamTimeline
{
    /// <summary>
    /// VaM Timeline Controller
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class Serializer
    {
        private readonly MVRScript _script;

        public Serializer(MVRScript mainScript)
        {
            _script = mainScript;
        }

        public AtomAnimation DeserializeState(string val)
        {
            var json = JSON.Parse(val);
            // TODO
            return new AtomAnimation();
        }

        public string SerializeState(AtomAnimation state)
        {
            var json = new JSONClass();
            // TODO
            return json.ToString();
        }
    }
}
