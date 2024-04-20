#include <inttypes.h>
#include "mem.h"
#include "sc_print.h"


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

int main()
{
  sc_printf("Test: UART registers states\n");
  
  int ret_val = 0;
  // starting UART registers states


  // Register map
  uint8_t rbr_default_value = READ_MEMORY(UART_REG_RBR, 8);
  sc_printf("rbr_default_value: %x\n", rbr_default_value);

  uint8_t thr_default_value = READ_MEMORY(UART_REG_THR, 8);
  sc_printf("thr_default_value: %x\n", thr_default_value);

  uint8_t dll_default_value = READ_MEMORY(UART_REG_DLL, 8);
  sc_printf("dll_default_value: %x\n", dll_default_value);

  uint8_t ier_default_value = READ_MEMORY(UART_REG_IER, 8);
  sc_printf("ier_default_value: %x\n", ier_default_value);

  uint8_t dlm_default_value = READ_MEMORY(UART_REG_DLM, 8);
  sc_printf("dlm_default_value: %x\n", dlm_default_value);

  uint8_t iir_default_value = READ_MEMORY(UART_REG_IIR, 8);
  sc_printf("iir_default_value: %x\n", iir_default_value);

  uint8_t fcr_default_value = READ_MEMORY(UART_REG_FCR, 8);
  sc_printf("fcr_default_value: %x\n", fcr_default_value);

  uint8_t lcr_default_value = READ_MEMORY(UART_REG_LCR, 8);
  sc_printf("lcr_default_value: %x\n", lcr_default_value);

  uint8_t lsr_default_value = READ_MEMORY(UART_REG_LSR, 8);
  sc_printf("lsr_default_value: %x\n", lsr_default_value);

  // Receiver Buffer Register

  // Transmitter Holding Register

  // Divisor Latch, LSB

  // Divisor Latch, MSB

  // Interrupt Enable Register

  // Interrupt Identification Register

  // FIFO Control Register

  // Line Control Register

  // Line Status Register




  ret_val = rbr_default_value != 0x0 || 
            thr_default_value != 0x0 || 
            dll_default_value != 0x0 || 
            ier_default_value != 0x0 || 
            dlm_default_value != 0x0 || 
            iir_default_value != 0xC0 || 
            //fcr_default_value != 0x0 || 
            lcr_default_value != 0x0 ||
            lsr_default_value != 0x60;

  sc_printf("ret_val: %x\n", ret_val);



  // template dont touch
  if(ret_val == 0){
    sc_printf("PASSED\n");
  } else {
    sc_printf("FAILED\n");
  }
  return ret_val;
}
