#if !PIGCO_USE_AP

#include "pico/stdlib.h"
#include "pico/cyw43_arch.h"
#include "pico/multicore.h"
#include "app.h"

#include "lwip/pbuf.h"
#include "lwip/udp.h"

#ifndef WIFI_SSID
#define WIFI_SSID "SSID_NOT_SET"
#endif
#ifndef WIFI_PASSWORD
#define WIFI_PASSWORD "PASSWORD_NOT_SET"
#endif

bool wifi()
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
    return false;
  }

  err_t err = udp_bind(pcb, IP_ADDR_ANY, 12345);
  if (err != ERR_OK)
  {
    return false;
  }

  udp_recv(pcb, udp_recv_callback, NULL);

  // Turn on Pico W LED
  cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, 1);

  return true;
}

int main()
{
  stdio_init_all();

  cyw43_arch_init();

  app_init();

  if (!wifi())
  {
    return 1;
  }

  multicore_launch_core1(app_run);

  while (true)
  {
    cyw43_arch_poll();
  }
}

#endif // !PIGCO_USE_AP
