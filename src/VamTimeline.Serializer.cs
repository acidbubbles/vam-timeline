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

        public State DeserializeState(string val)
        {
            var json = JSON.Parse(val);
            // TODO
            return new State();
        }

        public string SerializeState(State state)
        {
            var json = new JSONClass();
            // TODO
            return json.ToString();
        }
    }
}
