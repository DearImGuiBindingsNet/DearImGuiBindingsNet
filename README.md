# Dear ImGui Bindings for .Net 8

Uses [dear_bindings](https://github.com/dearimgui/dear_bindings)

# State

- Compiles cimgui in Docker
- Generates definitions in Docker
- Generates C# Enums, Enum constants, Typedefs (functions only), Defines, Structs, Functions.
- Done: Sample program

## Pointer-based sample compiles and runs

![image](https://github.com/DearImGuiBindingsNet/DearImGuiBindingsNet/assets/44116740/defada25-2731-442c-9813-6bb7ff1b9509)

InProgress: Managed wrapper

# Building

cimgui lib and dear_bindings definitions are built in Docker

run `build_natives.sh` on windows or linux (compiles with gcc)

then just run this generator
```sh
dotnet run
```