#ifndef APP_H
#define APP_H

#include "pico/stdlib.h"
#include "lwip/pbuf.h"
#include "lwip/udp.h"

extern volatile bool notify_flag;
extern uint8_t from_udp_buffer[22];
void enterBufferLock();
void exitBufferLock();

void udp_recv_callback(void *arg, struct udp_pcb *pcb, struct pbuf *p, const ip_addr_t *addr, u16_t port);

void app_init();
void app_run();

#endif