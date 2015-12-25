﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Dasher
{
    public sealed class Serialiser<T>
    {
        private readonly Serialiser _inner;

        public Serialiser()
        {
            _inner = new Serialiser(typeof(T));
        }

        public void Serialise(Stream stream, T value)
        {
            _inner.Serialise(stream, value);
        }

        public void Serialise(UnsafePacker packer, T value)
        {
            _inner.Serialise(packer, value);
        }

        public byte[] Serialise(T value)
        {
            return _inner.Serialise(value);
        }
    }

    public sealed class Serialiser
    {
        private readonly Action<UnsafePacker, object> _action;

        public Serialiser(Type type)
        {
            _action = BuildPacker(type);
        }

        public void Serialise(Stream stream, object value)
        {
            using (var packer = new UnsafePacker(stream))
                Serialise(packer, value);
        }

        public void Serialise(UnsafePacker packer, object value)
        {
            _action(packer, value);
        }

        public byte[] Serialise(object value)
        {
            var stream = new MemoryStream();
            using (var packer = new UnsafePacker(stream))
                _action(packer, value);
            return stream.ToArray();
        }

        private static Action<UnsafePacker, object> BuildPacker(Type type)
        {
            if (type.IsPrimitive)
                throw new Exception("TEST THIS CASE 1");

            var method = new DynamicMethod(
                $"Serialiser{type.Name}",
                null,
                new[] { typeof(UnsafePacker), typeof(object) });

            var ilg = method.GetILGenerator();

            // store packer in a local so we can pass it easily
            var packer = ilg.DeclareLocal(typeof(UnsafePacker));
            ilg.Emit(OpCodes.Ldarg_0); // packer
            ilg.Emit(OpCodes.Stloc, packer);

            // cast value to a local of required type
            var value = ilg.DeclareLocal(type);
            ilg.Emit(OpCodes.Ldarg_1); // value
            ilg.Emit(type.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, type);
            ilg.Emit(OpCodes.Stloc, value);

            WriteObject(ilg, packer, value);

            ilg.Emit(OpCodes.Ret);

            // Return a delegate that performs the above operations
            return (Action<UnsafePacker, object>)method.CreateDelegate(typeof(Action<UnsafePacker, object>));
        }

        private static void WriteObject(ILGenerator ilg, LocalBuilder packer, LocalBuilder value)
        {
            var type = value.LocalType;

            var packerMethod = typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.Pack), new[] { type });
            if (packerMethod != null)
            {
                ilg.Emit(OpCodes.Ldloc, packer);
                ilg.Emit(OpCodes.Ldloc, value);
                ilg.Emit(OpCodes.Call, packerMethod);
                return;
            }

            if (type == typeof(decimal))
            {
                // write the string form of the value
                ilg.Emit(OpCodes.Ldloc, packer);
                ilg.Emit(OpCodes.Ldloca, value);
                ilg.Emit(OpCodes.Call, typeof(decimal).GetMethod(nameof(decimal.ToString), new Type[0]));
                ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.Pack), new[] { typeof(string) }));
                return;
            }

            if (type.IsEnum)
            {
                // write the string form of the value
                ilg.Emit(OpCodes.Ldloc, packer);
                ilg.Emit(OpCodes.Ldloca, value);
                ilg.Emit(OpCodes.Constrained, type);
                ilg.Emit(OpCodes.Callvirt, typeof(object).GetMethod(nameof(ToString), new Type[0]));
                ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.Pack), new[] { typeof(string) }));
                return;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                var elementType = type.GetGenericArguments().Single();

                // read list length
                var count = ilg.DeclareLocal(typeof(int));
                ilg.Emit(OpCodes.Ldloc, value);
                ilg.Emit(OpCodes.Callvirt, typeof(IReadOnlyCollection<>).MakeGenericType(elementType).GetProperty(nameof(IReadOnlyList<int>.Count)).GetMethod);
                ilg.Emit(OpCodes.Stloc, count);

                // write array header
                ilg.Emit(OpCodes.Ldloc, packer);
                ilg.Emit(OpCodes.Ldloc, count);
                ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.PackArrayHeader)));

                // begin loop
                var loopStart = ilg.DefineLabel();
                var loopTest = ilg.DefineLabel();
                var loopEnd = ilg.DefineLabel();

                var i = ilg.DeclareLocal(typeof(int));
                ilg.Emit(OpCodes.Ldc_I4_0);
                ilg.Emit(OpCodes.Stloc, i);

                ilg.Emit(OpCodes.Br, loopTest);
                ilg.MarkLabel(loopStart);

                // loop body
                ilg.Emit(OpCodes.Ldloc, value);
                ilg.Emit(OpCodes.Ldloc, i);
                ilg.Emit(OpCodes.Callvirt, type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Single(p => p.Name == "Item" && p.GetIndexParameters().Length == 1).GetMethod);
                var elementValue = ilg.DeclareLocal(elementType);
                ilg.Emit(OpCodes.Stloc, elementValue);
                WriteObject(ilg, packer, elementValue);

                // loop counter increment
                ilg.Emit(OpCodes.Ldloc, i);
                ilg.Emit(OpCodes.Ldc_I4_1);
                ilg.Emit(OpCodes.Add);
                ilg.Emit(OpCodes.Stloc, i);

                // loop test
                ilg.MarkLabel(loopTest);
                ilg.Emit(OpCodes.Ldloc, i);
                ilg.Emit(OpCodes.Ldloc, count);
                ilg.Emit(OpCodes.Clt);
                ilg.Emit(OpCodes.Brtrue, loopStart);

                // after loop
                ilg.MarkLabel(loopEnd);
                return;
            }

            // treat as complex object and recur
            var props = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead)
                .ToList();

            // write map header
            ilg.Emit(OpCodes.Ldloc, packer);
            ilg.Emit(OpCodes.Ldc_I4, props.Count);
            ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.PackMapHeader)));

            // write each property's value
            foreach (var prop in props)
            {
                var propValue = ilg.DeclareLocal(prop.PropertyType);

                // write property name
                ilg.Emit(OpCodes.Ldloc, packer);
                ilg.Emit(OpCodes.Ldstr, prop.Name);
                ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.Pack), new[] {typeof(string)}));

                // get property value
                ilg.Emit(type.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc, value);
                ilg.Emit(OpCodes.Call, prop.GetMethod);
                ilg.Emit(OpCodes.Stloc, propValue);

                // write property value
                WriteObject(ilg, packer, propValue);
            }
        }
    }
}