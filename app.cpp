#include "app.h"
#include "SwitchUsb.h"
#include "tusb.h"

SwitchCommon *switchCommon = new SwitchUsb();

volatile bool notify_flag = false;

void tud_hid_set_report_cb(uint8_t itf, uint8_t report_id,
                           hid_report_type_t report_type, uint8_t const *buffer,
                           uint16_t bufsize)
{
    hid_report_data_callback(switchCommon, (uint16_t)buffer[0], (uint8_t *)buffer, bufsize);
}

uint16_t tud_hid_get_report_cb(uint8_t itf, uint8_t report_id,
                               hid_report_type_t report_type, uint8_t *buffer,
                               uint16_t reqlen)
{
    return reqlen;
}

critical_section_t buffer_lock;

static void initBufferLock()
{
    critical_section_init(&buffer_lock);
}

void enterBufferLock()
{
    critical_section_enter_blocking(&buffer_lock);
}

void exitBufferLock()
{
    critical_section_exit(&buffer_lock);
}

uint8_t from_udp_buffer[22];

void app_recv(uint8_t *data, size_t len)
{
#if PIGCO_RP2350ETH
    if (len == 22)
    {
        enterBufferLock();
        memcpy(from_udp_buffer, data, 22);
        exitBufferLock();
        notify_flag = true;
    }
#else
    if (len >= 4 && data[0] == 0x01 && data[1] == 0x02 && data[2] == 0x03 && data[3] == 0x04)
    {
        if (len >= 4 + 22)
        {
            enterBufferLock();
            memcpy(from_udp_buffer, data + 4, 22);
            exitBufferLock();
            notify_flag = true;
        }
    }
#endif
}

void app_init()
{
    initBufferLock();
}

void app_run()
{
    switchCommon->init();
}