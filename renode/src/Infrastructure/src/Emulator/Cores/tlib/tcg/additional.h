#ifndef ADDITIONAL_H
#define ADDITIONAL_H

#include <stdarg.h>
#include <stdlib.h>
#include <stdio.h>

void *TCG_malloc(size_t size);
void *TCG_realloc(void *ptr, size_t size);
void TCG_free(void *ptr);
void TCG_pstrcpy(char *buf, int buf_size, const char *str);
char *TCG_pstrcat(char *buf, int buf_size, const char *s);

// The highest value of NB_MMU_MODES used incremented by one.
#define MMU_MODES_MAX 16

extern unsigned int temp_buf_offset;
extern unsigned int tlb_table_n_0_addr_read[MMU_MODES_MAX];
extern unsigned int tlb_table_n_0_addr_write[MMU_MODES_MAX];
extern unsigned int tlb_table_n_0_addend[MMU_MODES_MAX];
extern unsigned int tlb_table_n_0[MMU_MODES_MAX];
extern unsigned int tlb_entry_addr_read;
extern unsigned int tlb_entry_addr_write;
extern unsigned int tlb_entry_addend;
extern unsigned int sizeof_CPUTLBEntry;

/* XXX: make safe guess about sizes */
#define MAX_OP_PER_INSTR      208

#if HOST_LONG_BITS == 32
#define MAX_OPC_PARAM_PER_ARG 2
#else
#define MAX_OPC_PARAM_PER_ARG 1
#endif
#define MAX_OPC_PARAM_IARGS   4
#define MAX_OPC_PARAM_OARGS   1
#define MAX_OPC_PARAM_ARGS    (MAX_OPC_PARAM_IARGS + MAX_OPC_PARAM_OARGS)

/* A Call op needs up to 4 + 2N parameters on 32-bit archs,
 * and up to 4 + N parameters on 64-bit archs
 * (N = number of input arguments + output arguments).  */
#define MAX_OPC_PARAM         (4 + (MAX_OPC_PARAM_PER_ARG * MAX_OPC_PARAM_ARGS))
#define OPC_BUF_SIZE          640
#define OPC_MAX_SIZE          (OPC_BUF_SIZE - MAX_OP_PER_INSTR)

/* Maximum size a TCG op can expand to.  This is complicated because a
   single op may require several host instructions and register reloads.
   For now take a wild guess at 192 bytes, which should allow at least
   a couple of fixup instructions per argument.  */
#define TCG_MAX_OP_SIZE       192

/* The maximum size of generated code within a block. */
#define TCG_MAX_CODE_SIZE     (TCG_MAX_OP_SIZE * OPC_BUF_SIZE)

/* The maximum size of PC search data within a block. */
#define TCG_MAX_SEARCH_SIZE   (TCG_MAX_CODE_SIZE * 0.3)

#define OPPARAM_BUF_SIZE      (OPC_BUF_SIZE * MAX_OPC_PARAM)

void tlib_abort(char *msg);

static inline void tcg_abortf(char *fmt, ...)
{
    char result[1024];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(result, 1024, fmt, ap);
    tlib_abort(result);
    va_end(ap);
    __builtin_unreachable();
}

#endif
