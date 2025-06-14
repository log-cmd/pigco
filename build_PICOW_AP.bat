@echo off

if not exist build mkdir build
pushd build

REM buildの中消したほうがいい

cmake .. -G Ninja -DBUILD_MODE=PICOW_AP
ninja

popd