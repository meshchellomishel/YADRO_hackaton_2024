//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Plugins.VerilatorPlugin.Connection.Protocols;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Verilated
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class VerilatedUART : BaseDoubleWordVerilatedPeripheral, IUART, ITemperatureSensor
    {
        public VerilatedUART(Machine machine, long frequency, string simulationFilePathLinux = null, string simulationFilePathWindows = null, string simulationFilePathMacOS = null,
            string simulationContextLinux = null, string simulationContextWindows = null, string simulationContextMacOS = null, ulong limitBuffer = LimitBuffer, int timeout = DefaultTimeout, string address = null)
            : base(machine, frequency, simulationFilePathLinux, simulationFilePathWindows, simulationFilePathMacOS, simulationContextLinux, simulationContextWindows, simulationContextMacOS, limitBuffer, timeout, address)
        {
            // TODO: Use REPL files for enviroment updating
            state = State.IDLE;
            size = 15;
            counter = 0;
            temperatureSeq = new decimal[size];

            for(int i = 0; i < size; i++)
            {
                temperatureSeq[i] = i + 1;
            }

            IRQ = new GPIO(); // TODO: 8 bit per 1ms
            conversionTimer = new LimitTimer(machine.ClockSource, frequency / 10, this, "SensorEvent", 100,autoUpdate: true, eventEnabled: true, enabled: true);
            conversionTimer.LimitReached += HandleConversion;
            conversionTimer.Reset();

        }

        public void WriteChar(byte value)
        {
            Send((ActionType)UARTActionNumber.UartWriteCharacter, 0, value);
        }
        

        public override void HandleReceivedMessage(ProtocolMessage message)
        {
            switch(message.ActionId)
            {
                case (ActionType)UARTActionNumber.UartReadCharacter:
                    CharReceived?.Invoke((byte)message.Data);
                    HandleCommand(message.Data);
                    break;
                default:
                    base.HandleReceivedMessage(message);
                    break;
            }
        }

        // StopBits, ParityBit and BaudRate are not in sync with the verilated model
        public Bits StopBits { get { return Bits.One; } }
        public Parity ParityBit { get { return Parity.None; } }
        public uint BaudRate { get { return 115200; } }

        public event Action<byte> CharReceived;

        public GPIO IRQ { get; private set; }

        protected override void HandleInterrupt(ProtocolMessage interrupt)
        {
            this.Log(LogLevel.Debug, "Interrupt\n");
            switch(interrupt.Address)
            {
                case RxdInterrupt:
                    this.Log(LogLevel.Debug, "Interrupt UART\n");
                    IRQ.Set(interrupt.Data != 0);
                    break;
                default:
                    base.HandleInterrupt(interrupt);
                    break; // comment

            }
        }

        private void HandleCommand(ulong cmd)
        {
            switch(cmd) {
                case ('a'):
                    state = State.SendA;
                    counter = 0;
                    break;
                case ('b'):
                    state = State.SendB;
                    counter = 0;
                    break;
                case ('c'):
                    state = State.SendTemperature;
                    counter = 0;
                    break;
                default:
                    state = State.IDLE;
                    counter = 0;
                    break;
            }

        }

        private void SendTemperature()
        {
            if (counter < size) {
                Temperature = temperatureSeq[counter];
                counter++;
            } 
        }

        private void SendAMessage()
        {
            if (counter < aMessage.Length) {
                WriteChar((byte)aMessage[counter]);
                counter++;
            } 
        }

        private void SendBMessage()
        {
            if (counter < bMessage.Length) {
                WriteChar((byte)bMessage[counter]);
                counter++;
            } 
        }

        private void HandleConversion()
        {
            switch (state)
            {
                case (State.IDLE): 
                    break;
                case (State.SendA):
                    SendAMessage(); 
                    break;
                case (State.SendB):
                    SendBMessage();
                    break;
                case (State.SendTemperature):
                    SendTemperature(); 
                    break;
                default : break; // TODO: throw error
            }
        }

        public decimal Temperature {
            get {
                return this.temperature;
            }

            set {
                
                this.temperature   = value;
                WriteChar(Decimal.ToByte(this.temperature));
            }
        }
        private const ulong RxdInterrupt = 0; 
        private decimal temperature;
        private readonly LimitTimer conversionTimer;
        protected decimal[] temperatureSeq;
        protected int counter;
        protected int size;
        private State state;
        private string aMessage = "Hello,";
        private string bMessage = "World!";
    }

    public enum State 
    {
        IDLE = 0,
        SendA = 1,
        SendB = 2,
        SendTemperature = 3
    }
    public enum UARTActionNumber
    {
         UartWriteCharacter = 31,
         UartReadCharacter = 32
    }
}
