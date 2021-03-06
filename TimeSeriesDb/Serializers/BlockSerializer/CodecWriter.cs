#region COPYRIGHT

/*
 *     Copyright 2009-2012 Yuri Astrakhan  (<Firstname><Lastname>@gmail.com)
 *
 *     This file is part of TimeSeriesDb library
 * 
 *  TimeSeriesDb is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 * 
 *  TimeSeriesDb is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 * 
 *  You should have received a copy of the GNU General Public License
 *  along with TimeSeriesDb.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

#endregion

using System;
using JetBrains.Annotations;

namespace NYurik.TimeSeriesDb.Serializers.BlockSerializer
{
    public class CodecWriter : CodecBase
    {
        /// All buffers are created slightly bigger than needed so that WriteOperations do not need to check for buffer end for every byte
        private const int PaddingSize = MaxBytesFor64;

        public readonly byte[] Buffer;
        public readonly int BufferSize;
        private int _count;
        private int _failedCount;

        /// <summary> Create writing codec. The internal buffer is padded with extra space. </summary>
        public CodecWriter(int bufferSize)
        {
            if (bufferSize <= ReservedSpace)
                throw new ArgumentOutOfRangeException("bufferSize", bufferSize, "Must be > " + ReservedSpace);
            BufferSize = bufferSize;
            Buffer = new byte[bufferSize + PaddingSize];
        }

        public int Count
        {
            get { return _count; }
            set
            {
                if (value < 0 || value > BufferSize)
                    throw new ArgumentOutOfRangeException("value", value, "Must be >= 0 && <= " + BufferSize);
                _count = value;
            }
        }

        public ArraySegment<byte> AsArraySegment()
        {
            return new ArraySegment<byte>(Buffer, 0, Count);
        }

        #region Header & Hash calculation

        [UsedImplicitly]
        internal void FinishBlock(int count, bool makeFullBlock)
        {
            // Write needed bytes at the end, calculate how many bytes it took
            // shift array right and copy to the value to the beginning.
            ValidateCount(count);

            // Can be read by 64bit unsigned reader
            // Writing will always put new bytes into the buffer because it has extra padding
            // but it might be beyond the max size, in which case we use _failedCount instead of _count.
            int tmp = _count;
            if (!WriteUnsignedValue((uint) count))
                _count = _failedCount;
            tmp = _count - tmp;

            Array.Copy(Buffer, 0, Buffer, tmp, _count);
            Array.Copy(Buffer, _count, Buffer, 0, tmp);

            // Write as many bytes as would fit of the MD5 signature
            byte[] hash = HashAlgorithm.ComputeHash(Buffer, 0, _count);
            int hashSize = hash.Length;
            if (hashSize >= BufferSize - _count)
                hashSize = BufferSize - _count;
            Array.Copy(hash, 0, Buffer, _count, hashSize);
            _count += hashSize;

            if (makeFullBlock && _count < BufferSize)
            {
                Array.Clear(Buffer, _count, BufferSize - _count);
                _count = BufferSize;
            }
        }

        #endregion

        #region Writing values

        [UsedImplicitly]
        internal bool WriteUnsignedValue(ulong value)
        {
            ThrowIfNotEnoughSpace(MaxBytesFor64);
            int pos = _count;
            while (value > 127)
            {
                Buffer[pos++] = (byte) (value & 0x7F | 0x80);
                value >>= 7;
            }
            Buffer[pos++] = (byte) value;

            if (pos > BufferSize - ReservedSpace)
            {
                _failedCount = pos;
                return false;
            }

            _count = pos;
            return true;
        }

        [UsedImplicitly]
        internal bool WriteUnsignedValue(uint value)
        {
            ThrowIfNotEnoughSpace(MaxBytesFor32);
            int pos = _count;
            while (value > 127)
            {
                Buffer[pos++] = (byte) (value & 0x7F | 0x80);
                value >>= 7;
            }
            Buffer[pos++] = (byte) value;

            if (pos > BufferSize - ReservedSpace)
            {
                _failedCount = pos;
                return false;
            }

            _count = pos;
            return true;
        }

        [UsedImplicitly]
        internal bool WriteByte(byte value)
        {
            ThrowIfNotEnoughSpace(MaxBytesFor8);

            int pos = _count;
            Buffer[pos] = value;
            if (pos >= BufferSize - ReservedSpace)
            {
                _failedCount = pos + 1;
                return false;
            }

            _count = pos + 1;
            return true;
        }

        /// <summary>
        /// Write signed long value in compressed form, returning true if it fits, or false otherwise
        /// </summary>
        [UsedImplicitly]
        internal bool WriteSignedValue(long value)
        {
            ThrowIfNotEnoughSpace(MaxBytesFor64);
            int pos = _count;

            if (value < 0)
            {
                while (true)
                {
                    var v = (byte) (value & 0x7F);
                    value = value >> 7;

                    // Shifting a signed value right fills leftmost positions with 1s
                    if (value != ~0 || (v & 0x40) == 0)
                        Buffer[pos++] = (byte) (v | 0x80);
                    else
                    {
                        Buffer[pos++] = v;
                        break;
                    }
                }
            }
            else
            {
                while (true)
                {
                    var v = (byte) (value & 0x7F);
                    value = value >> 7;

                    if (value != 0 || (v & 0x40) != 0)
                        Buffer[pos++] = (byte) (v | 0x80);
                    else
                    {
                        Buffer[pos++] = v;
                        break;
                    }
                }
            }

            if (pos > BufferSize - ReservedSpace)
            {
                _failedCount = pos;
                return false;
            }

            _count = pos;
            return true;
        }

        #endregion

        #region Exceptions

        [UsedImplicitly]
        internal void ThrowOverflow<T>(T value)
        {
            throw new OverflowException(string.Format("Value {0} cannot be stored", value));
        }

        [UsedImplicitly]
        internal void ThrowSerializer(string format, object value1)
        {
            throw new SerializerException(format, value1);
        }

        [UsedImplicitly]
        internal void ThrowSerializer(string format, object value1, object value2)
        {
            throw new SerializerException(format, value1, value2);
        }

        /// <summary>
        /// TODO: Make this method Debug only!
        /// </summary>
        /// <param name="neededSpace"></param>
        private void ThrowIfNotEnoughSpace(byte neededSpace)
        {
            if (_count + neededSpace >= Buffer.Length)
                throw new SerializerException(
                    "Unable to perform write operation requiring {0} bytes - buffer[{1}] is {2} bytes full",
                    neededSpace, BufferSize, Count);
        }

        #endregion
    }
}