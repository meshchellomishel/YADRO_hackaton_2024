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


static bool print(const char *name, int key, bool correct, int val)
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
    sc_printf("%s \t\t\ttest: (%s), read1: %d, read2: %d, write: %d\n", name,
                success ? "correct" : "invalid", read1, read2, wr_val);

    return !success;
}


int main()
{
  sc_printf("Test: UART loopback\n");

  int ret_val = 0, key;

  /* LCR(rw) */
  key = UART_REG_LCR;
  ret_val += print("LCR", key, true, 3 | 8 | UART_LCB_DLAB);

  /* DLL(rw) */
  key = UART_REG_DLL;
  ret_val += print("DLL", key, true, 100);
  /* DLM(rw) */
  key = UART_REG_DLM;
  ret_val += print("DLM", key, true, 10);

  /* LCR(rw) */
  key = UART_REG_LCR;
  WRITE_MEMORY(key, 8, 3 | 8);

  /// Отправка данных

  key = UART_REG_THR;
  WRITE_MEMORY(key, 8, 5);

  key = UART_REG_LSR;
  uint8_t read1 = READ_MEMORY(key, 8);

  sc_printf("read_lsr_send: %x\n", read1);
  sc_printf("Transmitter Empty flag: %x\n", read1&0b01000000);
  sc_printf("Transmitter FIFO empty flag: %x\n", read1&0b00100000);

  if((read1&0b01000000)&&(read1&0b00100000)){
    bool ignored = true;
    sc_printf("ignored\n");
  }
  else{
    sc_printf("not ignored\n");
    // Получение данных
    key = UART_REG_LSR;
    uint8_t read1 = READ_MEMORY(key, 8);
    sc_printf("read_lsr_recv: %x\n", read1);


    sc_printf("lsr_masked: %x\n", read1&0b00000001);
    while(read1&0b00000001==1){
      uint8_t read1 = READ_MEMORY(key, 8);
    }
    sc_printf("escaped\n");

    key = UART_REG_RBR;
    uint8_t read_data = READ_MEMORY(key, 8);
    sc_printf("read_rbr: %x\n", read_data);

    if (read_data == 5){
      ret_val = 0;
      sc_printf("read the same as sended\n");
    }
    else {ret_val = 1;sc_printf("wrong\n");}
  }

  sc_printf("ret_val: %x\n", ret_val);
  // template dont touch
  if(ret_val == 0){
    sc_printf("PASSED\n");
  } else {
    sc_printf("FAILED\n");
  }
  return ret_val;
}
