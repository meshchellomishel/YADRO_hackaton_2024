//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Helpers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.YADRO
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class YADRO_GPR : SimpleContainer<IPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public YADRO_GPR(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            // rxFifo = new Queue<byte>();
            // txFifo = new Queue<byte>(FifoCapacity);
            // txTransfer = new Queue<byte>();
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());

        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public override void Reset()
        {
            registers.Reset();
            targetDevice = null;
            // transferState = TransferState.Idle;

            // ClearFifos();
            // txTransfer.Clear();

            // foreach(var flag in GetInterruptFlags())
            // {
            //     flag.Reset();
            // }
            // UpdateInterrupts();
        }

        private ulong REG_0_old_value = 0x01234567;
        private ulong REG_0_new_value = 0;
        private ulong REG_2_old_value = 0x89ABCDEF;
        private ulong REG_2_new_value = 0;
        private ulong REG_3_30_old_value = 0x10;
        private ulong REG_3_30_new_value = 0;
        private ulong REG_3_32_old_value = 0xFFFF;
        private ulong REG_3_32_new_value = 0;
        
        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.REG_0, new DoubleWordRegister(this)
                    .WithValueField(0,32, FieldMode.Write | FieldMode.Read , name: "Field 00",valueProviderCallback: (_)=>REG_0_old_value , writeCallback: (_,value)=>
                                        {
                        REG_0_new_value = value;
                        REG_0_old_value = REG_0_new_value;
                        })
                },
                {(long)Registers.REG_1, new DoubleWordRegister(this)
                    .WithValueField(0,32, FieldMode.Read , name: "Field 10", valueProviderCallback: (_) => 0xDEADBEEF)
                },
                {(long)Registers.REG_2, new DoubleWordRegister(this)
                    .WithValueField(0,32, FieldMode.Write, name: "Field 20", writeCallback: (_,value)=>
                                        {
                        REG_2_new_value = value;
                        REG_2_old_value = REG_2_new_value;
                        })
                    // } 0x89ABCDEF)
                },
                {(long)Registers.REG_3, new DoubleWordRegister(this)
                    .WithValueField(16,16, FieldMode.Read | FieldMode.Write, name: "Field 32", valueProviderCallback: (_)=> REG_3_32_old_value, writeCallback: (_,value)=>
                                        {
                        REG_3_32_new_value = value;
                        REG_3_32_old_value = REG_3_32_new_value;
                        })
                    .WithValueField(8,8, FieldMode.Read, name: "Field 31", valueProviderCallback: (_) => 0xFF)
                    .WithValueField(0,8, FieldMode.Write, name: "Field 30", writeCallback: (_, value) =>
                    {
                        REG_3_30_new_value = value;
                        REG_3_30_old_value = REG_3_30_new_value;
                        })
                    // } 0x10)
                },
            };
        }

        private IPeripheral targetDevice;



        private readonly DoubleWordRegisterCollection registers;

        private enum Registers : long
        {
            REG_0 = 0x0,
            REG_1 = 0x4,
            REG_2 = 0x8,
            REG_3 = 0xC,
        }
    }
}
