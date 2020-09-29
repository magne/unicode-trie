using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    public abstract class Buffer
    {
        internal static readonly Unsafe UNSAFE = Unsafe.getUnsafe();

        private            int                mark = -1;
        private            int                _position;
        private            int                _limit;
        private            int                capacity;
        protected          long               address;
        protected readonly MemorySegmentProxy segment;

        protected Buffer(int mark, int pos, int lim, int cap, MemorySegmentProxy segment)
        {
            if (cap < 0)
            {
                throw createCapacityException(cap);
            }

            capacity = cap;
            this.segment = segment;
            limit(lim);
            position(pos);
            if (mark >= 0)
            {
                if (mark > pos)
                {
                    throw new ArgumentException("mark > position: (" + mark + " > " + pos + ")");
                }

                this.mark = mark;
            }
        }

        static ArgumentException createCapacityException(int capacity)
        {
            // TODO assert capacity < 0 : "capacity expected to be negative";

            return new ArgumentException("capacity < 0: (" + capacity + " < 0)");
        }

        public int position()
        {
            return _position;
        }

        public Buffer position(int newPosition)
        {
            if (newPosition > _limit | newPosition < 0)
            {
                throw createPositionException(newPosition);
            }

            _position = newPosition;
            if (mark > _position)
            {
                mark = -1;
            }

            return this;
        }

        private ArgumentException createPositionException(int newPosition)
        {
            string msg;
            if (newPosition > _limit)
            {
                msg = "newPosition > limit: (" + newPosition + " > " + _limit + ")";
            }
            else
            {
                // TODO assert newPosition < 0 : "newPosition expected to be negative";

                msg = "newPosition < 0: (" + newPosition + " < 0)";
            }

            return new ArgumentException(msg);
        }

        public int limit()
        {
            return _limit;
        }

        public Buffer limit(int newLimit)
        {
            if (newLimit > capacity | newLimit < 0)
            {
                throw createLimitException(newLimit);
            }

            _limit = newLimit;
            if (_position > _limit)
            {
                _position = _limit;
            }

            if (mark > _limit)
            {
                mark = -1;
            }

            return this;
        }

        private ArgumentException createLimitException(int newLimit)
        {
            string msg;
            if (newLimit > capacity)
            {
                msg = "newLimit > capacity: (" + newLimit + " > " + capacity + ")";
            }
            else
            {
                // TODO assert newLimit < 0 : "newLimit expected to be negative";

                msg = "newLimit < 0: (" + newLimit + " < 0)";
            }

            return new ArgumentException(msg);
        }

        public Buffer rewind()
        {
            _position = 0;
            mark = -1;
            return this;
        }

        public int remaining()
        {
            return _limit - _position;
        }

        internal int nextGetIndex()
        {
            if (_position >= _limit)
            {
                throw new Exception("BufferUnderflowException");
            }

            return _position++;
        }

        internal int nextGetIndex(int nb)
        {
            if (_limit - _position < nb)
            {
                throw new Exception("BufferUnderflowException");
            }

            int p = _position;
            _position += nb;
            return p;
        }

        internal int checkIndex(int i)
        {
            if (i >= 0 && i < _limit)
            {
                return i;
            }

            throw new Exception("IndexOutOfBoundsException");
        }

        internal void checkSegment()
        {
            segment?.checkValidState();
        }
    }
}