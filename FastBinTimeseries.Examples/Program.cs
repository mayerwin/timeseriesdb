﻿#region COPYRIGHT

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

namespace NYurik.FastBinTimeseries.Examples
{
    internal static class Program
    {
        private static readonly Dictionary<string, Action> Examples =
            new Dictionary<string, Action>
                {
                    {"binseriesfile", () => DemoBinSeriesFile.Run()},
                    {"bincompressedseriesfile", () => DemoBinCompressedSeriesFile.Run()},
                };

        /// <summary>
        /// Runs all examples when no parameters is given,
        /// or the specific one provided by the first parameter
        /// </summary>
        private static void Main(string[] args)
        {
            try
            {
                string example = null;
                if (args.Length > 0)
                    example = args[0].ToLowerInvariant();

                Action run;
                if (example != null && Examples.TryGetValue(example, out run))
                {
                    run();
                }
                else
                {
                    Console.WriteLine("Running all examples\n");
                    foreach (Action r in Examples.Values)
                        r();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}