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

#define DEFAULT_COUNTER     100


#define UART_IRR_SUCCESS    0xC0



static void init_apb(uint8_t data_size)
{
    int key;

    key = UART_REG_LCR;
    WRITE_MEMORY(key, 8, data_size | 0x80);

    key = UART_REG_DLL;
    WRITE_MEMORY(key, 8, 100);

    key = UART_REG_LCR;
    WRITE_MEMORY(key, 8, data_size);
}


#define UART_LSR_THR_EMPTY_5    0b00100000
#define UART_LSR_THR_EMPTY_6    0b01000000
#define UART_LSR_PARITY_ERR     0b00000100
#define UART_LSR_DATA_AVAILABLE 0b00000001

#define UART_LSR_ERR_MASK       0b01100101

#define UART_IIR_ERR_FLAG       0b00000001
#define UART_IIR_DATA_AVAILABLE 0b00000010
#define UART_IIR_THR_EMPTY      0b00000100

#define UART_IER_PARITY         0b00000100
#define UART_IER_THR_EMPTY      0b00000010
#define UART_IER_DATA_AVAILABLE 0b00000001

#define UART_IIR_ERR_MASK       0b00000111

static uint8_t convert_ier(uint8_t ier)
{
    uint8_t res = 0;

    if (ier & UART_IER_PARITY)
        res |= UART_IIR_ERR_FLAG;

    if (ier & UART_IER_DATA_AVAILABLE)
        res |= UART_IIR_DATA_AVAILABLE;

    if (ier & UART_IER_THR_EMPTY)
        res |= UART_IIR_THR_EMPTY;

    return res;
}

static bool iir_failed(uint8_t ier, uint8_t iir, uint8_t iir_must)
{
    return ((iir_must & convert_ier(ier)) != (iir & UART_IIR_ERR_MASK));
}

static bool lsr_failed(uint8_t lsr, uint8_t lsr_must)
{
    return (lsr_must != (lsr & UART_LSR_ERR_MASK));
}

static int wait_lsr(int lsr_must)
{
    int counter = 0;
    uint8_t read;

    read = READ_MEMORY(UART_REG_LSR, 8);
    while (lsr_failed(read, lsr_must) && counter < DEFAULT_COUNTER) {
        read = READ_MEMORY(UART_REG_LSR, 8);
        counter++;
    }

    if (counter == DEFAULT_COUNTER)
        return read & UART_LSR_ERR_MASK;

    return -1;
}

static int wait_iir(int iir_must, uint8_t ier)
{
    int counter = 0;
    uint8_t read;

    read = READ_MEMORY(UART_REG_IIR, 8);
    while (iir_failed(ier, read, iir_must) && counter < DEFAULT_COUNTER) {
        read = READ_MEMORY(UART_REG_IIR, 8);
        counter++;
    }

    if (counter == DEFAULT_COUNTER) {
        return read & UART_IIR_ERR_MASK;
    }

    return -1;
}

static int test_iir(const char *name, uint8_t ier)
{
    int key, ret_val, wait_t;
    uint8_t read, write = 0b11111111;

    key = UART_REG_IER;
    WRITE_MEMORY(key, 8, ier);

    key = UART_REG_FCR;
    WRITE_MEMORY(key, 8, 0b00000110);

    wait_t = wait_lsr(UART_LSR_THR_EMPTY_6 | UART_LSR_THR_EMPTY_5);
    if (wait_t != -1) {
        sc_printf("\t%s: test FCR: tx fifo not empty(LSR: %d, must: %d)\n", name,
                    wait_t, UART_LSR_THR_EMPTY_6 | UART_LSR_THR_EMPTY_5);
        ret_val = 1;
    }

    wait_t = wait_iir(UART_IIR_THR_EMPTY, ier);
    if (wait_t != -1) {
        sc_printf("\t%s: test iir FCR: tx fifo not empty(IIR: %d, must: %d)\n", name,
                    wait_t, UART_IIR_THR_EMPTY);
        ret_val = 1;
    }

    key = UART_REG_THR;
    WRITE_MEMORY(key, 8, write);

    wait_t = wait_lsr(0);
    if (wait_t != -1) {
        sc_printf("\t%s: test lsr: tx fifo empty, but THR was writed(LSR: %d, must: %d)\n", name,
                    wait_t, 0);
        ret_val = 1;
    }

    wait_t = wait_iir(0, ier);
    if (wait_t != -1) {
        sc_printf("\t%s: test iir: tx fifo empty, but THR was writed(IIR: %d, must: %d)\n", name,
                    wait_t, 0);
        ret_val = 1;
    }

    read = 0;
    while (!(read & UART_LSR_DATA_AVAILABLE) || (read & UART_LSR_PARITY_ERR)) {
        if (read & UART_LSR_PARITY_ERR) {
            read = READ_MEMORY(UART_REG_IIR, 8);
            if (iir_failed(ier, read, UART_IIR_ERR_FLAG)) {
                sc_printf("\t%s: test iir: parity flag was set in lsr, but iir err flag not set", name);
                ret_val = 1;
            }
        }
        read = READ_MEMORY(UART_REG_LSR, 8);
    }

    wait_t = wait_iir(UART_IIR_DATA_AVAILABLE, ier);
    if (wait_t != -1) {
        sc_printf("\t%s: test iir: tx data not available(IIR: %d, must: %d)\n", name,
                    wait_t, UART_IIR_DATA_AVAILABLE);
        ret_val = 1;
    }

    key = UART_REG_RBR;
    read = READ_MEMORY(key, 8);
    if (read != write) {
        sc_printf("\t%s: rx data: %d, tx data %d\n", read, write, name);
        ret_val = 1;
    }

    read = READ_MEMORY(UART_REG_LSR, 8);

    return ret_val;
}

int main(void)
{
    sc_printf("Test: XMIT TEST\n");

    int key;
    int ret_val = 0;

    init_apb(3);

    ret_val += test_iir("[NOIER]", 0);
    ret_val += test_iir("[ALL]", UART_IER_DATA_AVAILABLE |
                         UART_IER_THR_EMPTY | UART_IER_PARITY);
    ret_val += test_iir("[PARITY]", UART_IER_PARITY);
    ret_val += test_iir("[DATAAV]", UART_IER_DATA_AVAILABLE);
    ret_val += test_iir("[THREMP]", UART_IER_THR_EMPTY);
    ret_val += test_iir("[DA_PAR]", UART_IER_DATA_AVAILABLE | UART_IER_PARITY);
    ret_val += test_iir("[TH_PAR]", UART_IER_THR_EMPTY | UART_IER_PARITY);
    ret_val += test_iir("[DA_THR]", UART_IER_DATA_AVAILABLE | UART_IER_THR_EMPTY);



on_exit:
    if (ret_val)
        sc_printf("FAILED\n");
    else
        sc_printf("PASSED\n");

    return 0;
}
