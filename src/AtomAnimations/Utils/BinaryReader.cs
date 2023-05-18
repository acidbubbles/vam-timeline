namespace System
{
    public class BinaryReader
    {
        public readonly MemoryStream BaseStream;

        public BinaryReader(MemoryStream stream)
        {
            BaseStream = stream;
        }

        public byte ReadByte()
        {
            byte[] buffer = new byte[1];
            int read = BaseStream.Read(buffer, 0, 1);
            if (read < 1) throw new System.Exception("End of stream reached");
            return buffer[0];
        }

        public byte[] ReadBytes(int length)
        {
            byte[] buffer = new byte[length];
            int read = BaseStream.Read(buffer, 0, length);
            if (read < length) throw new System.Exception("End of stream reached");
            return buffer;
        }

        public bool ReadBoolean()
        {
            byte[] buffer = new byte[1];
            int read = BaseStream.Read(buffer, 0, 1);
            if (read < 1) throw new System.Exception("End of stream reached");
            return buffer[0] != 0;
        }

        public int ReadInt32()
        {
            byte[] buffer = new byte[4];
            int read = BaseStream.Read(buffer, 0, 4);
            if (read < 4) throw new System.Exception("End of stream reached");
            return System.BitConverter.ToInt32(buffer, 0);
        }

        public float ReadSingle()
        {
            byte[] buffer = new byte[8];
            int read = BaseStream.Read(buffer, 0, 8);
            if (read < 8) throw new System.Exception("End of stream reached");
            return System.BitConverter.ToSingle(buffer, 0);
        }

        public string ReadString()
        {
            int length = ReadInt32();
            byte[] buffer = new byte[length];
            int read = BaseStream.Read(buffer, 0, length);
            if (read < length) throw new System.Exception("End of stream reached");
            return System.Text.Encoding.UTF8.GetString(buffer);
        }

        // Add more methods as needed for other data types
    }

}
