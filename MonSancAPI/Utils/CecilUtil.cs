using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

namespace MonSancAPI.Utilities
{
    public static class CecilUtil
    {
        internal static bool IsSubTypeOf(this TypeDefinition typeDefinition, string typeFullName)
        {
            if (typeDefinition.FullName == typeFullName)
            {
                return true;
            }

            var typeDefBaseType = typeDefinition.BaseType?.Resolve();
            while (typeDefBaseType != null)
            {
                if (typeDefBaseType.FullName == typeFullName)
                {
                    return true;
                }

                typeDefBaseType = typeDefBaseType.BaseType?.Resolve();
            }

            return false;
        }
    }
}
