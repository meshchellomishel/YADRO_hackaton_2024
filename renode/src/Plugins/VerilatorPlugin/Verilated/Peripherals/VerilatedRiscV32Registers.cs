﻿//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.CPU.Registers;
using Antmicro.Renode.Utilities.Binding;

namespace Antmicro.Renode.Peripherals.Verilated
{
    public partial class VerilatedRiscV32
    {
        [Register]
        public RegisterValue ZERO
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.ZERO);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.ZERO, value);
            }
        }
        [Register]
        public RegisterValue RA
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.RA);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.RA, value);
            }
        }
        [Register]
        public RegisterValue SP
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.SP);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.SP, value);
            }
        }
        [Register]
        public RegisterValue GP
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.GP);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.GP, value);
            }
        }
        [Register]
        public RegisterValue TP
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.TP);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.TP, value);
            }
        }
        [Register]
        public RegisterValue FP
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.FP);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.FP, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.PC);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.PC, value);
            }
        }
        [Register]
        public RegisterValue SSTATUS
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.SSTATUS);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.SSTATUS, value);
            }
        }
        [Register]
        public RegisterValue SIE
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.SIE);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.SIE, value);
            }
        }
        [Register]
        public RegisterValue STVEC
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.STVEC);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.STVEC, value);
            }
        }
        [Register]
        public RegisterValue SSCRATCH
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.SSCRATCH);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.SSCRATCH, value);
            }
        }
        [Register]
        public RegisterValue SEPC
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.SEPC);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.SEPC, value);
            }
        }
        [Register]
        public RegisterValue SCAUSE
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.SCAUSE);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.SCAUSE, value);
            }
        }
        [Register]
        public RegisterValue STVAL
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.STVAL);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.STVAL, value);
            }
        }
        [Register]
        public RegisterValue SIP
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.SIP);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.SIP, value);
            }
        }
        [Register]
        public RegisterValue SATP
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.SATP);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.SATP, value);
            }
        }
        [Register]
        public RegisterValue SPTBR
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.SPTBR);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.SPTBR, value);
            }
        }
        [Register]
        public RegisterValue MSTATUS
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MSTATUS);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MSTATUS, value);
            }
        }
        [Register]
        public RegisterValue MISA
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MISA);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MISA, value);
            }
        }
        [Register]
        public RegisterValue MEDELEG
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MEDELEG);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MEDELEG, value);
            }
        }
        [Register]
        public RegisterValue MIDELEG
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MIDELEG);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MIDELEG, value);
            }
        }
        [Register]
        public RegisterValue MIE
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MIE);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MIE, value);
            }
        }
        [Register]
        public RegisterValue MTVEC
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MTVEC);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MTVEC, value);
            }
        }
        [Register]
        public RegisterValue MSCRATCH
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MSCRATCH);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MSCRATCH, value);
            }
        }
        [Register]
        public RegisterValue MEPC
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MEPC);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MEPC, value);
            }
        }
        [Register]
        public RegisterValue MCAUSE
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MCAUSE);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MCAUSE, value);
            }
        }
        [Register]
        public RegisterValue MTVAL
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MTVAL);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MTVAL, value);
            }
        }
        [Register]
        public RegisterValue MIP
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.MIP);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.MIP, value);
            }
        }
        [Register]
        public RegisterValue PRIV
        {
            get
            {
                return GetRegisterValue32((int)VerilatedRiscV32Registers.PRIV);
            }
            set
            {
                SetRegisterValue32((int)VerilatedRiscV32Registers.PRIV, value);
            }
        }
        public RegistersGroup X { get; private set; }
        public RegistersGroup T { get; private set; }
        public RegistersGroup S { get; private set; }
        public RegistersGroup A { get; private set; }
        public RegistersGroup F { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapX = new Dictionary<int, VerilatedRiscV32Registers>
            {
                { 0, VerilatedRiscV32Registers.X0 },
                { 1, VerilatedRiscV32Registers.X1 },
                { 2, VerilatedRiscV32Registers.X2 },
                { 3, VerilatedRiscV32Registers.X3 },
                { 4, VerilatedRiscV32Registers.X4 },
                { 5, VerilatedRiscV32Registers.X5 },
                { 6, VerilatedRiscV32Registers.X6 },
                { 7, VerilatedRiscV32Registers.X7 },
                { 8, VerilatedRiscV32Registers.X8 },
                { 9, VerilatedRiscV32Registers.X9 },
                { 10, VerilatedRiscV32Registers.X10 },
                { 11, VerilatedRiscV32Registers.X11 },
                { 12, VerilatedRiscV32Registers.X12 },
                { 13, VerilatedRiscV32Registers.X13 },
                { 14, VerilatedRiscV32Registers.X14 },
                { 15, VerilatedRiscV32Registers.X15 },
                { 16, VerilatedRiscV32Registers.X16 },
                { 17, VerilatedRiscV32Registers.X17 },
                { 18, VerilatedRiscV32Registers.X18 },
                { 19, VerilatedRiscV32Registers.X19 },
                { 20, VerilatedRiscV32Registers.X20 },
                { 21, VerilatedRiscV32Registers.X21 },
                { 22, VerilatedRiscV32Registers.X22 },
                { 23, VerilatedRiscV32Registers.X23 },
                { 24, VerilatedRiscV32Registers.X24 },
                { 25, VerilatedRiscV32Registers.X25 },
                { 26, VerilatedRiscV32Registers.X26 },
                { 27, VerilatedRiscV32Registers.X27 },
                { 28, VerilatedRiscV32Registers.X28 },
                { 29, VerilatedRiscV32Registers.X29 },
                { 30, VerilatedRiscV32Registers.X30 },
                { 31, VerilatedRiscV32Registers.X31 },
            };
            X = new RegistersGroup(
                indexValueMapX.Keys,
                i => GetRegisterUnsafe((int)indexValueMapX[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapX[i], v));

            var indexValueMapT = new Dictionary<int, VerilatedRiscV32Registers>
            {
                { 0, VerilatedRiscV32Registers.T0 },
                { 1, VerilatedRiscV32Registers.T1 },
                { 2, VerilatedRiscV32Registers.T2 },
                { 3, VerilatedRiscV32Registers.T3 },
                { 4, VerilatedRiscV32Registers.T4 },
                { 5, VerilatedRiscV32Registers.T5 },
                { 6, VerilatedRiscV32Registers.T6 },
            };
            T = new RegistersGroup(
                indexValueMapT.Keys,
                i => GetRegisterUnsafe((int)indexValueMapT[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapT[i], v));

            var indexValueMapS = new Dictionary<int, VerilatedRiscV32Registers>
            {
                { 0, VerilatedRiscV32Registers.S0 },
                { 1, VerilatedRiscV32Registers.S1 },
                { 2, VerilatedRiscV32Registers.S2 },
                { 3, VerilatedRiscV32Registers.S3 },
                { 4, VerilatedRiscV32Registers.S4 },
                { 5, VerilatedRiscV32Registers.S5 },
                { 6, VerilatedRiscV32Registers.S6 },
                { 7, VerilatedRiscV32Registers.S7 },
                { 8, VerilatedRiscV32Registers.S8 },
                { 9, VerilatedRiscV32Registers.S9 },
                { 10, VerilatedRiscV32Registers.S10 },
                { 11, VerilatedRiscV32Registers.S11 },
            };
            S = new RegistersGroup(
                indexValueMapS.Keys,
                i => GetRegisterUnsafe((int)indexValueMapS[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapS[i], v));

            var indexValueMapA = new Dictionary<int, VerilatedRiscV32Registers>
            {
                { 0, VerilatedRiscV32Registers.A0 },
                { 1, VerilatedRiscV32Registers.A1 },
                { 2, VerilatedRiscV32Registers.A2 },
                { 3, VerilatedRiscV32Registers.A3 },
                { 4, VerilatedRiscV32Registers.A4 },
                { 5, VerilatedRiscV32Registers.A5 },
                { 6, VerilatedRiscV32Registers.A6 },
                { 7, VerilatedRiscV32Registers.A7 },
            };
            A = new RegistersGroup(
                indexValueMapA.Keys,
                i => GetRegisterUnsafe((int)indexValueMapA[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapA[i], v));

            var indexValueMapF = new Dictionary<int, VerilatedRiscV32Registers>
            {
                { 0, VerilatedRiscV32Registers.F0 },
                { 1, VerilatedRiscV32Registers.F1 },
                { 2, VerilatedRiscV32Registers.F2 },
                { 3, VerilatedRiscV32Registers.F3 },
                { 4, VerilatedRiscV32Registers.F4 },
                { 5, VerilatedRiscV32Registers.F5 },
                { 6, VerilatedRiscV32Registers.F6 },
                { 7, VerilatedRiscV32Registers.F7 },
                { 8, VerilatedRiscV32Registers.F8 },
                { 9, VerilatedRiscV32Registers.F9 },
                { 10, VerilatedRiscV32Registers.F10 },
                { 11, VerilatedRiscV32Registers.F11 },
                { 12, VerilatedRiscV32Registers.F12 },
                { 13, VerilatedRiscV32Registers.F13 },
                { 14, VerilatedRiscV32Registers.F14 },
                { 15, VerilatedRiscV32Registers.F15 },
                { 16, VerilatedRiscV32Registers.F16 },
                { 17, VerilatedRiscV32Registers.F17 },
                { 18, VerilatedRiscV32Registers.F18 },
                { 19, VerilatedRiscV32Registers.F19 },
                { 20, VerilatedRiscV32Registers.F20 },
                { 21, VerilatedRiscV32Registers.F21 },
                { 22, VerilatedRiscV32Registers.F22 },
                { 23, VerilatedRiscV32Registers.F23 },
                { 24, VerilatedRiscV32Registers.F24 },
                { 25, VerilatedRiscV32Registers.F25 },
                { 26, VerilatedRiscV32Registers.F26 },
                { 27, VerilatedRiscV32Registers.F27 },
                { 28, VerilatedRiscV32Registers.F28 },
                { 29, VerilatedRiscV32Registers.F29 },
                { 30, VerilatedRiscV32Registers.F30 },
                { 31, VerilatedRiscV32Registers.F31 },
            };
            F = new RegistersGroup(
                indexValueMapF.Keys,
                i => GetRegisterUnsafe((int)indexValueMapF[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapF[i], v));

        }

        private static readonly Dictionary<VerilatedRiscV32Registers, CPURegister> mapping = new Dictionary<VerilatedRiscV32Registers, CPURegister>
        {
            { VerilatedRiscV32Registers.ZERO,  new CPURegister(0, 32, isGeneral: true, isReadonly: true) },
            { VerilatedRiscV32Registers.RA,  new CPURegister(1, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.SP,  new CPURegister(2, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.GP,  new CPURegister(3, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.TP,  new CPURegister(4, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X5,  new CPURegister(5, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X6,  new CPURegister(6, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X7,  new CPURegister(7, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.FP,  new CPURegister(8, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X9,  new CPURegister(9, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X10,  new CPURegister(10, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X11,  new CPURegister(11, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X12,  new CPURegister(12, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X13,  new CPURegister(13, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X14,  new CPURegister(14, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X15,  new CPURegister(15, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X16,  new CPURegister(16, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X17,  new CPURegister(17, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X18,  new CPURegister(18, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X19,  new CPURegister(19, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X20,  new CPURegister(20, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X21,  new CPURegister(21, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X22,  new CPURegister(22, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X23,  new CPURegister(23, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X24,  new CPURegister(24, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X25,  new CPURegister(25, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X26,  new CPURegister(26, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X27,  new CPURegister(27, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X28,  new CPURegister(28, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X29,  new CPURegister(29, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X30,  new CPURegister(30, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.X31,  new CPURegister(31, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.PC,  new CPURegister(32, 32, isGeneral: true, isReadonly: false) },
            { VerilatedRiscV32Registers.F0,  new CPURegister(33, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F1,  new CPURegister(34, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F2,  new CPURegister(35, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F3,  new CPURegister(36, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F4,  new CPURegister(37, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F5,  new CPURegister(38, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F6,  new CPURegister(39, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F7,  new CPURegister(40, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F8,  new CPURegister(41, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F9,  new CPURegister(42, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F10,  new CPURegister(43, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F11,  new CPURegister(44, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F12,  new CPURegister(45, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F13,  new CPURegister(46, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F14,  new CPURegister(47, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F15,  new CPURegister(48, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F16,  new CPURegister(49, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F17,  new CPURegister(50, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F18,  new CPURegister(51, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F19,  new CPURegister(52, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F20,  new CPURegister(53, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F21,  new CPURegister(54, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F22,  new CPURegister(55, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F23,  new CPURegister(56, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F24,  new CPURegister(57, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F25,  new CPURegister(58, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F26,  new CPURegister(59, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F27,  new CPURegister(60, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F28,  new CPURegister(61, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F29,  new CPURegister(62, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F30,  new CPURegister(63, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.F31,  new CPURegister(64, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.SSTATUS,  new CPURegister(256, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.SIE,  new CPURegister(260, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.STVEC,  new CPURegister(261, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.SSCRATCH,  new CPURegister(320, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.SEPC,  new CPURegister(321, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.SCAUSE,  new CPURegister(322, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.STVAL,  new CPURegister(323, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.SIP,  new CPURegister(324, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.SATP,  new CPURegister(384, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MSTATUS,  new CPURegister(768, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MISA,  new CPURegister(769, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MEDELEG,  new CPURegister(770, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MIDELEG,  new CPURegister(771, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MIE,  new CPURegister(772, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MTVEC,  new CPURegister(773, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MSCRATCH,  new CPURegister(832, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MEPC,  new CPURegister(833, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MCAUSE,  new CPURegister(834, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MTVAL,  new CPURegister(835, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.MIP,  new CPURegister(836, 32, isGeneral: false, isReadonly: false) },
            { VerilatedRiscV32Registers.PRIV,  new CPURegister(4096, 32, isGeneral: false, isReadonly: false) },
        };
    }

    public enum VerilatedRiscV32Registers
    {
        ZERO = 0,
        RA = 1,
        SP = 2,
        GP = 3,
        TP = 4,
        FP = 8,
        PC = 32,
        SSTATUS = 256,
        SIE = 260,
        STVEC = 261,
        SSCRATCH = 320,
        SEPC = 321,
        SCAUSE = 322,
        STVAL = 323,
        SIP = 324,
        SATP = 384,
        SPTBR = 384,
        MSTATUS = 768,
        MISA = 769,
        MEDELEG = 770,
        MIDELEG = 771,
        MIE = 772,
        MTVEC = 773,
        MSCRATCH = 832,
        MEPC = 833,
        MCAUSE = 834,
        MTVAL = 835,
        MIP = 836,
        PRIV = 4096,
        X0 = 0,
        X1 = 1,
        X2 = 2,
        X3 = 3,
        X4 = 4,
        X5 = 5,
        X6 = 6,
        X7 = 7,
        X8 = 8,
        X9 = 9,
        X10 = 10,
        X11 = 11,
        X12 = 12,
        X13 = 13,
        X14 = 14,
        X15 = 15,
        X16 = 16,
        X17 = 17,
        X18 = 18,
        X19 = 19,
        X20 = 20,
        X21 = 21,
        X22 = 22,
        X23 = 23,
        X24 = 24,
        X25 = 25,
        X26 = 26,
        X27 = 27,
        X28 = 28,
        X29 = 29,
        X30 = 30,
        X31 = 31,
        T0 = 5,
        T1 = 6,
        T2 = 7,
        T3 = 28,
        T4 = 29,
        T5 = 30,
        T6 = 31,
        S0 = 8,
        S1 = 9,
        S2 = 18,
        S3 = 19,
        S4 = 20,
        S5 = 21,
        S6 = 22,
        S7 = 23,
        S8 = 24,
        S9 = 25,
        S10 = 26,
        S11 = 27,
        A0 = 10,
        A1 = 11,
        A2 = 12,
        A3 = 13,
        A4 = 14,
        A5 = 15,
        A6 = 16,
        A7 = 17,
        F0 = 33,
        F1 = 34,
        F2 = 35,
        F3 = 36,
        F4 = 37,
        F5 = 38,
        F6 = 39,
        F7 = 40,
        F8 = 41,
        F9 = 42,
        F10 = 43,
        F11 = 44,
        F12 = 45,
        F13 = 46,
        F14 = 47,
        F15 = 48,
        F16 = 49,
        F17 = 50,
        F18 = 51,
        F19 = 52,
        F20 = 53,
        F21 = 54,
        F22 = 55,
        F23 = 56,
        F24 = 57,
        F25 = 58,
        F26 = 59,
        F27 = 60,
        F28 = 61,
        F29 = 62,
        F30 = 63,
        F31 = 64,
    }
}
