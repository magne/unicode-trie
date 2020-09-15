using System;
// ReSharper disable InconsistentNaming

namespace unicode_trie.java
{
    public abstract class Buffer
    {
        internal static readonly Unsafe UNSAFE = Unsafe.getUnsafe();

        private            int                mark      = -1;
        private            int                _position = 0;
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
            else
            {
                this.capacity = cap;
                this.segment = segment;
                this.limit(lim);
                this.position(pos);
                if (mark >= 0)
                {
                    if (mark > pos)
                    {
                        throw new ArgumentException("mark > position: (" + mark + " > " + pos + ")");
                    }

                    this.mark = mark;
                }
            }
        }

        static ArgumentException createCapacityException(int capacity)
        {
            // TODO assert capacity < 0 : "capacity expected to be negative";

            return new ArgumentException("capacity < 0: (" + capacity + " < 0)");
        }

        public int position()
        {
            return this._position;
        }

        public Buffer position(int newPosition)
        {
            if (newPosition > this._limit | newPosition < 0)
            {
                throw this.createPositionException(newPosition);
            }
            else
            {
                this._position = newPosition;
                if (this.mark > this._position)
                {
                    this.mark = -1;
                }

                return this;
            }
        }

        private ArgumentException createPositionException(int newPosition)
        {
            String msg = null;
            if (newPosition > this._limit)
            {
                msg = "newPosition > limit: (" + newPosition + " > " + this._limit + ")";
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
            return this._limit;
        }

        public Buffer limit(int newLimit)
        {
            if (newLimit > this.capacity | newLimit < 0)
            {
                throw this.createLimitException(newLimit);
            }
            else
            {
                this._limit = newLimit;
                if (this._position > this._limit)
                {
                    this._position = this._limit;
                }

                if (this.mark > this._limit)
                {
                    this.mark = -1;
                }

                return this;
            }
        }

        private ArgumentException createLimitException(int newLimit)
        {
            String msg = null;
            if (newLimit > this.capacity)
            {
                msg = "newLimit > capacity: (" + newLimit + " > " + this.capacity + ")";
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
            this._position = 0;
            this.mark = -1;
            return this;
        }

        public int remaining()
        {
            return this._limit - this._position;
        }

        internal int nextGetIndex()
        {
            if (this._position >= this._limit)
            {
                throw new Exception("BufferUnderflowException");
            }
            else
            {
                return this._position++;
            }
        }

        internal int nextGetIndex(int nb)
        {
            if (this._limit - this._position < nb)
            {
                throw new Exception("BufferUnderflowException");
            }
            else
            {
                int p = this._position;
                this._position += nb;
                return p;
            }
        }

        internal int checkIndex(int i)
        {
            if (i >= 0 && i < this._limit)
            {
                return i;
            }
            else
            {
                throw new Exception("IndexOutOfBoundsException");
            }
        }

        internal void checkSegment()
        {
            if (this.segment != null)
            {
                this.segment.checkValidState();
            }
        }
    }
}