#!/usr/bin/env bash

ensure_folder_exists() {
  # shellcheck disable=SC2181
  if [ ! -d $1 ]; then
    mkdir -p $1
  else
    echo "$1 dir is already present"
    echo "Clearing $1 directory"
    rm -R $1
    echo "$1 directory is empty"
  fi
}

scriptPath="`dirname \"$0\"`"

echo "running from $scriptPath"

cimguiPath=$scriptPath/cimgui

cd "$scriptPath/DearImGuiGenerator"
docker build -f dear_bindings.Dockerfile -t dear_bindings:v1 .

echo "--- BUILT ---"

cd ..

ensure_folder_exists cimgui

docker run --rm -i -v "${scriptPath}/cimgui:/cimgui_build" dear_bindings:v1

echo "--- RAN ---"

ensure_folder_exists 'DearImGuiGenerator/cimgui'
ensure_folder_exists 'DearImGuiBindings/runtimes/win-x64/native'
ensure_folder_exists 'DearImGuiBindings/runtimes/linux-x64/native'

cp cimgui/cimgui.json DearImGuiGenerator/cimgui/cimgui.json
cp cimgui/cimgui.dll DearImGuiBindings/runtimes/win-x64/native/cimgui.dll
cp cimgui/cimgui.so DearImGuiBindings/runtimes/linux-x64/native/cimgui.so

echo "Press any key to exit"
read -n 1 -s