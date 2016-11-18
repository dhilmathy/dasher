using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Dasher.TypeProviders
{
    internal sealed class EmptyProvider : ITypeProvider
    {
        public bool CanProvide(Type type) => type == typeof(Empty);

        public bool UseDefaultNullHandling(Type valueType) => false;

        public bool TryEmitSerialiseCode(ILGenerator ilg, ThrowBlockGatherer throwBlocks, ICollection<string> errors, LocalBuilder value, LocalBuilder packer, LocalBuilder contextLocal, DasherContext context)
        {
            ilg.Emit(OpCodes.Ldloc, packer);
            ilg.Emit(OpCodes.Ldc_I4_0);
            ilg.Emit(OpCodes.Call, Methods.Packer_PackMapHeader);

            return true;
        }

        public bool TryEmitDeserialiseCode(ILGenerator ilg, ThrowBlockGatherer throwBlocks, ICollection<string> errors, string name, Type targetType, LocalBuilder value, LocalBuilder unpacker, LocalBuilder contextLocal, DasherContext context, UnexpectedFieldBehaviour unexpectedFieldBehaviour)
        {
            if (unexpectedFieldBehaviour == UnexpectedFieldBehaviour.Ignore)
            {
                // When ignoring unexpected fields, it doesn't matter what we receive in the message
                // as we will always accept the value and store 'null' on the target.
                ilg.Emit(OpCodes.Ldloc, unpacker);
                ilg.Emit(OpCodes.Call, Methods.Unpacker_SkipValue);
            }
            else
            {
                var end = ilg.DefineLabel();

                // Check for null
                ilg.Emit(OpCodes.Ldloc, unpacker);
                ilg.Emit(OpCodes.Call, Methods.Unpacker_TryReadNull);
                ilg.Emit(OpCodes.Brtrue, end);

                var mapSize = ilg.DeclareLocal(typeof(int));

                // Try to read the map header
                ilg.Emit(OpCodes.Ldloc, unpacker);
                ilg.Emit(OpCodes.Ldloca, mapSize);
                ilg.Emit(OpCodes.Call, Methods.Unpacker_TryReadMapLength);

                // If the unpacker method failed (returned false), throw
                throwBlocks.ThrowIfFalse(() =>
                {
                    var format = ilg.DeclareLocal(typeof(Format));
                    ilg.Emit(OpCodes.Ldarg_0);
                    ilg.Emit(OpCodes.Ldloca, format);
                    ilg.Emit(OpCodes.Call, Methods.Unpacker_TryPeekFormat);
                    ilg.Emit(OpCodes.Pop);

                    ilg.Emit(OpCodes.Ldstr, $"Unable to deserialise {nameof(Empty)} type for \"{{0}}\". Expected MsgPack format Null or Map, got {{1}}.");
                    ilg.Emit(OpCodes.Ldstr, name);
                    ilg.Emit(OpCodes.Ldloc, format);
                    ilg.Emit(OpCodes.Box, typeof(Format));
                    ilg.Emit(OpCodes.Call, Methods.String_Format_String_Object_Object);
                    ilg.LoadType(targetType);
                    ilg.Emit(OpCodes.Newobj, Methods.DeserialisationException_Ctor_String_Type);
                    ilg.Emit(OpCodes.Throw);
                });

                ilg.Emit(OpCodes.Ldloc, mapSize);
                throwBlocks.ThrowIfTrue(() =>
                {
                    ilg.Emit(OpCodes.Ldstr, $"Unable to deserialise {nameof(Empty)} type for \"{{0}}\". Expected map with 0 entries, got {{1}}.");
                    ilg.Emit(OpCodes.Ldstr, name);
                    ilg.Emit(OpCodes.Ldloc, mapSize);
                    ilg.Emit(OpCodes.Box, typeof(int));
                    ilg.Emit(OpCodes.Call, Methods.String_Format_String_Object_Object);
                    ilg.LoadType(targetType);
                    ilg.Emit(OpCodes.Newobj, Methods.DeserialisationException_Ctor_String_Type);
                    ilg.Emit(OpCodes.Throw);
                });

                ilg.MarkLabel(end);
            }

            ilg.Emit(OpCodes.Ldnull);
            ilg.Emit(OpCodes.Stloc, value);

            return true;
        }
    }
}