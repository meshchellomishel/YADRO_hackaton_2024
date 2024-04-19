#include "def-helper.h"

DEF_HELPER_1(prepare_block_for_execution, i32, ptr)
DEF_HELPER_0(block_begin_event, i32)
DEF_HELPER_2(block_finished_event, void, tl, i32)
DEF_HELPER_2(log, void, i32, i32)
DEF_HELPER_1(var_log, void, tl)
DEF_HELPER_0(abort, void)
DEF_HELPER_2(announce_stack_change, void, tl, i32)
DEF_HELPER_1(on_interrupt_end_event, void, i64)

DEF_HELPER_4(mark_tbs_as_dirty, void, env, tl, i32, i32)

DEF_HELPER_1(count_opcode_inner, void, i32)
DEF_HELPER_1(tlb_flush, void, env)

DEF_HELPER_1(acquire_global_memory_lock, void, env)
DEF_HELPER_1(release_global_memory_lock, void, env)
DEF_HELPER_2(reserve_address, void, env, uintptr)
DEF_HELPER_2(check_address_reservation, tl, env, uintptr)

#include "def-helper.h"
