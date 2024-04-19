/*
 * Copyright (c) Antmicro
 *
 * SPDX-License-Identifier: Apache-2.0
 */

#ifndef SYSTEM_REGISTERS_H_
#define SYSTEM_REGISTERS_H_

#include <stdint.h>
#include "cpu.h"
#include "system_registers_common.h"

// This doesn't seem to be of any real use. It's passed to ENCODE_AA64_CP_REG in 'translate-a64.c:handle_sys'
// as 'cp' so it seems to be a coprocessor ID. However, there's no information about coprocessor for AArch64
// registers and instructions in the manual. There's only an information that some instruction type encodings
// are "equivalent to the registers in the AArch32 (coproc == XX) encoding space" (XX=15 for op0=(1 or 3) and
// XX=14 for op0=2). Perhaps let's use 16 to distinguish it from CP15 used for AArch32 and ARMv7 encodings.
#define CP_REG_ARM64_SYSREG_CP        16

// From C5.1.2
#define CP_REG_ARM64_SYSREG_OP2_SHIFT 0
#define CP_REG_ARM64_SYSREG_CRM_SHIFT (CP_REG_ARM64_SYSREG_OP2_SHIFT + 3)
#define CP_REG_ARM64_SYSREG_CRN_SHIFT (CP_REG_ARM64_SYSREG_CRM_SHIFT + 4)
#define CP_REG_ARM64_SYSREG_OP1_SHIFT (CP_REG_ARM64_SYSREG_CRN_SHIFT + 4)
#define CP_REG_ARM64_SYSREG_OP0_SHIFT (CP_REG_ARM64_SYSREG_OP1_SHIFT + 3)
// op0 is a 2-bit field
#define CP_REG_ARM_COPROC_SHIFT       (CP_REG_ARM64_SYSREG_OP0_SHIFT + 2)

// ARM Architecture Reference Manual ARMv7A and ARMv7-R (A8.6.92)
#define CP_REG_ARM32_32BIT_SYSREG_CRM_SHIFT 0
#define CP_REG_ARM32_32BIT_SYSREG_OP2_SHIFT (CP_REG_ARM32_32BIT_SYSREG_CRM_SHIFT + 5)
#define CP_REG_ARM32_32BIT_SYSREG_CRN_SHIFT (CP_REG_ARM32_32BIT_SYSREG_OP2_SHIFT + 11)
#define CP_REG_ARM32_32BIT_SYSREG_OP1_SHIFT (CP_REG_ARM64_SYSREG_CRN_SHIFT + 5)

// ARM Architecture Reference Manual ARMv7A and ARMv7-R (A8.6.93)
#define CP_REG_ARM32_64BIT_SYSREG_CRM_SHIFT 0
#define CP_REG_ARM32_64BIT_SYSREG_OP1_SHIFT (CP_REG_ARM32_64BIT_SYSREG_CRM_SHIFT + 4)

void system_instructions_and_registers_reset(CPUState *env);
void system_instructions_and_registers_init(CPUState *env, uint32_t cpu_model_id);

/* Functions for accessing system registers by their names. */

static inline uint64_t *sysreg_field_ptr(CPUState *env, const ARMCPRegInfo *ri)
{
    // Fieldoffset is in bytes hence 'env' is cast to 'uint8_t' before the addition.
    return (uint64_t *)(((uint8_t *)env) + ri->fieldoffset);
}

static bool ttable_compare_sysreg_name(TTable_entry entry, const void *sysreg_name)
{
    ARMCPRegInfo *ri = (ARMCPRegInfo *)entry.value;
    return strcasecmp(ri->name, sysreg_name) == 0;
}

static const ARMCPRegInfo *sysreg_find_by_name(CPUState *env, const char *name)
{
    const char *lookup_name = name;
    if (strcasecmp(name, "DBGDTRRX_EL0") == 0 || strcasecmp(name, "DBGDTRTX_EL0") == 0) {
        lookup_name = "DBGDTR_RX_TX_EL0";
    } else if (strcasecmp(lookup_name, "ICV_AP0R_0") == 0) {
        lookup_name = "ICC_AP0R_0";
    } else if (strcasecmp(lookup_name, "ICV_AP0R_1") == 0) {
        lookup_name = "ICC_AP0R_1";
    } else if (strcasecmp(lookup_name, "ICV_AP0R_2") == 0) {
        lookup_name = "ICC_AP0R_2";
    } else if (strcasecmp(lookup_name, "ICV_AP0R_3") == 0) {
        lookup_name = "ICC_AP0R_3";
    } else if (strcasecmp(lookup_name, "ICV_AP1R_0") == 0) {
        lookup_name = "ICC_AP1R_0";
    } else if (strcasecmp(lookup_name, "ICV_AP1R_1") == 0) {
        lookup_name = "ICC_AP1R_1";
    } else if (strcasecmp(lookup_name, "ICV_AP1R_2") == 0) {
        lookup_name = "ICC_AP1R_2";
    } else if (strcasecmp(lookup_name, "ICV_AP1R_3") == 0) {
        lookup_name = "ICC_AP1R_3";
    } else if (strcasecmp(lookup_name, "ICV_BPR0") == 0) {
        lookup_name = "ICC_BPR0";
    } else if (strcasecmp(lookup_name, "ICV_BPR1") == 0) {
        lookup_name = "ICC_BPR1";
    } else if (strcasecmp(lookup_name, "ICV_CTLR") == 0) {
        lookup_name = "ICC_CTLR";
    } else if (strcasecmp(lookup_name, "ICV_DIR") == 0) {
        lookup_name = "ICC_DIR";
    } else if (strcasecmp(lookup_name, "ICV_EOIR0") == 0) {
        lookup_name = "ICC_EOIR0";
    } else if (strcasecmp(lookup_name, "ICV_EOIR1") == 0) {
        lookup_name = "ICC_EOIR1";
    } else if (strcasecmp(lookup_name, "ICV_HPPIR0") == 0) {
        lookup_name = "ICC_HPPIR0";
    } else if (strcasecmp(lookup_name, "ICV_HPPIR1") == 0) {
        lookup_name = "ICC_HPPIR1";
    } else if (strcasecmp(lookup_name, "ICV_IAR0") == 0) {
        lookup_name = "ICC_IAR0";
    } else if (strcasecmp(lookup_name, "ICV_IAR1") == 0) {
        lookup_name = "ICC_IAR1";
    } else if (strcasecmp(lookup_name, "ICV_IGRPEN0") == 0) {
        lookup_name = "ICC_IGRPEN0";
    } else if (strcasecmp(lookup_name, "ICV_IGRPEN1") == 0) {
        lookup_name = "ICC_IGRPEN1";
    } else if (strcasecmp(lookup_name, "ICV_PMR") == 0) {
        lookup_name = "ICC_PMR";
    } else if (strcasecmp(lookup_name, "ICV_RPR") == 0) {
        lookup_name = "ICC_RPR";
    }

    TTable_entry *entry = ttable_lookup_custom(env->arm_core_config->cp_regs, ttable_compare_sysreg_name, lookup_name);
    if (entry != NULL) {
        return (ARMCPRegInfo *)entry->value;
    }
    return NULL;
}

static void sysreg_access_nzcv()
{
    // For now let's just inform that it can be handled through PSTATE.
    tlib_printf(LOG_LEVEL_INFO, "Use '<cpu_name> PSTATE' to access NZCV.");
}

static inline uint64_t sysreg_get_by_name(CPUState *env, const char *name)
{
    const ARMCPRegInfo *ri = sysreg_find_by_name(env, name);
    if (ri == NULL) {
        tlib_printf(LOG_LEVEL_WARNING, "Reading from system register failure. No such register: %s", name);
        return 0x0;
    }

    if (ri->type & ARM_CP_NZCV) {
        sysreg_access_nzcv();
        return 0x0;
    }

    if (ri->type & ARM_CP_CONST) {
        return ri->resetvalue;
    } else if (ri->readfn) {
        return ri->readfn(env, ri);
    } else if (ri->fieldoffset != 0) {
        if (ri->type & ARM_CP_64BIT) {
            return *sysreg_field_ptr(env, ri);
        } else {
            return *(uint32_t *)sysreg_field_ptr(env, ri);
        }
    } else {
        log_unhandled_sysreg_read(ri->name);
        return 0x0;
    }
}

static inline void sysreg_set_by_name(CPUState *env, const char *name, uint64_t value)
{
    const ARMCPRegInfo *ri = sysreg_find_by_name(env, name);
    if (ri == NULL) {
        tlib_printf(LOG_LEVEL_WARNING, "Writing to system register failure. No such register: %s", name);
        return;
    }

    if (ri->type & ARM_CP_NZCV) {
        sysreg_access_nzcv();
    }

    if (ri->writefn) {
        ri->writefn(env, ri, value);
    } else if (ri->fieldoffset != 0) {
        if (ri->type & ARM_CP_64BIT) {
            *sysreg_field_ptr(env, ri) = value;
        } else {
            *(uint32_t *)sysreg_field_ptr(env, ri) = (uint32_t)value;
        }
    } else {
        log_unhandled_sysreg_write(ri->name);
        return;
    }
}

#endif // SYSTEM_REGISTERS_H_
