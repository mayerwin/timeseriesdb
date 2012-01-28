#region COPYRIGHT

/*
 *     Copyright 2009-2011 Yuri Astrakhan  (<Firstname><Lastname>@gmail.com)
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

namespace NYurik.FastBinTimeseries
{
    //[Obsolete("Use IEnumerableFeed<TInd, TVal> instead")]

    public interface IStoredSeries : IDisposable
    {
        /// <summary> Type of the items stored in this file </summary>
        Type ItemType { get; }

        /// <summary> User string stored in the header </summary>
        string Tag { get; }

        /// <summary> True when the file has no data </summary>
        bool IsEmpty { get; }

        /// <summary> True when the object has been disposed. No further operations are allowed. </summary>
        bool IsDisposed { get; }

        /// <summary> Total number of items in the file </summary>
        long GetItemCount();

        /// <summary>
        /// Read up to <paramref name="count"/> items beging at <paramref name="firstItemIdx"/>, and return an <see cref="Array"/> object. 
        /// </summary>
        /// <param name="firstItemIdx">Index of the item to start from.</param>
        /// <param name="count">The maximum number of items to read.</param>
        Array GenericReadData(long firstItemIdx, int count);
    }
}