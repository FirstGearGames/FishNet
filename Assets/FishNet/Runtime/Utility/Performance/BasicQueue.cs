using System;

namespace FishNet.Utility.Performance
{

    /// <summary>
    /// Unity 2022 has a bug where codegen will not compile when referencing a Queue type,
    /// while also targeting .Net as the framework API.
    /// As a work around this class is used for queues instead.
    /// </summary>
    public class BasicQueue<T>
    {

        /// <summary>
        /// Number of elements in the queue.
        /// </summary>
        public int Count => _length;
        /// <summary>
        /// Collection containing data.
        /// </summary>
        private T[] _array = new T[4];
        /// <summary>
        /// Buffer for resizing.
        /// </summary>
        private T[] _resizeBuffer = new T[0];
        /// <summary>
        /// Read position of the next Dequeue.
        /// </summary>
        private int _read;
        /// <summary>
        /// Write position of the next Enqueue.
        /// </summary>
        private int _write;

        /// <summary>
        /// Length of the queue.
        /// </summary>
        private int _length;

        /// <summary>
        /// Enqueues an entry.
        /// </summary>
        /// <param name="data"></param>
        public void Enqueue(T data)
        {
            if (_length == _array.Length)
                Resize();

            if (_write >= _array.Length)
                _write = 0;
            _array[_write] = data;

            _write++;
            _length++;
        }

        /// <summary>
        /// Tries to dequeue the next entry.
        /// </summary>
        /// <param name="result">Dequeued entry.</param>
        /// <returns>True if an entry existed to dequeue.</returns>
        public bool TryDequeue(out T result)
        {
            if (_length == 0)
            {
                result = default;
                return false;
            }

            result = Dequeue();
            return true;
        }

        /// <summary>
        /// Dequeues the next entry.
        /// </summary>
        /// <returns></returns>
        public T Dequeue()
        {
            if (_length == 0)
                throw new Exception($"Queue of type {typeof(T).Name} is empty.");

            T result = _array[_read];

            _length--;
            _read++;
            if (_read >= _array.Length)
                _read = 0;

            return result;
        }

        /// <summary>
        /// Tries to peek the next entry.
        /// </summary>
        /// <param name="result">Peeked entry.</param>
        /// <returns>True if an entry existed to peek.</returns>
        public bool TryPeek(out T result)
        {
            if (_length == 0)
            {
                result = default;
                return false;
            }

            result = Peek();
            return true;
        }

        /// <summary>
        /// Peeks the next queue entry.
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            if (_length == 0)
                throw new Exception($"Queue of type {typeof(T).Name} is empty.");

            return _array[_read];
        }

        /// <summary>
        /// Clears the queue.
        /// </summary>
        /// <param name="makeDefault">True to make buffer entries default.</param>
        public void Clear()
        {
            _read = 0;
            _write = 0;
            _length = 0;

            DefaultCollection(_array);
            DefaultCollection(_resizeBuffer);

            void DefaultCollection(T[] array)
            {
                int count = array.Length;
                for (int i = 0; i < count; i++)
                    array[i] = default;
            }
        }

        /// <summary>
        /// Doubles the queue size.
        /// </summary>
        private void Resize()
        {
            int length = _length;
            int doubleLength = (length * 2);
            int read = _read;

            /* Make sure copy array is the same size as current
             * and copy contents into it. */
            //Ensure large enough to fit contents.
            T[] resizeBuffer = _resizeBuffer;
            if (resizeBuffer.Length < doubleLength)
                Array.Resize(ref resizeBuffer, doubleLength);
            //Copy from the read of queue first.
            int copyLength = (length - read);
            Array.Copy(_array, read, resizeBuffer, 0, copyLength);
            /* If read index was higher than 0
             * then copy remaining data as well from 0. */
            if (read > 0)
                Array.Copy(_array, 0, resizeBuffer, copyLength, read);

            //Set _array to resize.
            _array = resizeBuffer;
            //Reset positions.
            _read = 0;
            _write = length;
        }

    }

}