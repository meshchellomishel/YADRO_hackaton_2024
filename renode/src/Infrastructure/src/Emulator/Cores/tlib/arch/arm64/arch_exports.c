/*
 * Copyright (c) Antmicro
 *
 * SPDX-License-Identifier: Apache-2.0
 */

#include <stdint.h>
#include "cpu.h"
#include "system_registers.h"
#include "../../unwind.h"

uint32_t tlib_check_system_register_access(const char *name, bool is_write)
{
    enum {
        REGISTER_NOT_FOUND = 1,
        ACCESSOR_NOT_FOUND = 2,
        ACCESS_VALID       = 3,
    };

    const ARMCPRegInfo *ri = sysreg_find_by_name(env, name);
    if (ri == NULL) {
        return REGISTER_NOT_FOUND;
    }

    if (ri->fieldoffset) {
        return ACCESS_VALID;
    }

    if (is_write) {
        bool write_possible = ri->writefn != NULL;
        return write_possible ? ACCESS_VALID : ACCESSOR_NOT_FOUND;
    } else {
        bool read_possible = (ri->readfn != NULL) || (ri->type & ARM_CP_CONST);
        return read_possible ? ACCESS_VALID : ACCESSOR_NOT_FOUND;
    }
}
EXC_INT_2(uint32_t, tlib_check_system_register_access, const char *, name, bool, is_write)

uint64_t tlib_get_system_register(const char *name)
{
    return sysreg_get_by_name(cpu, name);
}
EXC_INT_1(uint64_t, tlib_get_system_register, const char *, name)

uint32_t tlib_has_el3()
{
    return cpu->arm_core_config->has_el3;
}
EXC_INT_0(uint32_t, tlib_has_el3)

uint32_t tlib_set_available_els(bool el2_enabled, bool el3_enabled)
{
    enum {
        SIMULATION_ALREADY_STARTED = 1,
        EL2_OR_EL3_ENABLING_FAILED = 2,
        SUCCESS                    = 3,
    };

    if (cpu->instructions_count_total_value != 0) {
        return SIMULATION_ALREADY_STARTED;
    }

    // Only allow enabling ELs for the cores that really support them.
    bool el2_enable_error = !cpu->arm_core_config->has_el2 && el2_enabled;
    bool el3_enable_error = !cpu->arm_core_config->has_el3 && el3_enabled;
    if (el2_enable_error || el3_enable_error) {
        return EL2_OR_EL3_ENABLING_FAILED;
    }

    set_el_features(cpu, el2_enabled, el3_enabled);

    if (is_a64(env)) {
        // Reset the Exception Level a CPU starts in.
        uint32_t reset_el = arm_highest_el(cpu);
        cpu->pstate = deposit32(cpu->pstate, 2, 2, reset_el);

        tlib_on_execution_mode_changed(reset_el, arm_is_secure(env));
    } else {
        uint32_t reset_mode = arm_get_highest_cpu_mode(env);
        cpsr_write(env, reset_mode, CPSR_M, CPSRWriteRaw);
    }

    arm_rebuild_hflags(cpu);

    return SUCCESS;
}
EXC_INT_2(uint32_t, tlib_set_available_els, bool, el2_enabled, bool, el3_enabled)

void tlib_set_current_el(uint32_t el)
{
    pstate_set_el(cpu, el);
}
EXC_VOID_1(tlib_set_current_el, uint32_t, el)

void tlib_set_system_register(const char *name, uint64_t value)
{
    sysreg_set_by_name(cpu, name, value);
}
EXC_VOID_2(tlib_set_system_register, const char *, name, uint64_t, value)

void tlib_set_mpu_regions_count(uint32_t count)
{
    if (count > MAX_MPU_REGIONS) {
        tlib_abortf("Unable to set MPU regions count to %d. Maximum value for this core is %d", count, MAX_MPU_REGIONS);
    }
    
    set_pmsav8_region_count(cpu, count);
}
EXC_VOID_1(tlib_set_mpu_regions_count, uint32_t, count)
