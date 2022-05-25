using System.Collections.Specialized;

namespace IntegrityService.Utils
{
    /// <summary>
    ///     An ordered dictionary implementation with fixed size (default: 50).
    ///     When maximum size is reached, the first element is removed (FIFO).
    /// </summary>
    /// <typeparam name="TKey">A non-null Type of keys</typeparam>
    /// <typeparam name="TValue">Type of values</typeparam>
    internal sealed class FixedSizeDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly OrderedDictionary _dictionary;
        private readonly int _maxSize;

        /// <summary>
        ///     FixedSizeDictionary constructor with default size value of 50.
        /// </summary>
        /// <param name="size">Maximum number of elements the dictionary will handle.</param>
        public FixedSizeDictionary(int size = 50)
        {
            _dictionary = new OrderedDictionary(size);
            _maxSize = size;
        }

        public bool ContainsKey(TKey key) => _dictionary.Contains(key);

        public void AddOrUpdate(TKey key, TValue value)
        {
            if (_dictionary.Contains(key))
            {
                _dictionary[key] = value;
            }
            else
            {
                _dictionary.Add(key, value);
            }

            if (Count > _maxSize)
            {
                RemoveAt(0);
            }
        }

        public void Remove(TKey key) => _dictionary.Remove(key);

        public void RemoveAt(int index) => _dictionary.RemoveAt(index);

        public int Count => _dictionary.Count;

        public TValue? this[int index]
        {
            get
            {
                return (TValue)_dictionary[index]!;
            }
            set
            {
                _dictionary[index] = value;
            }
        }

        public TValue? this[TKey key]
        {
            get
            {
                return (TValue)_dictionary[key]!;
            }
            set
            {
                _dictionary[key] = value;
            }
        }

    }
}
