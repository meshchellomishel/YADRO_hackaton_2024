//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.YADRO
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToWord | AllowedTranslation.DoubleWordToByte | AllowedTranslation.WordToByte | AllowedTranslation.ByteToDoubleWord)]    public class MMI_YADRO_RC32 : IKnownSize, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public MMI_YADRO_RC32(Machine machine) // : base(machine, 6*16)
        {
          this.machine = machine;
          RegistersCollection = new DoubleWordRegisterCollection(this, DefineRegisters());
          ;
        }

        private Dictionary<long, DoubleWordRegister> DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Char, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        string simb = new string(new char[]{(char)(value&0xff)});
                        Console.Write("{0}", simb);
                        //this.Log(LogLevel.Info, "{0}", simb);
                    })
                },
                {(long)Registers.Exit, new DoubleWordRegister(this)
                   .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        Console.WriteLine("MMI Exit with code {0}", value);
                        //this.Log(LogLevel.Info, "MMI Exit with code {0}", value);
                        //UserInterface.Commands.PauseCommand.Halt(monitor);
                        //EmulationManager.Instance.CurrentEmulation.PauseAll();
                    })
                },
                {(long)Registers.Bin, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        Console.Write("0b");
                       // this.Log(LogLevel.Info, "0b");
                        for(int i=31; i>=0; i--){
                            Console.Write((value & ((ulong)1 << i))==0 ? 0 : 1);
                           // this.Log(LogLevel.Info, "{0}", (value & ((ulong)1 << i))==0 ? 0 : 1);
                            if((i%8)==0 && i!=0) {
                                Console.Write('_');
                              //  this.Log(LogLevel.Info, "_");
                            }
                        }
                    })
                },
                {(long)Registers.Dec, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        Console.Write("{0}", value);
                    })
                },
                {(long)Registers.Hex, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        Console.Write("0x{0:X}", value);
                    })
                },
                {(long)Registers.String, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        byte[] buf = machine.GetSystemBus(this).ReadBytes(value, 256);
                        int cnt=0;
                        for(;buf[cnt]!=0;cnt++){}
                        string str = Encoding.Default.GetString(buf, 0, cnt);
                        Console.WriteLine(value);
                    })
                }
            };
            return registersMap;
        }
        public long Size
        {
            get
            {
                return 0x80;
            }
        }
        public void Reset ()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
         }
        public DoubleWordRegisterCollection RegistersCollection { get; }
        private enum Registers : long
        {
            Char = 0x0,
            Exit = 0x8,
            String = 0x10,
            Bin    = 0x20,
            Hex    = 0x28,
            Dec    = 0x30
        }

        private Machine machine;
    }
}

