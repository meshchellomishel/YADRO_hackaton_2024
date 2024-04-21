#include <inttypes.h>
#include "mem.h"
#include "sc_print.h"
#include <stdbool.h>



#define UART_BASE_ADDR  0xe0000000
#define UART_REG_RBR ( UART_BASE_ADDR + 0x00) // Receiver Buffer Register (Read Only)
#define UART_REG_DLL ( UART_BASE_ADDR + 0x00) // Divisor Latch (LS)
#define UART_REG_THR ( UART_BASE_ADDR + 0x00) // Transmitter Holding Register (Write Only)
#define UART_REG_DLM ( UART_BASE_ADDR + 0x01) // Divisor Latch (MS)
#define UART_REG_IER ( UART_BASE_ADDR + 0x01) // Interrupt Enable Register
#define UART_REG_IIR ( UART_BASE_ADDR + 0x02) // Interrupt Identity Register (Read Only)
#define UART_REG_FCR ( UART_BASE_ADDR + 0x02) // FIFO Control Register (Write Only)
#define UART_REG_LCR ( UART_BASE_ADDR + 0x03) // Line Control Register
#define UART_REG_MCR ( UART_BASE_ADDR + 0x04) // MODEM Control Register
#define UART_REG_LSR ( UART_BASE_ADDR + 0x05) // Line Status Register
#define UART_REG_MSR ( UART_BASE_ADDR + 0x06) // MODEM Status Register
#define UART_REG_SCR ( UART_BASE_ADDR + 0x07) // Scratch Register



#define UART_LCB_LEN_BITS_8 2
#define UART_LCB_DLAB       0x80


#define UART_LSR_THR_EMPTY      0b01000000
#define UART_LSR_PARITY_ERR     0b01000100
#define UART_LSR_DATA_AVAILABLE 0b00000001

static void init_uart(void)
{
    int key;

    /* LCR(rw) */
    key = UART_REG_LCR;
    WRITE_MEMORY(key, 8, 3 | 8 | UART_LCB_DLAB);

    /* DLL(rw) */
    key = UART_REG_DLL;
    WRITE_MEMORY(key, 8, 100);

    /* LCR(rw) */
    key = UART_REG_LCR;
    WRITE_MEMORY(key, 8, 3 | 8);
}

int main(void)
{
    sc_printf("Test: UART DPI TEST\n");

    int key;
    int ret_val = 0;

    init_uart();

    key = UART_REG_THR;
    WRITE_MEMORY(key, 8, 1);
    ret_val = 1;



on_exit:
    if (ret_val)
        sc_printf("FAILED\n");
    else
        sc_printf("PASSED\n");

    return 0;
}
