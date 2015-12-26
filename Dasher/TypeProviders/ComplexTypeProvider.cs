using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Dasher.TypeProviders
{
    internal sealed class ComplexTypeProvider : ITypeProvider
    {
        // TODO should support complex structs too
        // TODO cache subtype deserialiser instances in fields of generated class (requires moving away from DynamicMethod)

        public bool CanProvide(Type type) => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 1;

        public void Serialise(ILGenerator ilg, LocalBuilder value, LocalBuilder packer, DasherContext context)
        {
            var type = value.LocalType;

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
                ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.Pack), new[] { typeof(string) }));

                // get property value
                ilg.Emit(type.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc, value);
                ilg.Emit(OpCodes.Call, prop.GetMethod);
                ilg.Emit(OpCodes.Stloc, propValue);

                // find the property type's provider
                ITypeProvider provider;
                if (!context.TryGetTypeProvider(prop.PropertyType, out provider))
                    throw new Exception($"Unable to serialise type {prop.PropertyType}");

                // write property value
                provider.Serialise(ilg, propValue, packer, context);
            }
        }

        public void Deserialise(ILGenerator ilg, string name, Type targetType, LocalBuilder value, LocalBuilder unpacker, LocalBuilder contextLocal, DasherContext context, UnexpectedFieldBehaviour unexpectedFieldBehaviour)
        {
            ilg.LoadType(value.LocalType);
            ilg.Emit(OpCodes.Ldc_I4, (int)unexpectedFieldBehaviour);
            ilg.Emit(OpCodes.Ldloc, contextLocal);
            ilg.Emit(OpCodes.Newobj, typeof(Deserialiser).GetConstructor(new[] { typeof(Type), typeof(UnexpectedFieldBehaviour), typeof(DasherContext) }));
            ilg.Emit(OpCodes.Ldloc, unpacker);
            ilg.Emit(OpCodes.Call, typeof(Deserialiser).GetMethod(nameof(Deserialiser.Deserialise), new[] { typeof(Unpacker) }));
            ilg.Emit(OpCodes.Castclass, value.LocalType);
            ilg.Emit(OpCodes.Stloc, value);
        }
    }
}