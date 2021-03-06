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
using JetBrains.Annotations;

namespace NYurik.TimeSeriesDb.Serializers.BlockSerializer
{
    [Obsolete("Use ScaledDeltaFloatField or ScaledDeltaIntField instead")]
    public class ScaledDeltaField : BaseField
    {
        private long _divider = 1;
        private ConstantExpression _dividerExp;
        private bool _isInteger;
        private long _multiplier;

        [UsedImplicitly]
        protected ScaledDeltaField()
        {
        }

        /// <summary>
        /// Integer and Float delta serializer.
        /// </summary>
        /// <param name="stateStore">Serializer with the state</param>
        /// <param name="fieldType">Type of value to store</param>
        /// <param name="stateName">Name of the value (for debugging)</param>
        public ScaledDeltaField([NotNull] IStateStore stateStore, [NotNull] Type fieldType, string stateName)
            : base(Versions.Ver0, stateStore, fieldType, stateName)
        {
            // Floating point numbers must manually initialize Multiplier
            _multiplier =
                fieldType.IsPrimitive && (fieldType == typeof (float) || fieldType == typeof (double))
                    ? 0
                    : 1;
        }

        /// <summary> Value is multiplied by this parameter before storage</summary>
        public long Multiplier
        {
            get { return _multiplier; }
            set
            {
                ThrowOnInitialized();
                _multiplier = value;
            }
        }

        /// <summary> Value is divided by this parameter before storage </summary>
        public long Divider
        {
            get { return _divider; }
            set
            {
                ThrowOnInitialized();
                _divider = value;
            }
        }

        public override int MaxByteSize
        {
            get
            {
                // TODO: optimize to make this number smaller depending on the field type and scaling parameters
                return CodecBase.MaxBytesFor64;
            }
        }

        protected override void InitNewField(BinaryWriter writer)
        {
            base.InitNewField(writer);
            writer.Write(Divider);
            writer.Write(Multiplier);
        }

        protected override void InitExistingField(BinaryReader reader, Func<string, Type> typeResolver)
        {
            base.InitExistingField(reader, typeResolver);
            Divider = reader.ReadInt64();
            Multiplier = reader.ReadInt64();
        }

        protected override bool IsValidVersion(Version ver)
        {
            return ver == Versions.Ver0;
        }

        protected override void MakeReadonly()
        {
            if (_multiplier < 1)
                throw new SerializerException(
                    "Multiplier = {0} for value {1} ({2}), but must be >= 1", _multiplier, StateName, FieldType.FullName);
            if (_divider < 1)
                throw new SerializerException(
                    "Divider = {0} for value {1} ({2}), but must be >= 1", _divider, StateName, FieldType.FullName);

            ulong maxDivider = 0;
            _isInteger = true;
            switch (FieldType.GetTypeCode())
            {
                case TypeCode.Char:
                    maxDivider = char.MaxValue;
                    _dividerExp = Expression.Constant((char) _divider);
                    break;
                case TypeCode.Int16:
                    maxDivider = (ulong) Int16.MaxValue;
                    _dividerExp = Expression.Constant((short) _divider);
                    break;
                case TypeCode.UInt16:
                    maxDivider = UInt16.MaxValue;
                    _dividerExp = Expression.Constant((ushort) _divider);
                    break;
                case TypeCode.Int32:
                    maxDivider = Int32.MaxValue;
                    _dividerExp = Expression.Constant((int) _divider);
                    break;
                case TypeCode.UInt32:
                    maxDivider = UInt32.MaxValue;
                    _dividerExp = Expression.Constant((uint) _divider);
                    break;
                case TypeCode.Int64:
                    maxDivider = Int64.MaxValue;
                    // ReSharper disable RedundantCast
                    _dividerExp = Expression.Constant((long) _divider);
                    // ReSharper restore RedundantCast
                    break;
                case TypeCode.UInt64:
                    maxDivider = UInt64.MaxValue;
                    _dividerExp = Expression.Constant((ulong) _divider);
                    break;
                case TypeCode.Single:
                case TypeCode.Double:
                    _isInteger = false;
                    break;
                default:
                    throw new SerializerException(
                        "Value {0} has an unsupported type {1}", StateName, FieldType.ToDebugStr());
            }

            if (_isInteger)
            {
                if (_multiplier != 1)
                    throw new SerializerException(
                        "Integer types must have multiplier == 1, but {0} was given instead for value {1} ({2})",
                        _multiplier, StateName, FieldType.FullName);
                if ((ulong) _divider > maxDivider)
                    throw new SerializerException(
                        "Divider = {0} for value {1} ({2}), but must be < {3}", _divider, StateName, FieldType.FullName,
                        maxDivider);
            }

            base.MakeReadonly();
        }

        protected override Tuple<Expression, Expression> GetSerializerExp(Expression valueExp, Expression codec)
        {
            //
            // long stateVar;
            //
            bool needToInit;
            ParameterExpression stateVarExp = StateStore.GetOrCreateStateVar(StateName, typeof (long), out needToInit);

            //
            // valueGetter():
            //    if non-integer: (#long) Math.Round(value * Multiplier / Divider, 0)
            //    if integer:     (#long) (value / divider)
            //

            Expression getValExp = valueExp;

            if (_multiplier != 1 || _divider != 1)
            {
                if (!_isInteger)
                {
                    switch (FieldType.GetTypeCode())
                    {
                        case TypeCode.Single:
                            {
                                // floats support 7 significant digits
                                float dvdr = (float) _multiplier/_divider;
                                float maxValue = (float) Math.Pow(10, 7)/dvdr;

                                getValExp = FloatingGetValExp(codec, getValExp, dvdr, -maxValue, maxValue);
                            }
                            break;

                        case TypeCode.Double:
                            {
                                // doubles support at least 15 significant digits
                                double dvdr = (double) _multiplier/_divider;
                                double maxValue = Math.Pow(10, 15)/dvdr;

                                getValExp = FloatingGetValExp(codec, getValExp, dvdr, -maxValue, maxValue);
                            }
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    if (_multiplier != 1) throw new InvalidOperationException();
                    getValExp = Expression.Divide(getValExp, _dividerExp);
                }
            }

            // for integer types, do not check, ulong would not fit otherwise
            if (getValExp.Type != typeof (long))
                getValExp =
                    _isInteger
                        ? Expression.Convert(getValExp, typeof (long))
                        : Expression.ConvertChecked(getValExp, typeof (long));


            ParameterExpression varState2Exp = Expression.Variable(typeof (long), "state2");
            ParameterExpression varDeltaExp = Expression.Variable(typeof (long), "delta");

            //
            // stateVar2 = valueGetter();
            // delta = stateVar2 - stateVar
            // stateVar = stateVar2;
            // return codec.WriteSignedValue(delta);
            //
            Expression deltaExp =
                Expression.Block(
                    typeof (bool),
                    new[] {varState2Exp, varDeltaExp},
                    Expression.Assign(varState2Exp, getValExp),
                    Expression.Assign(varDeltaExp, Expression.Subtract(varState2Exp, stateVarExp)),
                    Expression.Assign(stateVarExp, varState2Exp),
                    DebugValueExp(codec, stateVarExp, "MultFld WriteDelta"),
                    WriteSignedValue(codec, varDeltaExp)
                    );

            //
            // stateVar = valueGetter();
            // codec.WriteSignedValue(stateVar);
            //
            Expression initExp =
                needToInit
                    ? Expression.Block(
                        Expression.Assign(stateVarExp, getValExp),
                        DebugValueExp(codec, stateVarExp, "MultFld WriteInit"),
                        WriteSignedValue(codec, stateVarExp))
                    : deltaExp;

            return new Tuple<Expression, Expression>(initExp, deltaExp);
        }

        private Expression FloatingGetValExp<T>(Expression codec, Expression value, T divider, T minValue, T maxValue)
        {
            Expression multExp = Expression.Multiply(value, Expression.Constant(divider));
            if (value.Type == typeof (float))
                multExp = Expression.Convert(multExp, typeof (double));

            return
                Expression.Block(
                    // if (value < -maxValue || maxValue < value)
                    //     ThrowOverflow(value);
                    Expression.IfThen(
                        Expression.Or(
                            Expression.LessThan(value, Expression.Constant(minValue)),
                            Expression.LessThan(Expression.Constant(maxValue), value)),
                        ThrowOverflow(codec, value)),
                    // Math.Round(value*_multiplier/_divider)
                    Expression.Call(typeof (Math), "Round", null, multExp));
        }

        protected override Tuple<Expression, Expression> GetDeSerializerExp(Expression codec)
        {
            //
            // long stateVar;
            //
            bool needToInit;
            ParameterExpression stateVarExp = StateStore.GetOrCreateStateVar(StateName, typeof (long), out needToInit);

            //
            // valueGetter():
            //    if non-integer: (decimal)value * ((decimal)Divider / Multiplier)
            //    if integer:     (type)value * divider
            //

            Expression getValExp = stateVarExp;

            if (_multiplier != 1 || _divider != 1)
            {
                if (!_isInteger)
                {
                    switch (FieldType.GetTypeCode())
                    {
                        case TypeCode.Single:
                            getValExp = Expression.Divide(
                                Expression.Convert(getValExp, typeof (float)),
                                Expression.Constant((float) _multiplier/_divider));
                            break;

                        case TypeCode.Double:
                            getValExp = Expression.Divide(
                                Expression.Convert(getValExp, typeof (double)),
                                Expression.Constant((double) _multiplier/_divider));
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    if (_multiplier != 1) throw new InvalidOperationException();
                    if (getValExp.Type != FieldType)
                        getValExp = Expression.Convert(getValExp, FieldType);
                    getValExp = Expression.Multiply(getValExp, _dividerExp);
                }
            }
            else if (getValExp.Type != FieldType)
                getValExp = Expression.Convert(getValExp, FieldType);


            MethodCallExpression readValExp = ReadSignedValue(codec);

            //
            // stateVar += codec.ReadSignedValue();
            // return stateVar;
            //
            Expression deltaExp =
                Expression.Block(
                    Expression.AddAssign(stateVarExp, readValExp),
                    DebugValueExp(codec, stateVarExp, "MultFld ReadDelta"),
                    getValExp);

            //
            // stateVar = codec.ReadSignedValue();
            // return stateVar;
            //
            Expression initExp =
                needToInit
                    ? Expression.Block(
                        Expression.Assign(stateVarExp, readValExp),
                        DebugValueExp(codec, stateVarExp, "MultFld ReadInit"),
                        getValExp)
                    : deltaExp;

            return new Tuple<Expression, Expression>(initExp, deltaExp);
        }

        protected override bool Equals(BaseField baseOther)
        {
            var other = (ScaledDeltaField) baseOther;
            return _divider == other._divider && _multiplier == other._multiplier;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // ReSharper disable NonReadonlyFieldInGetHashCode
                var hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ _divider.GetHashCode();
                hashCode = (hashCode*397) ^ _multiplier.GetHashCode();
                return hashCode;
                // ReSharper restore NonReadonlyFieldInGetHashCode
            }
        }
    }
}