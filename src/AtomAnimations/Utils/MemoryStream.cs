namespace System
{
    public class MemoryStream
    {
        private const int InitialCapacity = 256;
        private const float GrowthFactor = 1.5f;

        private byte[] _buffer;
        private int _position;

        public int Position => _position;

        public MemoryStream()
        {
            _buffer = new byte[InitialCapacity];
        }

        public MemoryStream(byte[] buffer)
        {
            _buffer = buffer;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            CheckArguments(buffer, offset, count);
            EnsureCapacity(_position + count);
            Buffer.BlockCopy(buffer, offset, _buffer, _position, count);
            _position += count;
        }

        public void WriteByte(byte value)
        {
            EnsureCapacity(_position + 1);
            _buffer[_position++] = value;
        }

        public byte[] GetBuffer()
        {
            var result = new byte[_position];
            Buffer.BlockCopy(_buffer, 0, result, 0, _position);
            return result;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            CheckArguments(buffer, offset, count);
            var remaining = _buffer.Length - _position;
            if (remaining <= 0) return 0;
            if (remaining < count) count = remaining;
            Buffer.BlockCopy(_buffer, _position, buffer, offset, count);
            _position += count;
            return count;
        }

        public void Seek(int position)
        {
            if (position < 0 || position > _buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(position));
            _position = position;
        }

        private void CheckArguments(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (offset + count > buffer.Length)
                throw new ArgumentException("The sum of offset and count is larger than the buffer length.");
        }

        private void EnsureCapacity(int required)
        {
            if (_buffer.Length < required)
            {
                var newCapacity = (int)(required * GrowthFactor);
                var newBuffer = new byte[newCapacity];
                Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _buffer.Length);
                _buffer = newBuffer;
            }
        }
    }
}
