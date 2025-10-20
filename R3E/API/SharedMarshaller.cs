using System.Runtime.InteropServices;
using R3E.Data;

namespace R3E.API
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
            catch
            {
                return false;
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }
    }
}
