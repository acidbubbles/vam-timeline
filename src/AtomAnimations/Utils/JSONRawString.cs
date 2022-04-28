	using System.IO;
    using System.Text;

    namespace SimpleJSON
    {
        public class JSONBinaryData : JSONNode
        {
            // ReSharper disable once InconsistentNaming
            private string m_Data;

            public override string Value
            {
                get { return m_Data; }
                set { m_Data = value; }
            }

            public JSONBinaryData(string aData)
            {
                m_Data = aData;
            }

            public override string ToString()
            {
                return "\"" + m_Data + "\"";
            }

            public override string ToString(string aPrefix)
            {
                return "\"" + m_Data + "\"";
            }

            public override void ToString(string aPrefix, StringBuilder sb)
            {
                sb.Append("\"" + m_Data + "\"");
            }

            public override void Serialize(BinaryWriter aWriter)
            {
                aWriter.Write((byte)3);
                aWriter.Write(m_Data);
            }
        }
    }
