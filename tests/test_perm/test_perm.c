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


#define UART_IRR_SUCCESS    0xC0

#define UART_LCB_LEN_BITS_8 2
#define UART_LCB_DLAB       0x80


static bool print(const char *test_name, const char *name, int key, bool correct, int val)
{
    uint8_t read1, read2, wr_val;

    read1 = READ_MEMORY(key, 8);
    if (val != -1)
        wr_val = val;
    else
        wr_val = read1+1 != 0 ? read1+1 : 1;

    WRITE_MEMORY(key, 8, wr_val);
    read2 = READ_MEMORY(key, 8);

    bool success = correct ? read1 != read2 : read1 == read2;

    if (!success)
    sc_printf("%s: %s \t\t\ttest: (%s), read1: %d, read2: %d, write: %d\n",
                test_name, name,
                success ? "correct" : "invalid", read1, read2, wr_val);

    return !success;
}


static int test_all(const char *name)
{
    int ret_val = 0, key;

    /* IER(rw) */
    key = UART_REG_IER;
    ret_val += print(name, "IER(0)", key, true, 1);
    ret_val += print(name, "IER(1)", key, true, 2);
    ret_val += print(name, "IER(2)", key, true, 4);


    /* FCR(w) */
    key = UART_REG_FCR;
    ret_val += print(name, "FCR(1)", key, false, 2);
    ret_val += print(name, "FCR(2)", key, false, 4);
    ret_val += print(name, "FCR(6:7)", key, false, 0);

    /* LSR(rw) */
    key = UART_REG_LSR;
    ret_val += print(name, "LSR(0)", key, false, 1);
    ret_val += print(name, "LSR(2)", key, false, 4);
    ret_val += print(name, "LSR(5)", key, false, 32);
    ret_val += print(name, "LSR(6)", key, false, 64);


    /* IIR(rw) */
    key = UART_REG_IIR;
    ret_val += print(name, "IIR", key, false, 1);
    /* RBR(r) */
    key = UART_REG_RBR;
    ret_val += print(name, "RBR", key, false, 1);
    /* THR(w) */
    key = UART_REG_THR;
    ret_val += print(name, "THR", key, false, 1);

    return ret_val;
}

#define NAME_AFTER  "\t[AFTERCONF]"
#define NAME_IN     "\t[INCONFIG]"

int main(void)
{
    sc_printf("Test: UART EXAMPLE\n");

    int ret_val = 0, key;

    /* LCR(rw) */
    key = UART_REG_LCR;
    ret_val += print(NAME_IN, "LCR", key, true, 3 | 8 | UART_LCB_DLAB);

    ret_val += test_all(NAME_IN);

    /* DLL(rw) */
    key = UART_REG_DLL;
    ret_val += print(NAME_IN, "DLL", key, true, 100);
    /* DLM(rw) */
    key = UART_REG_DLM;
    ret_val += print(NAME_IN, "DLM", key, true, 10);

    /* LCR(rw) */
    key = UART_REG_LCR;
    WRITE_MEMORY(key, 8, 3 | 8);

    /* DLL(rw) */
    key = UART_REG_DLL;
    ret_val += print(NAME_AFTER, "DLL", key, false, 2);
    /* DLM(rw) */
    key = UART_REG_DLM;             // ERROR
    ret_val += print(NAME_AFTER, "DLM", key, false, 2);

    ret_val += test_all(NAME_AFTER);

    if (ret_val)
        sc_printf("FAILED\n");
    else
        sc_printf("PASSED\n");

    return 0;
}
