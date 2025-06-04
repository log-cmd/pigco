#include "pico/stdlib.h"
#include "pico/cyw43_arch.h"
#include "pico/multicore.h"

#include "SwitchUsb.h"
#include "tusb.h"

#include "lwip/pbuf.h"
#include "lwip/udp.h"

#ifndef WIFI_SSID
#define WIFI_SSID "SSID_NOT_SET"
#endif
#ifndef WIFI_PASSWORD
#define WIFI_PASSWORD "PASSWORD_NOT_SET"
#endif

SwitchCommon *switchCommon = new SwitchUsb();

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
  critical_section_exit(&buffer_lock);
}

void enterBufferLock()
{
  critical_section_enter_blocking(&buffer_lock);
}

void exitBufferLock()
{
  critical_section_exit(&buffer_lock);
}

uint8_t from_udp_buffer[64];

bool blink = false;

static void udp_recv_callback(void *arg, struct udp_pcb *pcb, struct pbuf *p,
                              const ip_addr_t *addr, u16_t port)
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
      if (p->len >= 4 + 64)
      {
        enterBufferLock();
        memcpy(from_udp_buffer, data + 4, 64);
        exitBufferLock();
      }
    }
  }

  pbuf_free(p);
}

void wifi()
{
  cyw43_arch_enable_sta_mode();
  cyw43_wifi_pm(&cyw43_state, CYW43_DEFAULT_PM & ~0xf);

  const char *ssid = WIFI_SSID;
  const char *password = WIFI_PASSWORD;

  while (cyw43_arch_wifi_connect_timeout_ms(ssid, password, CYW43_AUTH_WPA2_AES_PSK, 10000))
  {
    printf("Retrying Wi-Fi...\n");
  }

  struct udp_pcb *pcb = udp_new();
  if (!pcb)
  {
    panic("Failed to create UDP PCB");
  }

  err_t err = udp_bind(pcb, IP_ADDR_ANY, 12345);
  if (err != ERR_OK)
  {
    panic("Failed to bind UDP PCB: %s", lwip_strerr(err));
  }

  udp_recv(pcb, udp_recv_callback, NULL);

  // Turn on Pico W LED
  cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, 1);
}

void controller_loop()
{
  switchCommon->init();
}

int main()
{
  stdio_init_all();

  initBufferLock();

  cyw43_arch_init();

  wifi();

  sleep_ms(1000);
  multicore_launch_core1(controller_loop);

  while (true)
  {
    cyw43_arch_poll();
    sleep_us(100);
  }
}