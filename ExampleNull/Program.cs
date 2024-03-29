﻿using ImGuiNative;
using static ImGuiNative.ImGuiNative;

unsafe
{
    var context = ImGui_CreateContext((ImFontAtlas*) IntPtr.Zero);

    ImGui_SetCurrentContext(context);

    var io = ImGui_GetIO();

    byte* tex_pixels = null;
    int tex_w, tex_h;
    ImFontAtlas_GetTexDataAsRGBA32(io->Fonts, &tex_pixels, &tex_w, &tex_h, null);

    float f = 0.5f;
    for (int i = 0; i < 20; i++)
    {
        io->DisplaySize.x = 1920;
        io->DisplaySize.y = 1080;
        io->DeltaTime = 1.0f / 60.0f;
    
        ImGui_NewFrame();

        fixed (byte* p = "Hello, world!"u8)
        {
            ImGui_Text(p);
        }

        fixed (byte* p = "float"u8)
        {
            ImGui_SliderFloat(p, &f, 0.0f, 1.0f);
        }

        // fixed (byte* p = "float"u8) ImGuiNative.ImGui_TextV(p, __arglist(1, 2, 3));

        ImGui_ShowDemoWindow((bool*)1);

        ImGui_Render();

        Console.WriteLine($"Ran frame {i}");
    }
}