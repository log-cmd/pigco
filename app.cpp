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

void udp_recv_callback(void *arg, struct udp_pcb *pcb, struct pbuf *p, const ip_addr_t *addr, u16_t port)
{
    if (!p)
    {
        return;
    }

    if (p->len >= 4)
    {
        uint8_t *data = (uint8_t *)p->payload;
        if (data[0] == 0x01 && data[1] == 0x02 && data[2] == 0x03 && data[3] == 0x04)
        {
            if (p->len >= 4 + 22)
            {
                enterBufferLock();
                memcpy(from_udp_buffer, data + 4, 22);
                exitBufferLock();
                notify_flag = true;
            }
        }
    }

    pbuf_free(p);
}

void app_init()
{
    initBufferLock();
}

void app_run()
{
    switchCommon->init();
}