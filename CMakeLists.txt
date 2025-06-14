# Generated Cmake Pico project file

cmake_minimum_required(VERSION 3.13)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# Initialise pico_sdk from installed location
# (note this can come from environment, CMake cache etc)

# == DO NOT EDIT THE FOLLOWING LINES for the Raspberry Pi Pico VS Code Extension to work ==
if(WIN32)
    set(USERHOME $ENV{USERPROFILE})
else()
    set(USERHOME $ENV{HOME})
endif()
set(sdkVersion 2.1.1)
set(toolchainVersion 14_2_Rel1)
set(picotoolVersion 2.1.1)
set(picoVscode ${USERHOME}/.pico-sdk/cmake/pico-vscode.cmake)
if(EXISTS ${picoVscode})
    include(${picoVscode})
endif()
# ====================================================================================
set(PICO_BOARD pico_w CACHE STRING "Board type")
set(PICO_CXX_ENABLE_EXCEPTIONS 1)
include(pico_sdk_import.cmake)
project(pigco C CXX ASM)
pico_sdk_init()

set(BUILD_MODE "PICOW_AP" CACHE STRING "Build mode: PICOW_AP, PICOW_STA, RP2350ETH")

# アクセスポイントとして動作する場合のオプション
set(AP_SSID "HUAWEI-UNKNOWN" CACHE STRING "AP SSID")
set(AP_PASSWORD "password" CACHE STRING "AP Password")
set(AP_IP_ADDRESS "192.168.4.1" CACHE STRING "AP IP Address")

# ステーションモードで動作する場合のオプション
set(STA_SSID "" CACHE STRING "STA SSID")
set(STA_PASSWORD "" CACHE STRING "STA Password")
# 必要ならIP固定のコード追加 cpp対応必要

add_executable(pigco
    app.cpp
    SwitchCommon.cpp
    SwitchUsb.cpp
    main.cpp
    lib/dhcpserver/dhcpserver.c 
    lib/dnsserver/dnsserver.c
    lib/CH9120/CH9120.c
)

pico_set_program_name(pigco "pigco")
pico_set_program_version(pigco "0.1")

# Modify the below lines to enable/disable output over UART/USB
pico_enable_stdio_uart(pigco 0)
pico_enable_stdio_usb(pigco 0)

target_link_libraries(pigco
    pico_stdlib
    pico_rand
    pico_multicore
    tinyusb_device
)

# Add the standard include files to the build
target_include_directories(pigco PRIVATE
    ${CMAKE_CURRENT_LIST_DIR}
)

if(BUILD_MODE STREQUAL "PICOW_AP")
    target_link_libraries(pigco pico_cyw43_arch_lwip_poll)
    target_compile_definitions(pigco PRIVATE PIGCO_PICOW_AP=1)
    pico_configure_ip4_address(pigco PRIVATE CYW43_DEFAULT_IP_AP_ADDRESS ${AP_IP_ADDRESS})
    target_compile_definitions(pigco PRIVATE AP_SSID=\"${AP_SSID}\")
    target_compile_definitions(pigco PRIVATE AP_PASSWORD=\"${AP_PASSWORD}\")
elseif(BUILD_MODE STREQUAL "PICOW_STA")
    target_link_libraries(pigco pico_cyw43_arch_lwip_poll)
    target_compile_definitions(pigco PRIVATE PIGCO_PICOW_STA=1)
    target_compile_definitions(pigco PRIVATE STA_SSID=\"${STA_SSID}\")
    target_compile_definitions(pigco PRIVATE STA_PASSWORD=\"${STA_PASSWORD}\")
elseif(BUILD_MODE STREQUAL "RP2350ETH")
    target_compile_definitions(pigco PRIVATE PIGCO_RP2350ETH=1)
else()
    message(FATAL_ERROR "Unknown BUILD_MODE: ${BUILD_MODE}")
endif()

pico_add_extra_outputs(pigco)
