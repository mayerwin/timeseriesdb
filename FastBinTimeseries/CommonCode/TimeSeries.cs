using System;
using NYurik.FastBinTimeseries.CommonCode;

namespace NYurik.FastBinTimeseries
{
    public interface ISeries
    {
        int Count { get; }
        Type GetElementType();
        object GetValueSlow(int index);
    }

    public interface ISeries<T> : ISeries
    {
        T this[int index] { get; }
        T[] Values { get; }
    }

    public interface ITimeSeries : ISeries
    {
        TimeSpan ItemSpan { get; }
        int BinarySearch(UtcDateTime timestamp);
        UtcDateTime GetTimestamp(int index);
    }

    public interface ITimeSeries<T> : ITimeSeries, ISeries<T>
    {
    }

    public interface INonUniformTimeSeries : ISeries
    {
        UtcDateTime[] Timestamps { get; }
    }

    [Serializable]
    public class TimeSeries<T> : ITimeSeries<T>, INonUniformTimeSeries
    {
        private readonly UtcDateTime[] _timestamps;
        private readonly T[] _values;

        public TimeSeries(int size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException("size", size, "must be non-negative");
            _timestamps = new UtcDateTime[size];
            _values = new T[size];
        }

        public TimeSeries(UtcDateTime[] timestamps, T[] values)
        {
            if (timestamps == null) throw new ArgumentNullException("timestamps");
            if (values == null) throw new ArgumentNullException("values");
            if (timestamps.Length != values.Length) throw new ArgumentException("Array sizes must be identical");
            _timestamps = timestamps;
            _values = values;
        }

        public UtcDateTime[] Timestamps
        {
            get { return _timestamps; }
        }

        #region ITimeSeries<T> Members

        public T this[int index]
        {
            get { return _values[index]; }
        }

        public T[] Values
        {
            get { return _values; }
        }

        public Type GetElementType()
        {
            return typeof (T);
        }

        object ISeries.GetValueSlow(int index)
        {
            return _values[index];
        }

        public int Count
        {
            get { return _values.Length; }
        }

        public TimeSpan ItemSpan
        {
            get { return TimeSpan.Zero; }
        }

        public int BinarySearch(UtcDateTime timestamp)
        {
            return Array.BinarySearch(_timestamps, timestamp);
        }

        public UtcDateTime GetTimestamp(int index)
        {
            return _timestamps[index];
        }

        #endregion

        public override string ToString()
        {
            string res = string.Format("{0} {1} values", Count, typeof (T).Name);
            return _timestamps.Length != 0
                       ? string.Format("{0} {1:o}-{2:o}", res, _timestamps[0], _timestamps[_timestamps.Length - 1])
                       : res;
        }
    }

    public class UniformTimeSeries<T> : ITimeSeries<T>
    {
        private readonly UtcDateTime _firstTimestamp;
        private readonly TimeSpan _itemSpan;
        private readonly T[] _values;

        public UniformTimeSeries(UtcDateTime firstTimestamp, TimeSpan itemSpan, T[] values)
        {
            if (values == null) throw new ArgumentNullException("values");
            if (itemSpan <= TimeSpan.Zero) throw new ArgumentOutOfRangeException("itemSpan", itemSpan, "<= 0");

            _firstTimestamp = firstTimestamp;
            _itemSpan = itemSpan;
            _values = values;
        }

        public UtcDateTime FirstTimestamp
        {
            get { return _firstTimestamp; }
        }

        #region ITimeSeries<T> Members

        public int Count
        {
            get { return _values.Length; }
        }

        public Type GetElementType()
        {
            return typeof (T);
        }

        object ISeries.GetValueSlow(int index)
        {
            return _values[index];
        }

        public TimeSpan ItemSpan
        {
            get { return _itemSpan; }
        }

        public int BinarySearch(UtcDateTime timestamp)
        {
            if (timestamp < FirstTimestamp)
                return ~0;

            if (timestamp > FirstTimestamp + ItemSpan.Multiply(Count - 1))
                return ~Count;

            TimeSpan t = (timestamp - FirstTimestamp);
            var div = (int) (t.Ticks/ItemSpan.Ticks);
            if (t.Ticks%ItemSpan.Ticks != 0)
                return ~(div + 1);
            return div;
        }

        public UtcDateTime GetTimestamp(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException("index", index, "<0");
            if (index >= Count) throw new ArgumentOutOfRangeException("index", index, ">=Count");
            return FirstTimestamp + ItemSpan.Multiply(index);
        }

        public T this[int index]
        {
            get { return _values[index]; }
        }

        public T[] Values
        {
            get { return _values; }
        }

        #endregion

        public override string ToString()
        {
            return string.Format("{0} {1} values starting at {2} every {3}", Count, typeof (T).Name, FirstTimestamp,
                                 ItemSpan);
        }
    }
}