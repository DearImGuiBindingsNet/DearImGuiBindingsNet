using System.Runtime.InteropServices;

namespace DearImguiGenerator;

public class TestImGuiNative
{
    [DllImport("cimgui/cimgui", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ImGui_CreateContext();

    [DllImport("cimgui/cimgui", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ImGui_SetCurrentContext(IntPtr context);
}