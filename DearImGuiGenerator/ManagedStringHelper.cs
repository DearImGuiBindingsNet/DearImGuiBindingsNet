using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;

namespace DearImguiGenerator;

public static class ManagedStringHelper
{
    public static unsafe Span<byte> GetString(this IntPtr ptr)
    {
        var pc = (byte*)ptr;
        while (*pc != 0)
        {
            pc++;
        }
        return new Span<byte>((void*)ptr, (int)(pc - (byte*)ptr));
    }
}