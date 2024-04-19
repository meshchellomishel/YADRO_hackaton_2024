//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Utilities.Packets
{
    public class Packet
    {
        public static int CalculateLength<T>()
        {
            lock(cache)
            {
                return cache.Get(typeof(T), _ =>
                {
                    var fieldsAndProperties = GetFieldsAndProperties<T>();

                    var maxOffset = 0;
                    var offset = 0;
                    foreach(var element in fieldsAndProperties)
                    {
                        var bytesRequired = element.BytesRequired;
                        offset = element.ByteOffset ?? offset;

                        var co = offset + bytesRequired;
                        maxOffset = Math.Max(co, maxOffset);
                        offset += bytesRequired;
                    }

                    return maxOffset;
                });
            }
        }

        public static int CalculateOffset<T>(string fieldName)
        {
            lock(cache)
            {
                return cache.Get(Tuple.Create(typeof(T), fieldName), _ =>
                {
                    var fieldsAndProperties = GetFieldsAndProperties<T>();

                    var maxOffset = 0;
                    var offset = 0;
                    foreach(var element in fieldsAndProperties)
                    {
                        if(element.ElementName == fieldName)
                        {
                            return maxOffset;
                        }

                        if(element.ByteOffset.HasValue)
                        {
                            offset = element.ByteOffset.Value;
                        }

                        var bytesRequired = element.BytesRequired;
                        var co = offset + bytesRequired;
                        maxOffset = Math.Max(co, maxOffset);
                        offset += bytesRequired;
                    }

                    return -1;
                });
            }
        }

        public static T Decode<T>(IList<byte> data, int dataOffset = 0)
        {
            if(!TryDecode<T>(data, out var result, dataOffset))
            {
                throw new ArgumentException($"Could not decode the packet of type {typeof(T)} due to insufficient data. Required {Packet.CalculateLength<T>()} bytes, but received {(data.Count - dataOffset)}");
            }
            return result;
        }

        public static bool TryDecode<T>(IList<byte> data, out T result, int dataOffset = 0)
        {
            var offset = dataOffset;
            if(offset < 0)
            {
                throw new ArgumentException("Data offset cannot be less than zero", "dataOffset");
            }

            // we need to do the casting as otherwise setting value would not work on structs
            var innerResult = (object)default(T);
            result = default(T);

            var fieldsAndProperties = GetFieldsAndProperties<T>();

            foreach(var field in fieldsAndProperties)
            {
                var type = field.ElementType;
                if(type.IsEnum)
                {
                    type = type.GetEnumUnderlyingType();
                }

                if(field.ByteOffset.HasValue)
                {
                    offset = dataOffset + field.ByteOffset.Value;
                }
                var bitOffset = field.BitOffset ?? 0;

                if(type == typeof(byte[]))
                {
                    if(bitOffset != 0)
                    {
                        throw new ArgumentException("Bit offset for byte array is not supported.");
                    }

                    var width = field.Width;
                    if(offset + width > data.Count)
                    {
                        return false;
                    }
                    if(width == 0)
                    {
                        throw new ArgumentException("Positive width must be provided to decode byte array");
                    }

                    var v = new byte[width];
                    for(var i = 0; i < width; i++)
                    {
                        v[i] = data[offset + i];
                    }

                    field.SetValue(innerResult, v);
                    offset += width;
                    continue;
                }

                var bytesRequired = field.BytesRequired;
                if(offset + bytesRequired > data.Count)
                {
                    return false;
                }

                var intermediate = 0UL;
                {
                    var i = 0;
                    foreach(var b in data.Skip(offset))
                    {
                        if(i >= bytesRequired)
                        {
                            break;
                        }
                        // assume host LSB bit ordering
                        var shift = (i << 3) - bitOffset;
                        intermediate |= shift > 0 ? (ulong)b << shift : (ulong)b >> -shift;
                        i += 1;
                    }
                }

                if(type == typeof(int) || type == typeof(uint))
                {
                    var v = (uint)intermediate;

                    if(!field.IsLSBFirst)
                    {
                        v = BitHelper.ReverseBytes(v);
                    }
                    v = BitHelper.GetValue(v, 0, field.BitWidth ?? 32);
                    if(type == typeof(int))
                    {
                        field.SetValue(innerResult, (int)v);
                    }
                    else
                    {
                        field.SetValue(innerResult, v);
                    }
                }
                else if(type == typeof(short) || type == typeof(ushort))
                {
                    var v = (ushort)intermediate;

                    if(!field.IsLSBFirst)
                    {
                        v = BitHelper.ReverseBytes(v);
                    }
                    v = (ushort)BitHelper.GetValue(v, 0, field.BitWidth ?? 16);
                    if(type == typeof(short))
                    {
                        field.SetValue(innerResult, (short)v);
                    }
                    else
                    {
                        field.SetValue(innerResult, v);
                    }
                }
                else if(type == typeof(byte) || type == typeof(sbyte))
                {
                    var v = (byte)intermediate;
                    v = BitHelper.GetValue(v, 0, field.BitWidth ?? 8);
                    if(type == typeof(sbyte))
                    {
                        field.SetValue(innerResult, (sbyte)v);
                    }
                    else
                    {
                        field.SetValue(innerResult, v);
                    }
                }
                else if(type == typeof(long) || type == typeof(ulong))
                {
                    var v = intermediate;
                    if(!field.IsLSBFirst)
                    {
                        v = BitHelper.ReverseBytes(v);
                    }
                    v = BitHelper.GetValue(v, 0, field.BitWidth ?? 64);
                    if(type == typeof(long))
                    {
                        field.SetValue(innerResult, (long)v);
                    }
                    else
                    {
                        field.SetValue(innerResult, v);
                    }
                }
                else if(type == typeof(bool))
                {
                    field.SetValue(innerResult, BitHelper.IsBitSet(intermediate, 0));
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {type.Name}");
                }
                offset += bytesRequired;
            }

            result = (T)innerResult;
            return true;
        }

        public static dynamic DecodeDynamic<T>(byte[] data, int dataOffset = 0) where T : class
        {
            if(!typeof(T).IsInterface)
            {
                throw new ArgumentException("This can be called on interfaces only");
            }

            var result = new DynamicPropertiesObject();

            var fieldsAndProperties = GetFieldsAndProperties<T>();

            var offset = dataOffset;
            foreach(var field in fieldsAndProperties)
            {
                if(field.ByteOffset.HasValue)
                {
                    offset = dataOffset + field.ByteOffset.Value;
                }

                if(field.ElementType == typeof(uint))
                {
                    var lOffset = offset;
                    result.ProvideProperty(field.ElementName, getter: () => (uint)((data[lOffset] << 24) | (data[lOffset + 1] << 16) | (data[lOffset + 2] << 8) | (data[lOffset + 3])));

                    offset += 4;
                }
                else if(field.ElementType == typeof(ushort))
                {
                    var lOffset = offset;
                    result.ProvideProperty(field.ElementName, getter: () => (ushort)((data[lOffset] << 16) | (data[lOffset + 1])));

                    offset += 2;
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {typeof(T).Name}");
                }
            }

            return (dynamic)result;
        }

        public static byte[] Encode<T>(T packet)
        {
            var size = CalculateLength<T>();
            var result = new byte[size];
            if(size == 0)
            {
                return result;
            }
            var fieldsAndProperties = GetFieldsAndProperties<T>();

            var offset = 0;
            foreach(var field in fieldsAndProperties)
            {
                var type = field.ElementType;
                if(type.IsEnum)
                {
                    type = type.GetEnumUnderlyingType();
                }

                if(field.ByteOffset.HasValue)
                {
                    offset = field.ByteOffset.Value;
                }
                var bitOffset = field.BitOffset ?? 0;

                if(type == typeof(byte[]))
                {
                    if(bitOffset != 0)
                    {
                        throw new ArgumentException("Bit offset for byte array is not supported.");
                    }

                    var width = field.Width;
                    if(width == 0)
                    {
                        throw new ArgumentException("Positive width must be provided to decode byte array");
                    }

                    var val = (byte[])field.GetValue(packet);

                    if(width != val.Length)
                    {
                        throw new ArgumentException("Declared and actual width is different: {0} vs {1}".FormatWith(width, val.Length));
                    }

                    Array.Copy(val, 0, result, offset, width);
                    offset += width;
                    continue;
                }

                var intermediate = 0UL;
                var bitWidth = field.BitWidth ?? 0;

                if(type == typeof(int))
                {
                    var v = (int)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? (uint)v : BitHelper.ReverseBytes((uint)v);
                }
                else if(type == typeof(uint))
                {
                    var v = (uint)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? v : BitHelper.ReverseBytes(v);
                }
                else if(type == typeof(short))
                {
                    var v = (short)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? (ushort)v : BitHelper.ReverseBytes((ushort)v);
                }
                else if(type == typeof(ushort))
                {
                    var v = (ushort)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? v : BitHelper.ReverseBytes(v);
                }
                else if(type == typeof(byte))
                {
                    intermediate = (byte)field.GetValue(packet);
                }
                else if(type == typeof(sbyte))
                {
                    intermediate = (ulong)(sbyte)field.GetValue(packet);
                }
                else if(type == typeof(long))
                {
                    var v = (long)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? (ulong)v : BitHelper.ReverseBytes((ulong)v);
                }
                else if(type == typeof(ulong))
                {
                    var v = (ulong)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? v : BitHelper.ReverseBytes(v);
                }
                else if(type == typeof(bool))
                {
                    intermediate = (bool)field.GetValue(packet) ? 1UL : 0UL;
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {field.ElementType}");
                }

                // write first byte
                result[offset] = result[offset].ReplaceBits((byte)intermediate, Math.Min(8, bitWidth), bitOffset);
                bitWidth -= Math.Min(8 - bitOffset, bitWidth);
                offset += 1;
                intermediate >>= 8 - bitOffset;
                // write next full bytes
                for(; bitWidth > 8; bitWidth -= 8)
                {
                    result[offset] = (byte)intermediate;
                    intermediate >>= 8;
                    offset += 1;
                }
                // write last non-full byte if exist
                if(bitWidth > 0)
                {
                    result[offset] = result[offset].ReplaceBits((byte)intermediate, bitWidth);
                    offset += 1;
                }
            }

            return result;
        }

        private static FieldPropertyInfoWrapper[] GetFieldsAndProperties<T>()
        {
            lock(cache)
            {
                return cache.Get(typeof(T), _ =>
                {
                    return typeof(T).GetFields()
                        .Where(x => Attribute.IsDefined(x, typeof(PacketFieldAttribute)))
                        .Select(x => new FieldPropertyInfoWrapper(x))
                        .Union(typeof(T).GetProperties()
                            .Where(x => Attribute.IsDefined(x, typeof(PacketFieldAttribute)))
                            .Select(x => new FieldPropertyInfoWrapper(x))
                        ).OrderBy(x => x.Order).ToArray();
                });
            }
        }

        private static readonly SimpleCache cache = new SimpleCache();

        private class FieldPropertyInfoWrapper
        {
            public FieldPropertyInfoWrapper(FieldInfo info)
            {
                fieldInfo = info;
            }

            public FieldPropertyInfoWrapper(PropertyInfo info)
            {
                propertyInfo = info;
            }

            public object GetValue(object o)
            {
                return (fieldInfo != null)
                    ? fieldInfo.GetValue(o)
                    : propertyInfo.GetValue(o);
            }

            public bool SetValue(object o, object v)
            {
                if(fieldInfo != null)
                {
                    fieldInfo.SetValue(o, v);
                }
                else
                {
                    if(!propertyInfo.CanWrite)
                    {
                        return false;
                    }
                    propertyInfo.SetValue(o, v);
                }

                return true;
            }

            public T GetAttribute<T>()
            {
                return (T)((MemberInfo)fieldInfo ?? propertyInfo).GetCustomAttributes(typeof(T), false).FirstOrDefault();
            }

            public bool IsLSBFirst => ((MemberInfo)fieldInfo ?? propertyInfo).DeclaringType.GetCustomAttribute<LeastSignificantByteFirst>() != null
                    || GetAttribute<LeastSignificantByteFirst>() != null;

            public int? ByteOffset => (int?)GetAttribute<OffsetAttribute>()?.OffsetInBytes;

            public int? BitOffset => (int?)GetAttribute<OffsetAttribute>()?.OffsetInBits;

            public int BytesRequired => ((BitOffset ?? 0) + BitWidth + 7) / 8 ?? (Width + (BitOffset > 0 ? 1 : 0));

            public int Order => GetAttribute<PacketFieldAttribute>().Order;

            public int Width
            {
                get
                {
                    var type = ElementType;
                    if(type.IsEnum)
                    {
                        type = type.GetEnumUnderlyingType();
                    }

                    if(type == typeof(byte) || type == typeof(bool) || type == typeof(sbyte))
                    {
                        return 1;
                    }
                    if(type == typeof(ushort) || type == typeof(short))
                    {
                        return 2;
                    }
                    if(type == typeof(uint) || type == typeof(int))
                    {
                        return 4;
                    }
                    if(type == typeof(ulong) || type == typeof(long))
                    {
                        return 8;
                    }
                    if(type == typeof(byte[]))
                    {
                        return (int)(GetAttribute<WidthAttribute>()?.Value ?? 0);
                    }

                    throw new ArgumentException($"Unknown width of type: {type}");
                }
            }

            public int? BitWidth
            {
                get
                {
                    var type = ElementType;
                    if(type.IsEnum)
                    {
                        type = type.GetEnumUnderlyingType();
                    }

                    if(type == typeof(bool))
                    {
                        return 1;
                    }

                    int inBytes;
                    if(type == typeof(byte) || type == typeof(sbyte))
                    {
                        inBytes = sizeof(byte);
                    }
                    else if(type == typeof(ushort) || type == typeof(short))
                    {
                        inBytes = sizeof(ushort);
                    }
                    else if(type == typeof(uint) || type == typeof(int))
                    {
                        inBytes = sizeof(uint);
                    }
                    else if(type == typeof(ulong) || type == typeof(long))
                    {
                        inBytes = sizeof(ulong);
                    }
                    else
                    {
                        return null;
                    }

                    var width = inBytes << 3;
                    var setWidth = (int?)GetAttribute<WidthAttribute>()?.Value;
                    if(setWidth < 0)
                    {
                        throw new ArgumentException($"Width is less than zero.");
                    }
                    if(setWidth > width)
                    {
                        throw new ArgumentException($"Width is greater than the size of type: {type} ({width} bits).");
                    }
                    return setWidth ?? width;
                }
            }

            public Type ElementType => fieldInfo?.FieldType ?? propertyInfo.PropertyType;

            public string ElementName => fieldInfo?.Name ?? propertyInfo.Name;

            private readonly FieldInfo fieldInfo;
            private readonly PropertyInfo propertyInfo;
        }
    }
}
