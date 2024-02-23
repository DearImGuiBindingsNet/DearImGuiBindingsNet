# syntax=docker/dockerfile:1
FROM alpine AS sources
WORKDIR /dear_bindings
RUN apk update
RUN apk add git
RUN git clone https://github.com/dearimgui/dear_bindings.git .

RUN git clone https://github.com/ocornut/imgui.git imgui
RUN git clone https://github.com/cimgui/cimgui.git cimgui

RUN cp cimgui/CMakeLists.txt CMakeLists.txt
RUN cp cimgui/Makefile Makefile

RUN rm -r cimgui

FROM python AS generator
WORKDIR /dear_bindings
COPY --from=sources /dear_bindings .
RUN pip3 install ply

RUN python3 dear_bindings.py --output cimgui imgui/imgui.h --generateunformattedfunctions

FROM ubuntu:20.04 AS compile-windows
WORKDIR /cimgui
COPY --from=generator /dear_bindings .

RUN apt-get update
RUN apt-get install -y cmake mingw-w64

WORKDIR /cimgui/build

#RUN x86_64-w64-mingw32-gcc 
ENV CXX='x86_64-w64-mingw32-gcc'
#RUN cmake -DCIMGUI_TEST=1 ..
RUN cmake -D CMAKE_CXX_FLAGS="-fno-exceptions -fno-rtti -fno-threadsafe-statics" ../
RUN cmake --build .

#FROM gcc AS compile-linux
#WORKDIR /cimgui
#COPY --from=generator /dear_bindings .
#
#RUN gcc 

FROM alpine AS final
WORKDIR /final
#COPY --from=compile-linux /cimgui/cimgui.so /final/cimgui.so
# cmake produces .so file, but it's a dll (because docker is linux)
COPY --from=compile-windows /cimgui/build/cimgui.so ./cimgui.dll
#COPY --from=generator /dear_bindings/cimgui.json /final/cimgui.json
#COPY --from=generator /dear_bindings/cimgui.h /final/cimgui.h
#COPY --from=generator /dear_bindings/cimgui.cpp /final/cimgui.cpp

RUN echo "Success"

CMD ["/bin/cp", "-r", "/final", "/cimgui_build/"]