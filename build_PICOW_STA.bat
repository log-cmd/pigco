@echo off

if not exist build mkdir build
pushd build

REM buildの中消したほうがいい

cmake .. -G Ninja -DBUILD_MODE=PICOW_STA -DSTA_SSID="HUAWEI_EXPLOIT" -DSTA_PASSWORD="1234567890"
ninja

popd