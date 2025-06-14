#ifndef APP_H
#define APP_H

#include "pico/stdlib.h"

extern volatile bool notify_flag;
extern uint8_t from_udp_buffer[22];
void enterBufferLock();
void exitBufferLock();

void app_recv(uint8_t *data, size_t len);

void app_init();
void app_run();

#endif