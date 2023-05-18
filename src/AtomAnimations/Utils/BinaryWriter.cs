namespace System
{
    public class BinaryWriter
    {
        public readonly MemoryStream BaseStream;

        public BinaryWriter(MemoryStream stream)
        {
            BaseStream = stream;
        }

        public void Write(byte value)
        {
            BaseStream.Write(new byte[] { value }, 0, 1);
        }

        public void Write(int value)
        {
            byte[] buffer = System.BitConverter.GetBytes(value);
            BaseStream.Write(buffer, 0, buffer.Length);
        }

        public void Write(long value)
        {
            byte[] buffer = System.BitConverter.GetBytes(value);
            BaseStream.Write(buffer, 0, buffer.Length);
        }

        public void Write(string value)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(value);
            Write(buffer.Length);
            BaseStream.Write(buffer, 0, buffer.Length);
        }

        // Add more methods as needed for other data types
    }

}
