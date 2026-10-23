using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace TripleInfusion;

/// <summary>
/// Reads nullable (T?) fields of game objects directly from memory.
/// Il2CppInterop's generated getters for Nullable value-type fields throw (NullReferenceException in
/// Il2CppObjectBase.CreateGCHandle), so read the struct's hasValue / value straight from the object instead.
/// Only for 4-byte T (float, int, enums).
/// </summary>
internal static unsafe class Raw
{
    private static readonly Dictionary<string, int> fieldOffsets = new();
    private static int hasValueAt = -1, valueAt = -1;

    private static int FieldOffset<TOwner>(string field) where TOwner : Il2CppObjectBase
    {
        string key = typeof(TOwner).FullName + "." + field;
        if (fieldOffsets.TryGetValue(key, out int o)) return o;
        IntPtr f = IL2CPP.GetIl2CppField(Il2CppClassPointerStore<TOwner>.NativeClassPtr, field);
        o = f == IntPtr.Zero ? -1 : (int)IL2CPP.il2cpp_field_get_offset(f);
        fieldOffsets[key] = o;
        if (o < 0) Plugin.Log.LogError($"Field {key} not found.");
        return o;
    }

    private static void NullableLayout()
    {
        if (hasValueAt >= 0) return;
        hasValueAt = 0; valueAt = 4; // .NET layout: bool hasValue; T value (4-aligned)
        try
        {
            IntPtr cls = Il2CppClassPointerStore<Il2CppSystem.Nullable<float>>.NativeClassPtr;
            IntPtr hv = IL2CPP.GetIl2CppField(cls, "hasValue"), v = IL2CPP.GetIl2CppField(cls, "value");
            if (hv != IntPtr.Zero && v != IntPtr.Zero)
            {
                // Value-type field offsets include the boxed object header.
                int header = IntPtr.Size * 2;
                int a = (int)IL2CPP.il2cpp_field_get_offset(hv) - header, b = (int)IL2CPP.il2cpp_field_get_offset(v) - header;
                if (a >= 0 && b >= 0 && a < 16 && b < 16) { hasValueAt = a; valueAt = b; }
            }
        }
        catch { }
    }

    /// <summary>True and the value if the TOwner's T? field has a value.</summary>
    internal static bool TryGetNullable4<TOwner>(TOwner obj, string field, out int bits) where TOwner : Il2CppObjectBase
    {
        bits = 0;
        int off = FieldOffset<TOwner>(field);
        if (off < 0 || obj == null) return false;
        NullableLayout();
        byte* p = (byte*)obj.Pointer + off;
        if (p[hasValueAt] == 0) return false;
        bits = *(int*)(p + valueAt);
        return true;
    }
}
