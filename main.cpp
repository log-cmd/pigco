#include "app.h"
#include "pico/multicore.h"

#define PACKET_MAGIC1 0x06
#define PACKET_MAGIC2 0x14
#define MAX_DATA_SIZE 64

// CRC16-CCITT (poly 0x1021, init 0xFFFF)
uint16_t calc_crc16(const uint8_t *data, uint16_t len)
{
    uint16_t crc = 0xFFFF;
    for (uint16_t i = 0; i < len; i++)
    {
        crc ^= (uint16_t)data[i] << 8;
        for (uint8_t j = 0; j < 8; j++)
        {
            if (crc & 0x8000)
                crc = (crc << 1) ^ 0x1021;
            else
                crc <<= 1;
        }
    }
    return crc;
}

#if PIGCO_PICOW_AP || PIGCO_PICOW_STA

#include <string.h>
#include "pico/cyw43_arch.h"
#include "pico/stdlib.h"

#include "lwip/pbuf.h"
#include "lwip/tcp.h"

void udp_recv_callback(void *arg, struct udp_pcb *pcb, struct pbuf *p, const ip_addr_t *addr, u16_t port)
{
    if (!p)
    {
        return;
    }

    uint8_t *payload = (uint8_t *)p->payload;

    if (p->len > 4 && payload[0] == PACKET_MAGIC1 && payload[1] == PACKET_MAGIC2)
    {
        uint16_t crc_packet = (payload[2] << 8) | payload[3];
        uint8_t data_size = payload[4];
        if ((p->len == 5 + data_size) && (data_size < MAX_DATA_SIZE))
        {
            uint8_t *data = payload + 5;
            uint16_t crc_calculated = calc_crc16(data, data_size);

            if (crc_calculated == crc_packet)
            {
                app_recv(data, data_size);
            }
        }
    }

    pbuf_free(p);
}

#endif

#if PIGCO_PICOW_AP

#include "lib/dhcpserver/dhcpserver.h"
#include "lib/dnsserver/dnsserver.h"

#ifndef AP_SSID
#define AP_SSID "picow_test"
#endif
#ifndef AP_PASSWORD
#define AP_PASSWORD "password"
#endif

#define TCP_PORT 80
#define DEBUG_printf printf
#define POLL_TIME_S 5
#define HTTP_GET "GET"
#define HTTP_RESPONSE_HEADERS "HTTP/1.1 %d OK\nContent-Length: %d\nContent-Type: text/html; charset=utf-8\nConnection: close\n\n"
#define LED_TEST_BODY "<html><body><h1>Hello from Pico.</h1><p>Led is %s</p><p><a href=\"?led=%d\">Turn led %s</a></body></html>"
#define LED_PARAM "led=%d"
#define LED_TEST "/ledtest"
#define LED_GPIO 0
#define HTTP_RESPONSE_REDIRECT "HTTP/1.1 302 Redirect\nLocation: http://%s" LED_TEST "\n\n"

typedef struct TCP_SERVER_T_
{
    struct tcp_pcb *server_pcb;
    bool complete;
    ip_addr_t gw;
} TCP_SERVER_T;

typedef struct TCP_CONNECT_STATE_T_
{
    struct tcp_pcb *pcb;
    int sent_len;
    char headers[128];
    char result[256];
    int header_len;
    int result_len;
    ip_addr_t *gw;
} TCP_CONNECT_STATE_T;

static err_t tcp_close_client_connection(TCP_CONNECT_STATE_T *con_state, struct tcp_pcb *client_pcb, err_t close_err)
{
    if (client_pcb)
    {
        assert(con_state && con_state->pcb == client_pcb);
        tcp_arg(client_pcb, NULL);
        tcp_poll(client_pcb, NULL, 0);
        tcp_sent(client_pcb, NULL);
        tcp_recv(client_pcb, NULL);
        tcp_err(client_pcb, NULL);
        err_t err = tcp_close(client_pcb);
        if (err != ERR_OK)
        {
            DEBUG_printf("close failed %d, calling abort\n", err);
            tcp_abort(client_pcb);
            close_err = ERR_ABRT;
        }
        if (con_state)
        {
            free(con_state);
        }
    }
    return close_err;
}

static void tcp_server_close(TCP_SERVER_T *state)
{
    if (state->server_pcb)
    {
        tcp_arg(state->server_pcb, NULL);
        tcp_close(state->server_pcb);
        state->server_pcb = NULL;
    }
}

static err_t tcp_server_sent(void *arg, struct tcp_pcb *pcb, u16_t len)
{
    TCP_CONNECT_STATE_T *con_state = (TCP_CONNECT_STATE_T *)arg;
    DEBUG_printf("tcp_server_sent %u\n", len);
    con_state->sent_len += len;
    if (con_state->sent_len >= con_state->header_len + con_state->result_len)
    {
        DEBUG_printf("all done\n");
        return tcp_close_client_connection(con_state, pcb, ERR_OK);
    }
    return ERR_OK;
}

static int test_server_content(const char *request, const char *params, char *result, size_t max_result_len)
{
    int len = 0;
    if (strncmp(request, LED_TEST, sizeof(LED_TEST) - 1) == 0)
    {
        // Get the state of the led
        bool value;
        cyw43_gpio_get(&cyw43_state, LED_GPIO, &value);
        int led_state = value;

        // See if the user changed it
        if (params)
        {
            int led_param = sscanf(params, LED_PARAM, &led_state);
            if (led_param == 1)
            {
                if (led_state)
                {
                    // Turn led on
                    cyw43_gpio_set(&cyw43_state, LED_GPIO, true);
                }
                else
                {
                    // Turn led off
                    cyw43_gpio_set(&cyw43_state, LED_GPIO, false);
                }
            }
        }
        // Generate result
        if (led_state)
        {
            len = snprintf(result, max_result_len, LED_TEST_BODY, "ON", 0, "OFF");
        }
        else
        {
            len = snprintf(result, max_result_len, LED_TEST_BODY, "OFF", 1, "ON");
        }
    }
    return len;
}

err_t tcp_server_recv(void *arg, struct tcp_pcb *pcb, struct pbuf *p, err_t err)
{
    TCP_CONNECT_STATE_T *con_state = (TCP_CONNECT_STATE_T *)arg;
    if (!p)
    {
        DEBUG_printf("connection closed\n");
        return tcp_close_client_connection(con_state, pcb, ERR_OK);
    }
    assert(con_state && con_state->pcb == pcb);
    if (p->tot_len > 0)
    {
        DEBUG_printf("tcp_server_recv %d err %d\n", p->tot_len, err);
#if 0
        for (struct pbuf *q = p; q != NULL; q = q->next) {
            DEBUG_printf("in: %.*s\n", q->len, q->payload);
        }
#endif
        // Copy the request into the buffer
        pbuf_copy_partial(p, con_state->headers, p->tot_len > sizeof(con_state->headers) - 1 ? sizeof(con_state->headers) - 1 : p->tot_len, 0);

        // Handle GET request
        if (strncmp(HTTP_GET, con_state->headers, sizeof(HTTP_GET) - 1) == 0)
        {
            char *request = con_state->headers + sizeof(HTTP_GET); // + space
            char *params = strchr(request, '?');
            if (params)
            {
                if (*params)
                {
                    char *space = strchr(request, ' ');
                    *params++ = 0;
                    if (space)
                    {
                        *space = 0;
                    }
                }
                else
                {
                    params = NULL;
                }
            }

            // Generate content
            con_state->result_len = test_server_content(request, params, con_state->result, sizeof(con_state->result));
            DEBUG_printf("Request: %s?%s\n", request, params);
            DEBUG_printf("Result: %d\n", con_state->result_len);

            // Check we had enough buffer space
            if (con_state->result_len > sizeof(con_state->result) - 1)
            {
                DEBUG_printf("Too much result data %d\n", con_state->result_len);
                return tcp_close_client_connection(con_state, pcb, ERR_CLSD);
            }

            // Generate web page
            if (con_state->result_len > 0)
            {
                con_state->header_len = snprintf(con_state->headers, sizeof(con_state->headers), HTTP_RESPONSE_HEADERS,
                                                 200, con_state->result_len);
                if (con_state->header_len > sizeof(con_state->headers) - 1)
                {
                    DEBUG_printf("Too much header data %d\n", con_state->header_len);
                    return tcp_close_client_connection(con_state, pcb, ERR_CLSD);
                }
            }
            else
            {
                // Send redirect
                con_state->header_len = snprintf(con_state->headers, sizeof(con_state->headers), HTTP_RESPONSE_REDIRECT,
                                                 ipaddr_ntoa(con_state->gw));
                DEBUG_printf("Sending redirect %s", con_state->headers);
            }

            // Send the headers to the client
            con_state->sent_len = 0;
            err_t err = tcp_write(pcb, con_state->headers, con_state->header_len, 0);
            if (err != ERR_OK)
            {
                DEBUG_printf("failed to write header data %d\n", err);
                return tcp_close_client_connection(con_state, pcb, err);
            }

            // Send the body to the client
            if (con_state->result_len)
            {
                err = tcp_write(pcb, con_state->result, con_state->result_len, 0);
                if (err != ERR_OK)
                {
                    DEBUG_printf("failed to write result data %d\n", err);
                    return tcp_close_client_connection(con_state, pcb, err);
                }
            }
        }
        tcp_recved(pcb, p->tot_len);
    }
    pbuf_free(p);
    return ERR_OK;
}

static err_t tcp_server_poll(void *arg, struct tcp_pcb *pcb)
{
    TCP_CONNECT_STATE_T *con_state = (TCP_CONNECT_STATE_T *)arg;
    DEBUG_printf("tcp_server_poll_fn\n");
    return tcp_close_client_connection(con_state, pcb, ERR_OK); // Just disconnect clent?
}

static void tcp_server_err(void *arg, err_t err)
{
    TCP_CONNECT_STATE_T *con_state = (TCP_CONNECT_STATE_T *)arg;
    if (err != ERR_ABRT)
    {
        DEBUG_printf("tcp_client_err_fn %d\n", err);
        tcp_close_client_connection(con_state, con_state->pcb, err);
    }
}

static err_t tcp_server_accept(void *arg, struct tcp_pcb *client_pcb, err_t err)
{
    TCP_SERVER_T *state = (TCP_SERVER_T *)arg;
    if (err != ERR_OK || client_pcb == NULL)
    {
        DEBUG_printf("failure in accept\n");
        return ERR_VAL;
    }
    DEBUG_printf("client connected\n");

    // Create the state for the connection
    TCP_CONNECT_STATE_T *con_state = (TCP_CONNECT_STATE_T *)calloc(1, sizeof(TCP_CONNECT_STATE_T));
    if (!con_state)
    {
        DEBUG_printf("failed to allocate connect state\n");
        return ERR_MEM;
    }
    con_state->pcb = client_pcb; // for checking
    con_state->gw = &state->gw;

    // setup connection to client
    tcp_arg(client_pcb, con_state);
    tcp_sent(client_pcb, tcp_server_sent);
    tcp_recv(client_pcb, tcp_server_recv);
    tcp_poll(client_pcb, tcp_server_poll, POLL_TIME_S * 2);
    tcp_err(client_pcb, tcp_server_err);

    return ERR_OK;
}

static bool tcp_server_open(void *arg, const char *ap_name)
{
    TCP_SERVER_T *state = (TCP_SERVER_T *)arg;
    DEBUG_printf("starting server on port %d\n", TCP_PORT);

    struct tcp_pcb *pcb = tcp_new_ip_type(IPADDR_TYPE_ANY);
    if (!pcb)
    {
        DEBUG_printf("failed to create pcb\n");
        return false;
    }

    err_t err = tcp_bind(pcb, IP_ANY_TYPE, TCP_PORT);
    if (err)
    {
        DEBUG_printf("failed to bind to port %d\n", TCP_PORT);
        return false;
    }

    state->server_pcb = tcp_listen_with_backlog(pcb, 1);
    if (!state->server_pcb)
    {
        DEBUG_printf("failed to listen\n");
        if (pcb)
        {
            tcp_close(pcb);
        }
        return false;
    }

    tcp_arg(state->server_pcb, state);
    tcp_accept(state->server_pcb, tcp_server_accept);

    printf("Try connecting to '%s' (press 'd' to disable access point)\n", ap_name);
    return true;
}

void key_pressed_func(void *param)
{
    assert(param);
    TCP_SERVER_T *state = (TCP_SERVER_T *)param;
    int key = getchar_timeout_us(0); // get any pending key press but don't wait
    if (key == 'd' || key == 'D')
    {
        cyw43_arch_lwip_begin();
        cyw43_arch_disable_ap_mode();
        cyw43_arch_lwip_end();
        state->complete = true;
    }
}

int main()
{
    stdio_init_all();

    TCP_SERVER_T *state = (TCP_SERVER_T *)calloc(1, sizeof(TCP_SERVER_T));
    if (!state)
    {
        DEBUG_printf("failed to allocate state\n");
        return 1;
    }

    if (cyw43_arch_init())
    {
        DEBUG_printf("failed to initialise\n");
        return 1;
    }

    // Get notified if the user presses a key
    stdio_set_chars_available_callback(key_pressed_func, state);

    const char *ap_name = AP_SSID;
    const char *password = AP_PASSWORD;

    cyw43_arch_enable_ap_mode(ap_name, password, CYW43_AUTH_WPA2_AES_PSK);

    cyw43_wifi_pm(&cyw43_state, CYW43_DEFAULT_PM & ~0xf);

#if LWIP_IPV6
#define IP(x) ((x).u_addr.ip4)
#else
#define IP(x) (x)
#endif

    ip4_addr_t mask;
    IP(state->gw).addr = PP_HTONL(CYW43_DEFAULT_IP_AP_ADDRESS);
    IP(mask).addr = PP_HTONL(CYW43_DEFAULT_IP_MASK);

#undef IP

    // Start the dhcp server
    dhcp_server_t dhcp_server;
    dhcp_server_init(&dhcp_server, &state->gw, &mask);

    // Start the dns server
    dns_server_t dns_server;
    dns_server_init(&dns_server, &state->gw);

    if (!tcp_server_open(state, ap_name))
    {
        DEBUG_printf("failed to open server\n");
        return 1;
    }

    // Turn on Pico W LED
    cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, 1);

    app_init();

    {
        struct udp_pcb *pcb = udp_new();
        err_t err = udp_bind(pcb, IP_ADDR_ANY, 12345);
        udp_recv(pcb, udp_recv_callback, NULL);
    }

    multicore_launch_core1(app_run);

    state->complete = false;
    while (!state->complete)
    {
        // the following #ifdef is only here so this same example can be used in multiple modes;
        // you do not need it in your code
#if PICO_CYW43_ARCH_POLL
        // if you are using pico_cyw43_arch_poll, then you must poll periodically from your
        // main loop (not from a timer interrupt) to check for Wi-Fi driver or lwIP work that needs to be done.
        cyw43_arch_poll();
        // you can poll as often as you like, however if you have nothing else to do you can
        // choose to sleep until either a specified time, or cyw43_arch_poll() has work to do:
        cyw43_arch_wait_for_work_until(make_timeout_time_ms(1000));
#else
        // if you are not using pico_cyw43_arch_poll, then Wi-FI driver and lwIP work
        // is done via interrupt in the background. This sleep is just an example of some (blocking)
        // work you might be doing.
        sleep_ms(1000);
#endif
    }
    tcp_server_close(state);
    dns_server_deinit(&dns_server);
    dhcp_server_deinit(&dhcp_server);
    cyw43_arch_deinit();
    printf("Test complete\n");
    return 0;
}

#elif PIGCO_PICOW_STA

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

    const char *ssid = STA_SSID;
    const char *password = STA_PASSWORD;

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

#elif PIGCO_RP2350ETH

#include "lib/CH9120/CH9120.h"

#define UART_TIMEOUT_MS 100

bool read_timeout(uint8_t *data_out, uint16_t timeout_ms)
{
    uint32_t start_time = to_ms_since_boot(get_absolute_time());
    while (!uart_is_readable(UART_ID1))
    {
        if (to_ms_since_boot(get_absolute_time()) - start_time >= timeout_ms)
        {
            return false; // タイムアウト
        }
    }
    *data_out = uart_getc(UART_ID1);
    return true;
}

int main()
{
    CH9120_init();

    app_init();

    multicore_launch_core1(app_run);

    while (1)
    {
        // マジックを探す
        uint8_t magic;
        if (!read_timeout(&magic, UART_TIMEOUT_MS))
            continue;
        if (magic != PACKET_MAGIC1)
            continue; // マジックが一致しない場合は次のループへ

        // マジック2を取得
        uint8_t magic2;
        if (!read_timeout(&magic2, UART_TIMEOUT_MS))
            continue;
        if (magic2 != PACKET_MAGIC2)
            continue; // マジック2が一致しない場合は次のループへ

        // 16ビットチェックサムを取得
        uint8_t checksum, checksum2;
        if (!read_timeout(&checksum, UART_TIMEOUT_MS))
            continue;
        if (!read_timeout(&checksum2, UART_TIMEOUT_MS))
            continue;

        uint16_t crc16_packet = (checksum << 8) | checksum2;

        // データ長を取得
        uint8_t length;
        if (!read_timeout(&length, UART_TIMEOUT_MS))
            continue;

        if (length == 0 || length > MAX_DATA_SIZE)
            continue; // 不正な長さは破棄

        // データを取得
        uint8_t data[MAX_DATA_SIZE];
        bool timeout = false;
        for (uint8_t i = 0; i < length && i < MAX_DATA_SIZE; i++)
        {
            if (!read_timeout(&data[i], UART_TIMEOUT_MS))
            {
                timeout = true;
                break;
            }
        }
        if (timeout)
            continue;

        // CRC16を計算
        uint16_t crc16_calculated = calc_crc16(data, length);

        // チェックサムを検証
        if (crc16_calculated != crc16_packet)
        {
            continue; // エラー処理: 次のループへ
        }

        // TODO エラー率を計測してPCに送信してもいいかもしれない

        app_recv(data, length);
    }
}

#endif
