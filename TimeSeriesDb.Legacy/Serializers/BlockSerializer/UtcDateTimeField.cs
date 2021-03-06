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
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using NYurik.TimeSeriesDb.CommonCode;

namespace NYurik.TimeSeriesDb.Serializers.BlockSerializer
{
    [Obsolete]
    public class UtcDateTimeField : BaseField
    {
        private ScaledDeltaField _deltaField;

        [UsedImplicitly]
        protected UtcDateTimeField()
        {
        }

        /// <summary>
        /// Integer and Float delta serializer.
        /// </summary>
// ReSharper disable UnusedParameter.Local
        public UtcDateTimeField([NotNull] IStateStore serializer, Type valueType, string stateName)
// ReSharper restore UnusedParameter.Local
            : base(Versions.Ver0, serializer, typeof (UtcDateTime), stateName)
        {
            _deltaField = new ScaledDeltaField(serializer, typeof (long), stateName);
        }

        /// <summary>Value is divided by this parameter before storage</summary>
        public TimeSpan TimeDivider
        {
            get { return TimeSpan.FromTicks(_deltaField.Divider); }
            set { _deltaField.Divider = ValidateDivider(value); }
        }

        private static long ValidateDivider(TimeSpan value)
        {
            if (value < TimeSpan.Zero)
                throw new SerializerException("Divider ({0}) must be positive", value);
            if (value > TimeSpan.FromDays(1))
                throw new SerializerException("Divider {0} is > 1 day", value);
            if (value > TimeSpan.Zero && TimeSpan.TicksPerDay%value.Ticks != 0)
                throw new SerializerException(
                    "TimeSpan.TicksPerDay must be divisible by time slice " + value);
            return value == TimeSpan.Zero ? 1 : value.Ticks;
        }

        public override int MaxByteSize
        {
            get { return _deltaField.MaxByteSize; }
        }

        protected override void InitNewField(BinaryWriter writer)
        {
            base.InitNewField(writer);
            _deltaField.InitNew(writer);
        }

        protected override void InitExistingField(BinaryReader reader, Func<string, Type> typeResolver)
        {
            base.InitExistingField(reader, typeResolver);

            BaseField fld = FieldFromReader(StateStore, reader, typeResolver);
            _deltaField = fld as ScaledDeltaField;
            if (_deltaField == null)
                throw new SerializerException(
                    "Unexpected field {0} was deserialized instead of {1}", fld,
                    typeof (ScaledDeltaField).ToDebugStr());

            ValidateDivider(TimeDivider);
        }

        protected override bool IsValidVersion(Version ver)
        {
            return ver == Versions.Ver0;
        }

        protected override Tuple<Expression, Expression> GetSerializerExp(Expression valueExp, Expression codec)
        {
            return _deltaField.GetSerializer(Expression.Property(valueExp, "Ticks"), codec);
        }

        protected override Tuple<Expression, Expression> GetDeSerializerExp(Expression codec)
        {
            Tuple<Expression, Expression> res = _deltaField.GetDeSerializer(codec);

            ConstructorInfo ctor = typeof (UtcDateTime).GetConstructor(new[] {typeof (long)});
            if (ctor == null)
                throw new SerializerException("UtcDateTime(long) constructor was not found");

            return new Tuple<Expression, Expression>(
                Expression.New(ctor, res.Item1),
                Expression.New(ctor, res.Item2));
        }

        protected override bool Equals(BaseField baseOther)
        {
            var other = (UtcDateTimeField)baseOther;
            return _deltaField.Equals(other._deltaField);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // ReSharper disable NonReadonlyFieldInGetHashCode
                var hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ _deltaField.GetHashCode();
                return hashCode;
                // ReSharper restore NonReadonlyFieldInGetHashCode
            }
        }
    }
}