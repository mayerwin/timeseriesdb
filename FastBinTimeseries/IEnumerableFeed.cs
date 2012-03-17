#region COPYRIGHT

/*
 *     Copyright 2009-2012 Yuri Astrakhan  (<Firstname><Lastname>@gmail.com)
 *
 *     This file is part of FastBinTimeseries library
 * 
 *  FastBinTimeseries is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 * 
 *  FastBinTimeseries is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 * 
 *  You should have received a copy of the GNU General Public License
 *  along with FastBinTimeseries.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

#endregion

using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NYurik.FastBinTimeseries
{
    /// <summary>
    /// Implementors can read values as a stream.
    /// Any type implementing this interface must also implement <see cref="IEnumerableFeed{TInd,TVal}"/>.
    /// </summary>
    public interface IEnumerableFeed : IGenericInvoker2, IDisposable
    {
        /// <summary> Type of the items stored in this file </summary>
        Type ItemType { get; }

        /// <summary> User string stored in the header </summary>
        string Tag { get; }
    }

    /// <summary>
    /// Implementors can read <typeparamref name="TVal"/> values as a stream.
    /// It is assumed that value's index is somehow stored inside the value.
    /// </summary>
    /// <typeparam name="TInd">Type of the index. Must be comparable.</typeparam>
    /// <typeparam name="TVal">Type of the value stored</typeparam>
    public interface IEnumerableFeed<TInd, TVal> : IEnumerableFeed
        where TInd : IComparable<TInd>
    {
        /// <summary>
        /// Returns function that can extract TInd index from a given value T
        /// </summary>
        Func<TVal, TInd> IndexAccessor { get; }

        /// <summary>
        /// Read data from the underlying storage one block at a time.
        /// </summary>
        /// <param name="fromInd">The index of the first element to read.
        /// If default(<typeparamref name="TInd"/>), will read from the first item going forward, or last when going in reverse.
        /// Inclusive if going forward, exclusive when going backwards.</param>
        /// <param name="inReverse">Set to true if you want to enumerate backwards, from last to first</param>
        /// <param name="bufferProvider">Provides buffers (or re-yields the same buffer) for each new result. Could be null for automatic</param>
        /// <param name="maxItemCount">Maximum number of items to return</param>
        IEnumerable<ArraySegment<TVal>> StreamSegments(TInd fromInd = default(TInd), bool inReverse = false,
                                                       IEnumerable<Buffer<TVal>> bufferProvider = null,
                                                       long maxItemCount = long.MaxValue);
    }

    /// <summary>
    /// Implementors can read and store values.
    /// Any type implementing this interface must also implement <see cref="IWritableFeed{TInd,TVal}"/>.
    /// </summary>
    public interface IWritableFeed : IEnumerableFeed
    {
        /// <summary>
        /// Returns true if this file is empty
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// False if more than one identical index is allowed in the feed, True otherwise
        /// </summary>
        bool UniqueIndexes { get; }
    }

    /// <summary>
    /// Implementors can read and store values of type <typeparamref name="TVal"/>.
    /// </summary>
    /// <typeparam name="TInd">Type of the index. Must be comparable.</typeparam>
    /// <typeparam name="TVal">Type of the value stored</typeparam>
    public interface IWritableFeed<TInd, TVal> : IWritableFeed, IEnumerableFeed<TInd, TVal>
        where TInd : IComparable<TInd>
    {
        /// <summary>
        /// If available, returns the first index of the feed, or default(TInd) if empty
        /// </summary>
        TInd FirstIndex { get; }

        /// <summary>
        /// If available, returns the last index of the feed, or default(TInd) if empty
        /// </summary>
        TInd LastIndex { get; }

        /// <summary>
        /// Add new items at the end of the existing file.
        /// Special case: If file allows non-unique indexes, and the new data starts with the same index as the last in file,
        /// duplicate indexes will be preserved if <paramref name="allowFileTruncation"/> is false,
        /// whereas when true, the file's last item(s) with that index will be deleted.
        /// </summary>
        /// <param name="bufferStream">Stream of new data to be added.</param>
        /// <param name="allowFileTruncation">If true, the file will be truncated up to, but not including the first new item's index.
        /// If false, no data will be removed from the file.</param>
        void AppendData([NotNull] IEnumerable<ArraySegment<TVal>> bufferStream, bool allowFileTruncation = false);
    }
}