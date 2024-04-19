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


static bool print(const char *name, bool correct)
{
    int iir = READ_MEMORY(UART_REG_IIR, 8);
    bool success = correct ? iir == UART_IRR_SUCCESS : iir != UART_IRR_SUCCESS;

    if (!success)
        sc_printf("%s test: %x(%s)\n", name, iir, success ? "correct" : "invalid");
    return !success;
}


int main(void)
{
    sc_printf("Test: UART EXAMPLE\n");

    int ret_val = 0, key;
    bool success = false;
    uint8_t iir;

    WRITE_MEMORY(UART_REG_IER, 8, 5);

    /* RBR(r) */
    key = UART_REG_RBR;

    uint8_t rbr_read = READ_MEMORY(key, 8);
    ret_val += print("RBR read", true);

    WRITE_MEMORY(key, 8, 1);
    ret_val += print("RBR write", false);

    /* THR(w) */
    key = UART_REG_THR;

    WRITE_MEMORY(key, 8, 1);       // ERROR
    ret_val += print("THR write", true);

    uint8_t thr_read = READ_MEMORY(key, 8);
    ret_val += print("THR read", false);

    /* DLL(rw) */
    key = UART_REG_DLL;

    WRITE_MEMORY(key, 8, 1);       // ERROR
    ret_val += print("DLL write", true);

    uint8_t dll_read = READ_MEMORY(key, 8);
    ret_val += print("DLL read", true);

    /* IER(rw) */
    key = UART_REG_IER;

    WRITE_MEMORY(key, 8, 1);
    ret_val += print("IER write", true);

    uint8_t ier_read = READ_MEMORY(key, 8);
    ret_val += print("IER read", true);

    /* DLM(rw) */
    key = UART_REG_DLM;

    WRITE_MEMORY(key, 8, 1);
    ret_val += print("DLM write", true);

    uint8_t dlm_read = READ_MEMORY(key, 8);
    ret_val += print("DLM read", true);

    /* IIR(r) */
    key = UART_REG_IIR;

    WRITE_MEMORY(key, 8, 1);                    // ERROR
    ret_val += print("IIR write", false);

    uint8_t iir_read = READ_MEMORY(key, 8);
    ret_val += print("IIR read", true);

    /* FCR(w) */
    key = UART_REG_FCR;

    WRITE_MEMORY(key, 8, 1);
    ret_val += print("FCR write", true);

    uint8_t fcr_read = READ_MEMORY(key, 8);     // ERROR
    ret_val += print("FCR read", false);

    /* LCR(rw) */
    key = UART_REG_LCR;

    WRITE_MEMORY(key, 8, 1);
    ret_val += print("LCR write", true);

    uint8_t lcr_read = READ_MEMORY(key, 8);
    ret_val += print("LCR read", true);

    /* LSR(r) */
    key = UART_REG_LSR;

    WRITE_MEMORY(key, 8, 1);        // ERROR
    ret_val += print("LSR write", false);

    uint8_t lsr_read = READ_MEMORY(key, 8);
    ret_val += print("LSR read", true);



    if (ret_val)
        sc_printf("FAILED\n");
    else
        sc_printf("PASSED\n");

    return 0;
}
