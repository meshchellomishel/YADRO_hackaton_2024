/*
 *  ARM interface functions.
 *
 *  Copyright (c) Antmicro
 *  Copyright (c) Realtime Embedded
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, see <http://www.gnu.org/licenses/>.
 */
#include <stdint.h>
#include "cpu.h"
#include "../../unwind.h"
#include "bit_helper.h"
#include "host-utils.h"

uint32_t tlib_get_cpu_id()
{
    return cpu->cp15.c0_cpuid;
}

EXC_INT_0(uint32_t, tlib_get_cpu_id)

uint32_t tlib_get_it_state()
{
    return cpu->condexec_bits;
}

EXC_INT_0(uint32_t, tlib_get_it_state)

uint32_t tlib_evaluate_condition_code(uint32_t condition)
{
    uint8_t ZF = (env->ZF == 0);
    uint8_t NF = (env->NF & 0x80000000) > 0;
    uint8_t CF = (env->CF == 1);
    uint8_t VF = (env->VF & 0x80000000) > 0;
    switch (condition) {
    case 0b0000:        //EQ
        return ZF == 1;
    case 0b0001:        //NE
        return ZF == 0;
    case 0b0010:        //CS
        return CF == 1;
    case 0b0011:        //CC
        return CF == 0;
    case 0b0100:        //MI
        return NF == 1;
    case 0b0101:        //PL
        return NF == 0;
    case 0b0110:        //VS
        return VF == 1;
    case 0b0111:        //VC
        return VF == 0;
    case 0b1000:        //HI
        return CF == 1 && ZF == 0;
    case 0b1001:        //LS
        return CF == 0 || ZF == 1;
    case 0b1010:        //GE
        return NF == VF;
    case 0b1011:        //LT
        return NF != VF;
    case 0b1100:        //GT
        return ZF == 0 && NF == VF;
    case 0b1101:        //LE
        return ZF == 1 || NF != VF;
    case 0b1110:        //AL
        return 1;
    case 0b1111:        //NV
        return 0;
    default:
        tlib_printf(LOG_LEVEL_ERROR, "trying to evaluate incorrect condition code (0x%x)", condition);
        return 0;
    }
}

EXC_INT_1(uint32_t, tlib_evaluate_condition_code, uint32_t, condition)

void tlib_set_cpu_id(uint32_t value)
{
    cpu->cp15.c0_cpuid = value;
}

EXC_VOID_1(tlib_set_cpu_id, uint32_t, value)

void tlib_toggle_fpu(int32_t enabled)
{
    if (enabled) {
        cpu->vfp.xregs[ARM_VFP_FPEXC] |= ARM_VFP_FPEXC_FPUEN_MASK;
    } else {
        cpu->vfp.xregs[ARM_VFP_FPEXC] &= ~ARM_VFP_FPEXC_FPUEN_MASK;
    }
}

EXC_VOID_1(tlib_toggle_fpu, int32_t, enabled)

void tlib_set_sev_on_pending(int32_t value)
{
    cpu->sev_on_pending = !!value;
}

EXC_VOID_1(tlib_set_sev_on_pending, int32_t, value)

void tlib_set_event_flag(int value)
{
    cpu->sev_pending = !!value;
}

EXC_VOID_1(tlib_set_event_flag, int, value)

void tlib_set_thumb(int value)
{
    cpu->thumb = value != 0;
}

EXC_VOID_1(tlib_set_thumb, int, value)

void tlib_set_number_of_mpu_regions(uint32_t value)
{
    if (value >= MAX_MPU_REGIONS) {
        tlib_abortf("Failed to set number of unified MPU regions to %u, maximal supported value is %u", value, MAX_MPU_REGIONS);
    }
    cpu->number_of_mpu_regions = value;
}

EXC_VOID_1(tlib_set_number_of_mpu_regions, uint32_t, value)

uint32_t tlib_get_number_of_mpu_regions()
{
    return cpu->number_of_mpu_regions;
}

EXC_INT_0(uint32_t, tlib_get_number_of_mpu_regions)

void tlib_register_tcm_region(uint32_t address, uint64_t size, uint64_t index)
{
    // interface index is the opc2 value when addressing region register via MRC/MCR
    // region index is the selection register value
    uint32_t interface_index = index >> 32;
    uint32_t region_index = index;
    if (interface_index >= 2) {
        tlib_abortf("Attempted to register TCM region for interface #%u. Only 2 TCM interfaces are supported", interface_index);
    }
    if (region_index >= MAX_TCM_REGIONS) {
        tlib_abortf("Attempted to register TCM region #%u, maximal supported value is %u", region_index, MAX_TCM_REGIONS);
    }
    if (size == 0) {
        cpu->cp15.c9_tcmregion[interface_index][region_index] = 0;
        return;
    }
    uint64_t size_unit = 0x200; // unit * 2 ^ exp == size
    uint32_t min_size_exp = 0b00001;
    uint32_t max_size_exp = 0b11111;
    if (size < (size_unit << min_size_exp) || size > (size_unit << max_size_exp) || !is_power_of_2(size)) {
        tlib_abortf("Attempted to register TCM region #%u, maximal supported value is %u", region_index, MAX_TCM_REGIONS);
    }
    if ((address % TARGET_PAGE_SIZE) != 0 || (address & ((1 << 7) - 1)) || (address % size) != 0) {
        tlib_abortf("Attempted to set illegal TCM region base address (0x%llx)", address);
    }
    cpu->cp15.c9_tcmregion[interface_index][region_index] = address | (ctz64(size / size_unit) << 2) | 1; // always enable
}

EXC_VOID_3(tlib_register_tcm_region, uint32_t, address, uint64_t, size, uint64_t, index)

#ifdef TARGET_PROTO_ARM_M

void tlib_set_interrupt_vector_base(uint32_t address)
{
    cpu->v7m.vecbase = address;
}

EXC_VOID_1(tlib_set_interrupt_vector_base, uint32_t, address)

uint32_t tlib_get_interrupt_vector_base()
{
    return cpu->v7m.vecbase;
}

EXC_INT_0(uint32_t, tlib_get_interrupt_vector_base)

uint32_t tlib_get_xpsr()
{
    return xpsr_read(cpu);
}

EXC_INT_0(uint32_t, tlib_get_xpsr)

uint32_t tlib_get_fault_status()
{
    return cpu->v7m.fault_status;
}

EXC_INT_0(uint32_t, tlib_get_fault_status)

void tlib_set_fault_status(uint32_t value)
{
    cpu->v7m.fault_status = value;
}

EXC_VOID_1(tlib_set_fault_status, uint32_t, value)

uint32_t tlib_get_memory_fault_address()
{
    return cpu->cp15.c6_data;
}

EXC_INT_0(uint32_t, tlib_get_memory_fault_address)

uint32_t tlib_is_mpu_enabled()
{
    return cpu->cp15.c1_sys & 0x1;
}

EXC_INT_0(uint32_t, tlib_is_mpu_enabled)

void tlib_enable_mpu(int32_t enabled)
{
    if (!!enabled != (cpu->cp15.c1_sys & 1)) {
        cpu->cp15.c1_sys ^= 1;
        tlb_flush(cpu, 1, false);
    }
}

EXC_VOID_1(tlib_enable_mpu, int32_t, enabled)

void tlib_set_mpu_region_number(uint32_t value)
{
    if (value >= cpu->number_of_mpu_regions) {
        tlib_abortf("MPU: Trying to use non-existent MPU region. Number of regions: %d, faulting region number: %d", cpu->number_of_mpu_regions, value);
    }
    cpu->cp15.c6_region_number = value;
    tlb_flush(cpu, 1, false);
}

EXC_VOID_1(tlib_set_mpu_region_number, uint32_t, value)

// This function mimics mpu configuration through the "Region Base Address" register
void tlib_set_mpu_region_base_address(uint32_t value)
{
    if (value & 0x10) {
        /* If VALID (0x10) bit is set, we change the region number to zero-extended value of youngest 4 bits */
        tlib_set_mpu_region_number(value & 0xF);
    }
    cpu->cp15.c6_base_address[cpu->cp15.c6_region_number] = value & 0xFFFFFFE0;
#if DEBUG
    tlib_printf(LOG_LEVEL_DEBUG, "MPU: Set base address 0x%x, for region %lld", value & 0xFFFFFFE0, cpu->cp15.c6_region_number);
#endif
    tlb_flush(cpu, 1, false);
}

EXC_VOID_1(tlib_set_mpu_region_base_address, uint32_t, value)

// This function mimics mpu configuration through the "Region Attribute and Size" register
void tlib_set_mpu_region_size_and_enable(uint32_t value)
{
    uint32_t index = cpu->cp15.c6_region_number;
    cpu->cp15.c6_size_and_enable[index] = value & MPU_SIZE_AND_ENABLE_FIELD_MASK;
    cpu->cp15.c6_subregion_disable[index] = (value & MPU_SUBREGION_DISABLE_FIELD_MASK) >> MPU_SUBREGION_DISABLE_FIELD_OFFSET;
    cpu->cp15.c6_access_control[index] = value >> 16;
#if DEBUG
    tlib_printf(LOG_LEVEL_DEBUG, "MPU: Set access control 0x%x, permissions 0x%x, size 0x%x, enable 0x%x, for region %lld", value >> 16, ((value >> 16) & MPU_PERMISSION_FIELD_MASK) >> 8 , (value & MPU_SIZE_FIELD_MASK) >> 1, value & MPU_REGION_ENABLED_BIT, index);
#endif
    tlb_flush(cpu, 1, false);
}

EXC_VOID_1(tlib_set_mpu_region_size_and_enable, uint32_t, value)

// This function mimics mpu configuration through the "Region Base Address" register
uint32_t tlib_get_mpu_region_base_address()
{
    return cpu->cp15.c6_base_address[cpu->cp15.c6_region_number] | cpu->cp15.c6_region_number;
}

EXC_INT_0(uint32_t, tlib_get_mpu_region_base_address)

// This function mimics mpu configuration through the "Region Attribute and Size" register
uint32_t tlib_get_mpu_region_size_and_enable()
{
    uint32_t index = cpu->cp15.c6_region_number;
    return (cpu->cp15.c6_access_control[index] << 16) | (cpu->cp15.c6_subregion_disable[index] << 8) | cpu->cp15.c6_size_and_enable[index];
}

EXC_INT_0(uint32_t, tlib_get_mpu_region_size_and_enable)

uint32_t tlib_get_mpu_region_number()
{
    return cpu->cp15.c6_region_number;
}

EXC_INT_0(uint32_t, tlib_get_mpu_region_number)
/* See vfp_trigger_exception for irq_number value interpretation */
void tlib_set_fpu_interrupt_number(int32_t irq_number)
{
    cpu->vfp.fpu_interrupt_irq_number = irq_number;
}

EXC_VOID_1(tlib_set_fpu_interrupt_number, int32_t, irq_number)

uint32_t tlib_is_v8()
{
    return arm_feature(env, ARM_FEATURE_V8);
}
EXC_INT_0(uint32_t, tlib_is_v8)

/* PMSAv8 */

static void guard_pmsav8()
{
    if (!arm_feature(env, ARM_FEATURE_V8))
    {
        tlib_abort("This feature is only supported on ARM v8-M architecture");
    }
}

void tlib_set_pmsav8_ctrl(uint32_t value)
{
    guard_pmsav8();
    cpu->pmsav8.ctrl = value;
}
EXC_VOID_1(tlib_set_pmsav8_ctrl, uint32_t, value)

void tlib_set_pmsav8_rnr(uint32_t value)
{
    guard_pmsav8();
    if (value > MAX_MPU_REGIONS) {
        tlib_printf(LOG_LEVEL_ERROR, "Requested RNR value is greater than the maximum MPU regions");
        return;
    }
    cpu->pmsav8.rnr = value;
}
EXC_VOID_1(tlib_set_pmsav8_rnr, uint32_t, value)

void tlib_set_pmsav8_rbar(uint32_t value)
{
    guard_pmsav8();
    uint32_t index = cpu->pmsav8.rnr;
    cpu->pmsav8.rbar[index] = value;
}
EXC_VOID_1(tlib_set_pmsav8_rbar, uint32_t, value)

void tlib_set_pmsav8_rlar(uint32_t value)
{
    guard_pmsav8();
    uint32_t index = cpu->pmsav8.rnr;
    cpu->pmsav8.rlar[index] = value;
}
EXC_VOID_1(tlib_set_pmsav8_rlar, uint32_t, value)

void tlib_set_pmsav8_mair(uint32_t index, uint32_t value)
{
    guard_pmsav8();
    if (index > 1) {
        tlib_printf(LOG_LEVEL_ERROR, "Only indexes {0,1} are supported by MAIR registers");
        return;
    }
    cpu->pmsav8.mair[index] = value;
}
EXC_VOID_2(tlib_set_pmsav8_mair, uint32_t, index, uint32_t, value)

uint32_t tlib_get_pmsav8_ctrl()
{
    guard_pmsav8();
    return cpu->pmsav8.ctrl;
}
EXC_INT_0(uint32_t, tlib_get_pmsav8_ctrl)

uint32_t tlib_get_pmsav8_rnr()
{
    guard_pmsav8();
    return cpu->pmsav8.rnr;
}
EXC_INT_0(uint32_t, tlib_get_pmsav8_rnr)

uint32_t tlib_get_pmsav8_rbar()
{
    guard_pmsav8();
    uint32_t index = cpu->pmsav8.rnr;
    return cpu->pmsav8.rbar[index];
}
EXC_INT_0(uint32_t, tlib_get_pmsav8_rbar)

uint32_t tlib_get_pmsav8_rlar()
{
    guard_pmsav8();
    uint32_t index = cpu->pmsav8.rnr;
    return cpu->pmsav8.rlar[index];
}
EXC_INT_0(uint32_t, tlib_get_pmsav8_rlar)

uint32_t tlib_get_pmsav8_mair(uint32_t index)
{
    guard_pmsav8();
    if (index > 1) {
        tlib_printf(LOG_LEVEL_ERROR, "Only indexes {0,1} are supported by MAIR registers");
        return 0;
    }
    return cpu->pmsav8.mair[index];
}
EXC_INT_1(uint32_t, tlib_get_pmsav8_mair, uint32_t, index)

#endif
