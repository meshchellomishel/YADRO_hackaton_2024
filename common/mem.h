#ifndef MEM_H
#define MEM_H
#include <stdint.h> 

#define READ_MEMORY(addr, size) \
    ({ \
        const volatile uint ## size ## _t *const __ptr = (const volatile uint ## size ## _t *)(addr); \
        const uint ## size ## _t __value = *__ptr; \
        __value; \
    })

#define WRITE_MEMORY(addr, size, value) \
    ({ \
        volatile uint ## size ## _t *const __ptr = (volatile uint ## size ## _t *)(addr); \
        uint ## size ## _t __value = (uint ## size ## _t)(value); \
        *__ptr = __value;\
    })

#endif  