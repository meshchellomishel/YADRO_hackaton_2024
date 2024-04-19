#include "irq.h"
#include "sc_print.h"

void __attribute__((weak, interrupt, aligned(16))) MachineExternalInterrupt (void)
{
    sc_printf("Default handler detected\n");
}

void set_isr_routine()
{
    const int mie_val = read_csr(mie);
    write_csr(mie,0);

    write_csr(mtvec,(uintptr_t)(MachineExternalInterrupt));

    const int MSTATUS = read_csr(mstatus);
    write_csr(mstatus, MSTATUS |
              1ull<<1 | // SIE
              1ull<<3); // MIE

    // enable interrupts
    write_csr(mie,mie_val |
              (1ull<<1)|
              (1ull<<3)|
              (1ull<<11)|
              (1ull<<12));
}