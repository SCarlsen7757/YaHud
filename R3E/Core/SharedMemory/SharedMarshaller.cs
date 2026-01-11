using R3E.Data;
using System.Reflection;
using System.Runtime.InteropServices;

namespace R3E.Core.SharedMemory
{
    public static class SharedMarshaller
    {
        public static bool TryMarshalShared(byte[] src, out Shared value)
        {
            value = new();
            if (src.Length < Marshal.SizeOf<Shared>()) return false;

            var handle = GCHandle.Alloc(src, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                value = Marshal.PtrToStructure<Shared>(ptr);
                return true;
            }
            catch (ArgumentException)
            {
                // Invalid structure or pointer
                return false;
            }
            catch (MissingMethodException)
            {
                // Structure doesn't have parameterless constructor
                return false;
            }
            catch (TargetInvocationException)
            {
                // Error in structure constructor
                return false;
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }
    }
}
