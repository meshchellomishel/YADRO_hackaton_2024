/*
 * Copyright (c) Antmicro
 *
 * SPDX-License-Identifier: Apache-2.0
 */

#ifndef SYSTEM_REGISTERS_COMMON_H_
#define SYSTEM_REGISTERS_COMMON_H_

// Types of ARMCPRegInfo.
// Each bit is a different type.
#define ARM_CP_NOP             (1 << 0)
#define ARM_CP_CURRENTEL       (1 << 1)
// Special regs
#define ARM_CP_NZCV            (1 << 2)
#define ARM_CP_DC_ZVA          (1 << 3)
#define ARM_CP_DC_GVA          (1 << 4)
#define ARM_CP_DC_GZVA         (1 << 5)
#define ARM_CP_WFI             (1 << 6)
// Mask used on above type - remember to update it when adding more special types!
#define ARM_CP_SPECIAL_MASK    0x007F

#define ARM_CP_64BIT           (1 << 8)
#define ARM_CP_CONST           (1 << 9)
#define ARM_CP_FPU             (1 << 10)
#define ARM_CP_IO              (1 << 11)
#define ARM_CP_RAISES_EXC      (1 << 12)
#define ARM_CP_RO              (1 << 13)  // Read-only
#define ARM_CP_SME             (1 << 14)
#define ARM_CP_SUPPRESS_TB_END (1 << 15)
#define ARM_CP_SVE             (1 << 16)
#define ARM_CP_WO              (1 << 17)  // Write-Only
#define ARM_CP_TLB_FLUSH       (1 << 18)  // TLB will be flushed after writing such a register
// TODO: Implement gen_helper_rebuild_hflags_a32_newel() for handling ARM_CP_NEWEL
#define ARM_CP_NEWEL           (1 << 19)  // Write can change EL

// Minimum EL access
#define ARM_CP_EL_SHIFT        20
#define ARM_CP_EL_MASK         (3 << ARM_CP_EL_SHIFT)
#define ARM_CP_EL_0            (0 << ARM_CP_EL_SHIFT)
#define ARM_CP_EL_1            (1 << ARM_CP_EL_SHIFT)
#define ARM_CP_EL_2            (2 << ARM_CP_EL_SHIFT)
#define ARM_CP_EL_3            (3 << ARM_CP_EL_SHIFT)

#define ARM_CP_READABLE(ri_type)   (!(ri_type & ARM_CP_WO))
#define ARM_CP_WRITABLE(ri_type)   (!(ri_type & ARM_CP_RO))
#define ARM_CP_GET_MIN_EL(ri_type) ((ri_type & ARM_CP_EL_MASK) >> ARM_CP_EL_SHIFT)

typedef enum {
    CP_ACCESS_EL0,
    CP_ACCESS_EL1,
    CP_ACCESS_EL2,
    CP_ACCESS_EL3,
    CP_ACCESS_OK                 = 0x10,
    CP_ACCESS_TRAP_EL2           = 0x20,
    CP_ACCESS_TRAP_UNCATEGORIZED = 0x30,
    CP_ACCESS_TRAP               = 0x40,
} CPAccessResult;
#define CP_ACCESS_EL_MASK 3

typedef struct ARMCPRegInfo ARMCPRegInfo;
typedef CPAccessResult AccessFn(CPUState *, const ARMCPRegInfo *, bool isread);
typedef uint64_t ReadFn(CPUState *, const ARMCPRegInfo *);
typedef void WriteFn(CPUState *, const ARMCPRegInfo *, uint64_t);

struct ARMCPRegInfo
{
    const char *name;
    // Register coprocessor, in AArch64 always CP_REG_ARM64_SYSREG_CP
    uint32_t cp;
    // type of register, if require special handling
    uint32_t type;

    // from C5.1.2, only 2 lower bits used
    uint8_t op0;
    // from C5.1.1, only 3 lower bits used
    uint8_t op1;
    // from C5.1.3, only 4 lower bits used
    uint8_t crn;
    // from C5.1.3, only 4 lower bits used
    uint8_t crm;
    // from C5.1.3, only 4 lower bits used
    uint8_t op2;
    // offset from CPUState struct when there is no readfn/writefn
    uint32_t fieldoffset;
    // resetvalue of the register
    uint64_t resetvalue;
    // fuction that checks if access to the register should be granted
    AccessFn *accessfn;
    // read function (required when fieldoffset and type is missing)
    ReadFn *readfn;
    // write function (required when fieldoffset and type is missing)
    WriteFn *writefn;
};

// Only EL and RO/WO are checked here. Traps etc. are checked in the 'access_check_cp_reg' helper.
static inline bool cp_access_ok(int current_el, const ARMCPRegInfo *reg_info, bool isread)
{
    uint32_t ri_type = reg_info->type;

    if (current_el < ARM_CP_GET_MIN_EL(ri_type)) {
        tlib_printf(LOG_LEVEL_WARNING, "The '%s' register shouldn't be accessed on EL%d", reg_info->name, current_el);
        return false;
    }

    // Rule IWCXDT
    if ((isread && !ARM_CP_READABLE(ri_type)) || (!isread && !ARM_CP_WRITABLE(ri_type))) {
        tlib_printf(LOG_LEVEL_WARNING, "The '%s' register shouldn't be %s", reg_info->name, isread ? "read from" : "written to");
        return false;
    }
    return true;
}

// Always pass an actual array directly; otherwise 'sizeof(array)' will be a pointer size.
#define ARM_CP_ARRAY_COUNT(array) (sizeof(array) / sizeof(ARMCPRegInfo))

/* Macros creating ARMCPRegInfo entries. */

// The parameters have to start with an underscore because preprocessing replaces '.PARAMETER' too.
// The 'extra_type' parameter is any type besides 'ARM_CP_64BIT' and 'ARM_CP_EL*' since those are set automatically.
#define ARM_CP_REG_DEFINE(_name, _cp, _op0, _op1, _crn, _crm, _op2, width, el, extra_type, ...) \
    {                                                           \
        .name        = #_name,                                  \
        .cp          = _cp,                                     \
        .op0         = _op0,                                    \
        .op1         = _op1,                                    \
        .crn         = _crn,                                    \
        .crm         = _crm,                                    \
        .op2         = _op2,                                    \
        .type        = (extra_type                              \
                | (el << ARM_CP_EL_SHIFT)                       \
                | (width == 64 ? ARM_CP_64BIT : 0x0)            \
                ),                                              \
        __VA_ARGS__                                             \
    },

// All ARM64 (AArch64) registers use the same CP value. Width can always be 64 since ARM_CP_64BIT only matters for AArch32 registers.
#define ARM64_CP_REG_DEFINE(name, op0, op1, crn, crm, op2, el, extra_type, ...) \
    ARM_CP_REG_DEFINE(name, CP_REG_ARM64_SYSREG_CP, op0, op1, crn, crm, op2, 64, el, extra_type, __VA_ARGS__)

#define ARM32_CP_REG_DEFINE(name, cp, op1, crn, crm, op2, el, extra_type, ...) \
    ARM_CP_REG_DEFINE(name, cp, 0, op1, crn, crm, op2, 32, el, extra_type, __VA_ARGS__)

#define ARM32_CP_64BIT_REG_DEFINE(name, cp, op1, crm, el, extra_type, ...) \
    ARM_CP_REG_DEFINE(name, cp, 0, op1, 0, crm, 0, 32, el, extra_type | ARM_CP_64BIT, __VA_ARGS__)

/* Macros for the most common types used in 'extra_type'.
 *
 * Reading/writing the register specified as WO/RO (respectively) will trigger the 'Undefined instruction' exception.
 * Therefore CONST can be used with RO if the instruction to write the given register doesn't exist.
 * Writes to a CONST register are simply ignored unless RO is used too.
 *
 * CONST has to be used as the last one in 'extra_type', e.g., 'RW | CONST(0xCAFE)'.
 * IGNORED silences the unhandled warning.
 */

#define CONST(resetvalue)  ARM_CP_CONST, RESETVALUE(resetvalue)
#define IGNORED            ARM_CP_NOP
#define RO                 ARM_CP_RO
#define RW                 0x0
#define WO                 ARM_CP_WO

// Optional macro arguments.
#define ACCESSFN(name)         .accessfn = access_ ## name
#define FIELD(cpu_state_field) .fieldoffset = offsetof(CPUState, cpu_state_field)
#define READFN(name)           .readfn = read_ ## name
#define RESETVALUE(value)      .resetvalue = value
#define RW_FNS(name)           READFN(name), WRITEFN(name)
#define WRITEFN(name)          .writefn = write_ ## name

/* Read/write functions. */

#define READ_FUNCTION(width, mnemonic, value)                                      \
    uint ## width ## _t read_ ## mnemonic(CPUState *env, const ARMCPRegInfo *info) \
    {                                                                              \
        return value;                                                              \
    }

#define WRITE_FUNCTION(width, mnemonic, write_statement)                                        \
    void write_ ## mnemonic(CPUState *env, const ARMCPRegInfo *info, uint ## width ## _t value) \
    {                                                                                           \
        write_statement;                                                                        \
    }

#define RW_FUNCTIONS(width, mnemonic, read_value, write_statement) \
    READ_FUNCTION(width, mnemonic, read_value)                     \
    WRITE_FUNCTION(width, mnemonic, write_statement)

#define RW_FUNCTIONS_PTR(width, mnemonic, pointer) \
    RW_FUNCTIONS(width, mnemonic, *(pointer), *(pointer) = value)

void cp_reg_add(CPUState *env, ARMCPRegInfo *reg_info);

// These need to be marked as unused, so the compiler doesn't complain when including the header outside `system_registers`
__attribute__((unused))
static void cp_regs_add(CPUState *env, ARMCPRegInfo *reg_info_array, uint32_t array_count)
{
    for (int i = 0; i < array_count; i++) {
        ARMCPRegInfo *reg_info = &reg_info_array[i];
        cp_reg_add(env, reg_info);
    }
}

__attribute__((unused))
static void cp_reg_add_with_key(CPUState *env, TTable *cp_regs, uint32_t *key, ARMCPRegInfo *reg_info)
{
    if (!ttable_insert_check(cp_regs, key, reg_info)) {
        tlib_printf(LOG_LEVEL_ERROR,
                    "Duplicated system_register definition!: name: %s, cp: %d, crn: %d, op1: %d, crm: %d, op2: %d, op0: %d",
                    reg_info->name, reg_info->cp, reg_info->crn, reg_info->op1, reg_info->crm, reg_info->op2, reg_info->op0);

        const char * const name = reg_info->name;
        reg_info = ttable_lookup_value_eq(cp_regs, key);
        tlib_printf(LOG_LEVEL_ERROR, "Previously defined as!: name: %s, cp: %d, crn: %d, op1: %d, crm: %d, op2: %d, op0: %d",
                    reg_info->name, reg_info->cp, reg_info->crn, reg_info->op1, reg_info->crm, reg_info->op2, reg_info->op0);
        tlib_abortf("Redefinition of register %s by %s", name, reg_info->name);
    }
}

static inline void log_unhandled_sysreg_access(const char *sysreg_name, bool is_write)
{
    // %-6s is required to format the printf nicely
    tlib_printf(LOG_LEVEL_WARNING, "Unhandled system instruction or register %-6s %s", is_write ? "write:" : "read:", sysreg_name);
}

static inline void log_unhandled_sysreg_read(const char *sysreg_name)
{
    log_unhandled_sysreg_access(sysreg_name, false);
}

static inline void log_unhandled_sysreg_write(const char *sysreg_name)
{
    log_unhandled_sysreg_access(sysreg_name, true);
}

#endif
