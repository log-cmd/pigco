@echo off

if not exist build mkdir build
pushd build

REM buildの中消したほうがいい

cmake .. -G Ninja -DBUILD_MODE=RP2350ETH -DPICO_BOARD=waveshare_rp2350_eth
ninja

popd