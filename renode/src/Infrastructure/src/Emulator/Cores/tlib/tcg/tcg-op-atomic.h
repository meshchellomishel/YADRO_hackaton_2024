/*
 * Tiny Code Generator for QEMU
 *
 * Copyright (c) 2008 Fabrice Bellard
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
#ifndef __TCG_OP_ATOMIC_H__
#define __TCG_OP_ATOMIC_H__

#include "tb-helper.h"

#include "tcg.h"
#include "tcg-op.h"

// These are based on TCG's 'nonatomic' functions.
// tlib global memory locking makes them atomic.

static inline void tcg_gen_atomic_cmpxchg_i32(TCGv_i32 retv, TCGv addr, TCGv_i32 cmpv,
                                              TCGv_i32 newv, TCGArg idx, TCGMemOp memop)
{
    memop = tcg_canonicalize_memop(memop, 0, 0);

    TCGv_i32 t1 = tcg_temp_new_i32();
    TCGv_i32 t2 = tcg_temp_new_i32();

    tcg_gen_ext_i32(t2, cmpv, memop & MO_SIZE);

    gen_helper_acquire_global_memory_lock(cpu_env);
    tcg_gen_qemu_ld_i32(t1, addr, idx, memop & ~MO_SIGN);
    tcg_gen_movcond_i32(TCG_COND_EQ, t2, t1, t2, newv, t1);
    tcg_gen_qemu_st_i32(t2, addr, idx, memop);
    gen_helper_release_global_memory_lock(cpu_env);

    tcg_temp_free_i32(t2);

    if (memop & MO_SIGN) {
        tcg_gen_ext_i32(retv, t1, memop);
    } else {
        tcg_gen_mov_i32(retv, t1);
    }
    tcg_temp_free_i32(t1);
}

static inline void tcg_gen_atomic_cmpxchg_i64(TCGv_i64 retv, TCGv addr, TCGv_i64 cmpv,
                                              TCGv_i64 newv, TCGArg idx, TCGMemOp memop)
{
    memop = tcg_canonicalize_memop(memop, 1, 0);

    TCGv_i64 t1 = tcg_temp_new_i64();
    TCGv_i64 t2 = tcg_temp_new_i64();

    tcg_gen_ext_i64(t2, cmpv, memop & MO_SIZE);

    gen_helper_acquire_global_memory_lock(cpu_env);
    tcg_gen_qemu_ld_i64(t1, addr, idx, memop & ~MO_SIGN);
    tcg_gen_movcond_i64(TCG_COND_EQ, t2, t1, t2, newv, t1);
    tcg_gen_qemu_st_i64(t2, addr, idx, memop);
    gen_helper_release_global_memory_lock(cpu_env);

    tcg_temp_free_i64(t2);

    if (memop & MO_SIGN) {
        tcg_gen_ext_i64(retv, t1, memop);
    } else {
        tcg_gen_mov_i64(retv, t1);
    }
    tcg_temp_free_i64(t1);
}

// 'new_val' controls whether a value before or after the operation should be returned.
static void do_atomic_op_i32(TCGv_i32 ret, TCGv addr, TCGv_i32 val,
                             TCGArg idx, TCGMemOp memop, bool new_val,
                             void (*gen)(TCGv_i32, TCGv_i32, TCGv_i32))
{
    TCGv_i32 t1 = tcg_temp_new_i32();
    TCGv_i32 t2 = tcg_temp_new_i32();

    memop = tcg_canonicalize_memop(memop, 0, 0);

    gen_helper_acquire_global_memory_lock(cpu_env);
    tcg_gen_qemu_ld_i32(t1, addr, idx, memop);
    tcg_gen_ext_i32(t2, val, memop);
    gen(t2, t1, t2);
    tcg_gen_qemu_st_i32(t2, addr, idx, memop);
    gen_helper_release_global_memory_lock(cpu_env);

    tcg_gen_ext_i32(ret, (new_val ? t2 : t1), memop);
    tcg_temp_free_i32(t1);
    tcg_temp_free_i32(t2);
}

// 'new_val' controls whether a value before or after the operation should be returned.
static void do_atomic_op_i64(TCGv_i64 ret, TCGv addr, TCGv_i64 val,
                             TCGArg idx, TCGMemOp memop, bool new_val,
                             void (*gen)(TCGv_i64, TCGv_i64, TCGv_i64))
{
    TCGv_i64 t1 = tcg_temp_new_i64();
    TCGv_i64 t2 = tcg_temp_new_i64();

    memop = tcg_canonicalize_memop(memop, 1, 0);

    gen_helper_acquire_global_memory_lock(cpu_env);
    tcg_gen_qemu_ld_i64(t1, addr, idx, memop);
    tcg_gen_ext_i64(t2, val, memop);
    gen(t2, t1, t2);
    tcg_gen_qemu_st_i64(t2, addr, idx, memop);
    gen_helper_release_global_memory_lock(cpu_env);

    tcg_gen_ext_i64(ret, (new_val ? t2 : t1), memop);
    tcg_temp_free_i64(t1);
    tcg_temp_free_i64(t2);
}

#define GEN_ATOMIC_HELPER(NAME, OP, NEW)                                   \
static inline void tcg_gen_atomic_##NAME##_i32                             \
    (TCGv_i32 ret, TCGv addr, TCGv_i32 val, TCGArg idx, TCGMemOp memop)    \
{                                                                          \
    do_atomic_op_i32(ret, addr, val, idx, memop, NEW, tcg_gen_##OP##_i32); \
}                                                                          \
static inline void tcg_gen_atomic_##NAME##_i64                             \
    (TCGv_i64 ret, TCGv addr, TCGv_i64 val, TCGArg idx, TCGMemOp memop)    \
{                                                                          \
    do_atomic_op_i64(ret, addr, val, idx, memop, NEW, tcg_gen_##OP##_i64); \
}

GEN_ATOMIC_HELPER(fetch_add, add, 0)
GEN_ATOMIC_HELPER(fetch_and, and, 0)
GEN_ATOMIC_HELPER(fetch_or, or, 0)
GEN_ATOMIC_HELPER(fetch_xor, xor, 0)
GEN_ATOMIC_HELPER(fetch_smin, smin, 0)
GEN_ATOMIC_HELPER(fetch_umin, umin, 0)
GEN_ATOMIC_HELPER(fetch_smax, smax, 0)
GEN_ATOMIC_HELPER(fetch_umax, umax, 0)

static void tcg_gen_mov2_i32(TCGv_i32 r, TCGv_i32 a, TCGv_i32 b)
{
    tcg_gen_mov_i32(r, b);
}

static void tcg_gen_mov2_i64(TCGv_i64 r, TCGv_i64 a, TCGv_i64 b)
{
    tcg_gen_mov_i64(r, b);
}

GEN_ATOMIC_HELPER(xchg, mov2, 0)

#endif
