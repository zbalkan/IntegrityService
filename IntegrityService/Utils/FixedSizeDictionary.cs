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
        private const int DEFAULT_SIZE = 50;
        private readonly OrderedDictionary _dictionary;

        /// <summary>
        ///     FixedSizeDictionary constructor with default size value of 50.
        /// </summary>
        /// <param name="size">Maximum number of elements the dictionary will handle.</param>
        public FixedSizeDictionary(int size = DEFAULT_SIZE)
        {
            _dictionary = new OrderedDictionary(size);
            Capacity = size;
        }

        public bool ContainsKey(TKey key) => _dictionary.Contains(key);

        /// <summary>
        ///     Adds, or updates a new entry. If the capacity is reached, removes the first record.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <exception cref="System.NotSupportedException"></exception>
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

            if (Count > Capacity)
            {
                RemoveAt(0);
            }
        }

        /// <summary>
        ///     Removes the entry with the specified key from <see cref="FixedSizeDictionary{TKey, TValue}"/>
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="System.NotSupportedException"></exception>
        public void Remove(TKey key) => _dictionary.Remove(key);

        /// <summary>
        ///     Removes the entry from the specified index from <see cref="FixedSizeDictionary{TKey, TValue}"/>
        /// </summary>
        /// <param name="index">Index of the entry to remove</param>
        /// <exception cref="System.NotSupportedException"></exception>
        public void RemoveAt(int index) => _dictionary.RemoveAt(index);

        /// <summary>
        ///     Gets the number of entries within <see cref="FixedSizeDictionary{TKey, TValue}"/>
        /// </summary>
        public int Count => _dictionary.Count;

        /// <summary>
        ///     Gets the maximum number of items in the <see cref="FixedSizeDictionary{TKey, TValue}"/>. Default is 50.
        /// </summary>
        public int Capacity { get; }

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
