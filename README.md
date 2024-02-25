# Dear ImGui Bindings for .Net 8

Uses [dear_bindings](https://github.com/dearimgui/dear_bindings)

# State

- Compiles `cimgui in` Docker for `win-x64`, `linux-x64`, `macos-arm`
- Generates definitions in Docker
- Generates C# Enums, Enum constants, Typedefs (functions only), Defines, Structs, Functions.
- Done: Sample program

InProgress: **Managed wrapper**

## Pointer-based sample compiles and runs

![image](https://github.com/DearImGuiBindingsNet/DearImGuiBindingsNet/assets/44116740/defada25-2731-442c-9813-6bb7ff1b9509)

# Building

`cimgui` lib and `dear_bindings` definitions are built in Docker

### Supported natives:
* `win-x64`
* `linux-x64`
* `macos-arm` (technically, produced `dylib` should run on linux, but it's untested)

### Compiling natives
- Variant 1
    * run `build_natives.sh`
    * this will produce outputs to `cimgui` folder, which should allow you to run this repo automatically
- Variant 2
    * run the `dear_bindings.Dockerfile` mounting your desired output folder to `/cimgui_build`
    * example `docker run --rm -i -v "${PWD}/cimgui:/cimgui_build" dear_bindings:v1`

`cimgui.json`, `cimgui.h` and `cimgui.cpp` from running `dear_binding` are exported as well, if you need them

### Running C# code generator

* In `DearImGuiGenerator` folder run
```sh
dotnet run
```