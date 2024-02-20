# Dear ImGui Bindings for .Net 8

Uses [dear_bindings](https://github.com/dearimgui/dear_bindings)

# State

- Compiles cimgui in Docker
- Generates definitions in Docker
- Generates C# Enums, Enum constants, Typedefs.

InProgress: Defines, Delegates, Functions, Structs

# Building

cimgui lib and dear_bindings definitions are built in Docker

run on windows or linux
```shell
docker build -f dear_bindings.Dockerfile -t dear_bindings:v1 .;
docker run --rm -it -v "${PWD}/cimgui:/cimgui_build" dear_bindings:v1;
```

then just run this generator
```sh
dotnet run
```