using System.Runtime.InteropServices;
using DearImGuiBindings;

unsafe
{
    var context = ImGuiNative.ImGui_CreateContext((ImGuiNative.ImFontAtlas*) IntPtr.Zero);

    ImGuiNative.ImGui_SetCurrentContext(context);

    var io = ImGuiNative.ImGui_GetIO();

    byte* tex_pixels = null;
    int tex_w, tex_h;
    ImGuiNative.ImFontAtlas_GetTexDataAsRGBA32(
        io->Fonts,
        &tex_pixels,
        &tex_w,
        &tex_h,
        null
    );

    float f = 0.5f;
    for (int i = 0; i < 20; i++)
    {
        io->DisplaySize.x = 1920;
        io->DisplaySize.y = 1080;
        io->DeltaTime = 1.0f / 60.0f;

        ImGuiNative.ImGui_NewFrame();

        fixed (byte* p = "Example window\0"u8)
        {
            ImGuiNative.ImGui_Begin(p, (bool*) 1, 0);
        }

        fixed (byte* userData = "this is UserData"u8)
        {
            fixed (byte* comboLabel = "some combo\0"u8)
            {
                var cb = new ImGuiNative.ImGui_ComboCallbackgetterDelegate(
                    (data, idx) =>
                    {
                        Console.WriteLine($"Callback {idx}");
                        byte* ptr = (byte*) data;

                        // just a dumb print
                        while (*ptr != '\0')
                        {
                            Console.Write((char) *ptr);
                            ptr++;
                        }

                        Console.WriteLine();

                        return (byte*) 0;
                    });

                int currentIndex = 1;

                ImGuiNative.ImGui_ComboCallback(
                    comboLabel,
                    &currentIndex,
                    (ImGuiNative.ImGui_ComboCallbackgetterDelegate*) Marshal.GetFunctionPointerForDelegate(cb),
                    userData,
                    items_count: 10
                );
            }
        }

        fixed (byte* p = "float"u8)
        {
            ImGuiNative.ImGui_SliderFloat(
                p,
                &f,
                0.0f,
                1.0f
            );
        }

        ImGuiNative.ImGui_End();

        ImGuiNative.ImGui_ShowDemoWindow((bool*) 1);

        ImGuiNative.ImGui_Render();

        Console.WriteLine($"Ran frame {i}");
    }
}