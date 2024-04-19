#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "cpu.h"
#include "helper.h"
#include "../arm64/system_registers_common.h"

void HELPER(set_cp_reg)(CPUState * env, void *rip, uint32_t value)
{
    const ARMCPRegInfo *ri = rip;

    if (ri->type & ARM_CP_IO) {
        // Use mutex if executed in parallel.
        ri->writefn(env, ri, value);
    } else {
        ri->writefn(env, ri, value);
    }
}

uint32_t HELPER(get_cp_reg)(CPUState * env, void *rip)
{
    const ARMCPRegInfo *ri = rip;
    uint32_t res;

    if (ri->type & ARM_CP_IO) {
        // Use mutex if executed in parallel.
        res = ri->readfn(env, ri);
    } else {
        res = ri->readfn(env, ri);
    }

    return res;
}

void HELPER(set_cp_reg64)(CPUState * env, void *rip, uint64_t value)
{
    const ARMCPRegInfo *ri = rip;

    if (ri->type & ARM_CP_IO) {
        // Use mutex if executed in parallel.
        ri->writefn(env, ri, value);
    } else {
        ri->writefn(env, ri, value);
    }
}

uint64_t HELPER(get_cp_reg64)(CPUState * env, void *rip)
{
    const ARMCPRegInfo *ri = rip;
    uint64_t res;

    if (ri->type & ARM_CP_IO) {
        // Use mutex if executed in parallel.
        res = ri->readfn(env, ri);
    } else {
        res = ri->readfn(env, ri);
    }

    return res;
}

ARMCPRegInfo general_coprocessor_registers[] = {
    // Empty for now
};

// The keys are dynamically allocated so let's make TTable free them when removing the entry.
static void entry_remove_callback(TTable_entry *entry)
{
    tlib_free(entry->key);
}

void cp_reg_add(CPUState *env, ARMCPRegInfo *reg_info)
{
    const bool ns = true; // TODO: Handle secure state banking in a correct way, when we add Secure Mode to this lib
    const bool is64 = reg_info->type & ARM_CP_64BIT;

    uint32_t *key = tlib_malloc(sizeof(uint32_t));
    *key = ENCODE_CP_REG(reg_info->cp, is64, ns, reg_info->crn, reg_info->crm, reg_info->op1, reg_info->op2);

    cp_reg_add_with_key(env, env->cp_regs, key, reg_info);
}

void system_instructions_and_registers_reset(CPUState *env)
{
    TTable *cp_regs = env->cp_regs;

    int i;
    for (i = 0; i < cp_regs->count; i++) {
        ARMCPRegInfo *ri = cp_regs->entries[i].value;

        // Nothing to be done for these because:
        // * all the backing fields except the 'arm_core_config' ones are always reset to zero,
        // * CONSTs have no backing fields and 'resetvalue' is always used when they're read.
        if ((ri->resetvalue == 0) || (ri->type & ARM_CP_CONST)) {
            continue;
        }

        uint32_t width = ri->cp == (ri->type & ARM_CP_64BIT) ? 64 : 32;
        uint64_t value = width == 64 ? ri->resetvalue : ri->resetvalue & UINT32_MAX;

        tlib_printf(LOG_LEVEL_NOISY, "Resetting value for '%s': 0x%" PRIx64, ri->name, value);
        if (ri->fieldoffset) {
            memcpy((void *)env + ri->fieldoffset, &value, (size_t)(width / 8));
        } else if (ri->writefn) {
            ri->writefn(env, ri, value);
        } else {
            // Shouldn't happen so let's make sure it doesn't.
            tlib_assert_not_reached();
        }
    }
}

void system_instructions_and_registers_init(CPUState *env)
{
    uint32_t ttable_size = ARM_CP_ARRAY_COUNT(general_coprocessor_registers);
    env->cp_regs = ttable_create(ttable_size, entry_remove_callback, ttable_compare_key_uint32);

    cp_regs_add(env, general_coprocessor_registers, ARM_CP_ARRAY_COUNT(general_coprocessor_registers));
}
