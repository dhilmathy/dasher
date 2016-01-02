#region License
//
// Dasher
//
// Copyright 2015 Drew Noakes
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
// More information about this project is available at:
//
//    https://github.com/drewnoakes/dasher
//
#endregion

using System;
using System.Linq;
using System.Reflection.Emit;

namespace Dasher.TypeProviders
{
    internal sealed class NullableValueProvider : ITypeProvider
    {
        public bool CanProvide(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        public void Serialise(ILGenerator ilg, LocalBuilder value, LocalBuilder packer, DasherContext context)
        {
            var type = value.LocalType;
            var valueType = type.GetGenericArguments().Single();

            ilg.Emit(OpCodes.Ldloca, value);
            ilg.Emit(OpCodes.Call, type.GetProperty(nameof(Nullable<int>.HasValue)).GetMethod);

            var lblNull = ilg.DefineLabel();
            var lblExit = ilg.DefineLabel();

            ilg.Emit(OpCodes.Brfalse, lblNull);

            // has a value to serialise
            ITypeProvider provider;
            if (!context.TryGetTypeProvider(valueType, out provider))
                throw new Exception($"Cannot serialise underlying type of Nullable<{valueType}>");
            var nonNullValue = ilg.DeclareLocal(valueType);
            ilg.Emit(OpCodes.Ldloca, value);
            ilg.Emit(OpCodes.Call, type.GetProperty(nameof(Nullable<int>.Value)).GetMethod);
            ilg.Emit(OpCodes.Stloc, nonNullValue);
            provider.Serialise(ilg, nonNullValue, packer, context);

            ilg.Emit(OpCodes.Br, lblExit);

            ilg.MarkLabel(lblNull);

            // value is null
            ilg.Emit(OpCodes.Ldloc, packer);
            ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.PackNull)));

            ilg.MarkLabel(lblExit);
        }

        public void Deserialise(ILGenerator ilg, string name, Type targetType, LocalBuilder value, LocalBuilder unpacker, LocalBuilder contextLocal, DasherContext context, UnexpectedFieldBehaviour unexpectedFieldBehaviour)
        {
            var nullableType = value.LocalType;
            var valueType = nullableType.GetGenericArguments().Single();

            ITypeProvider valueProvider;
            if (!context.TryGetTypeProvider(valueType, out valueProvider))
                throw new Exception($"Unable to deserialise values of type Nullable<{valueType}> from MsgPack data.");

            var lblNull = ilg.DefineLabel();
            var lblExit = ilg.DefineLabel();

            ilg.Emit(OpCodes.Ldloc, unpacker);
            ilg.Emit(OpCodes.Call, typeof(Unpacker).GetMethod(nameof(Unpacker.TryReadNull)));

            ilg.Emit(OpCodes.Brtrue, lblNull);

            // non-null
            var nonNullValue = ilg.DeclareLocal(valueType);
            valueProvider.Deserialise(ilg, name, targetType, nonNullValue, unpacker, contextLocal, context, unexpectedFieldBehaviour);
            ilg.Emit(OpCodes.Ldloca, value);
            ilg.Emit(OpCodes.Ldloc, nonNullValue);
            ilg.Emit(OpCodes.Call, nullableType.GetConstructor(new [] {valueType}));

            ilg.Emit(OpCodes.Br, lblExit);
            ilg.MarkLabel(lblNull);

            // null
            ilg.Emit(OpCodes.Ldloca, value);
            ilg.Emit(OpCodes.Initobj, nullableType);

            ilg.MarkLabel(lblExit);
        }
    }
}