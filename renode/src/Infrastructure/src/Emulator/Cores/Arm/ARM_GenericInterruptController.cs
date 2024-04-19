//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class ARM_GenericInterruptController : IBusPeripheral, ILocalGPIOReceiver, INumberedGPIOOutput, IIRQController
    {
        public ARM_GenericInterruptController(uint numberOfCPUs = 1, bool supportsTwoSecurityStates = true, ARM_GenericInterruptControllerVersion architectureVersion = DefaultArchitectureVersion, uint sharedPeripheralCount = 960)
        {
            if(numberOfCPUs < 1)
            {
                throw new ConstructionException($"The numberOfCPUs can't be lower than 1, given {numberOfCPUs}.");
            }
            if(sharedPeripheralCount > InterruptsDecoder.MaximumSharedPeripheralCount)
            {
                throw new ConstructionException($"The number of shared peripherals {sharedPeripheralCount} is larger than supported {(InterruptsDecoder.MaximumSharedPeripheralCount)}");
            }

            // The behaviour of the GIC doesn't directly depend on the suppportsTwoSecurityState field
            // The disabledSecurity field corresponds to the GICD_CTRL.DS flag described in the GICv3 Architecture Specification
            // The GIC without support for two security states has disabled security
            // Changing the disabledSecurity field affects the behaviour and register map of a GIC
            // Once security is disabled it's impossible to enable it
            // So it is impossible to enable security for the GIC that doesn't support two security states
            this.supportsTwoSecurityStates = supportsTwoSecurityStates;
            this.ArchitectureVersion = architectureVersion;

            this.irqsDecoder = new InterruptsDecoder(sharedPeripheralCount, identifierBits: 10);

            var irqIds = InterruptId.GetRange(irqsDecoder.SharedPeripheralFirst, irqsDecoder.SharedPeripheralLast)
                .Concat(InterruptId.GetRange(irqsDecoder.ExtendedSharedPeripheralFirst, irqsDecoder.ExtendedSharedPeripheralLast));
            sharedInterrupts = new ReadOnlyDictionary<InterruptId, SharedInterrupt>(irqIds.ToDictionary(id => id, id => new SharedInterrupt(id)));

            var groupTypes = new[]
            {
                GroupType.Group0,
                GroupType.Group1NonSecure,
                GroupType.Group1Secure
            };
            groups = new ReadOnlyDictionary<GroupType, InterruptGroup>(groupTypes.ToDictionary(type => type, _ => new InterruptGroup()));

            cpuEntries = new Dictionary<uint, CPUEntry>();
            supportedInterruptSignals = (InterruptSignalType[])Enum.GetValues(typeof(InterruptSignalType));
            Connections = new ReadOnlyDictionary<int, IGPIO>(Enumerable.Range(0, (int)(numberOfCPUs * supportedInterruptSignals.Length)).ToDictionary<int, int, IGPIO>(x => x, x => new GPIO()));

            // Field layouts of some of the registers depend on the current security state
            distributorRegistersSecureView = new DoubleWordRegisterCollection(this, BuildDistributorRegistersMapSecurityView(false, SecurityState.Secure));
            distributorRegistersNonSecureView = new DoubleWordRegisterCollection(this, BuildDistributorRegistersMapSecurityView(false, SecurityState.NonSecure));
            distributorRegistersDisabledSecurityView = new DoubleWordRegisterCollection(this, BuildDistributorRegistersMapSecurityView(true));
            cpuInterfaceRegistersSecureView = new DoubleWordRegisterCollection(this, BuildCPUInterfaceRegistersMapSecurityView(false, SecurityState.Secure));
            cpuInterfaceRegistersNonSecureView = new DoubleWordRegisterCollection(this, BuildCPUInterfaceRegistersMapSecurityView(false, SecurityState.NonSecure));
            cpuInterfaceRegistersDisabledSecurityView = new DoubleWordRegisterCollection(this, BuildCPUInterfaceRegistersMapSecurityView(true));

            // The rest may behave differently for various security settings, but the layout of fields doesn't change
            distributorRegisters = new DoubleWordRegisterCollection(this, BuildDistributorRegistersMap());
            redistributorDoubleWordRegisters = new DoubleWordRegisterCollection(this, BuildRedistributorDoubleWordRegistersMap());
            redistributorQuadWordRegisters = new QuadWordRegisterCollection(this, BuildRedistributorQuadWordRegistersMap());
            cpuInterfaceRegisters = new DoubleWordRegisterCollection(this, BuildCPUInterfaceRegistersMap());
            cpuInterfaceSystemRegisters = new QuadWordRegisterCollection(this, BuildCPUInterfaceSystemRegistersMap());

            Reset();
        }

        public void Reset()
        {
            LockExecuteAndUpdate(() =>
                {
                    ProductIdentifier = DefaultProductIdentifier;
                    CPUInterfaceRevision = DefaultRevisionNumber;
                    CPUInterfaceImplementer = DefaultImplementerIdentification;
                    DistributorVariant = DefaultVariantNumber;
                    DistributorRevision = DefaultRevisionNumber;
                    DistributorImplementer = DefaultImplementerIdentification;

                    ackControl = false;
                    enableFIQ = false;
                    disabledSecurity = false;
                    foreach(var irq in sharedInterrupts.Values)
                    {
                        irq.Reset();
                    }
                    foreach(var group in groups.Values)
                    {
                        group.Reset();
                    }
                    foreach(var cpu in cpuEntries.Values)
                    {
                        cpu.Reset();
                    }
                });

            distributorRegistersSecureView.Reset();
            distributorRegistersNonSecureView.Reset();
            distributorRegistersDisabledSecurityView.Reset();
            distributorRegisters.Reset();
            redistributorDoubleWordRegisters.Reset();
            redistributorQuadWordRegisters.Reset();
            cpuInterfaceRegistersSecureView.Reset();
            cpuInterfaceRegistersNonSecureView.Reset();
            cpuInterfaceRegistersDisabledSecurityView.Reset();
            cpuInterfaceRegisters.Reset();
            cpuInterfaceSystemRegisters.Reset();
        }

        public void AttachCPU(uint connectionId, IARMSingleSecurityStateCPU cpu)
        {
            AttachCPUEntry(connectionId, new CPUEntry(this, cpu, groups.Keys, GetCPUConnections(connectionId)));
        }

        public void AttachCPU(uint connectionId, IARMTwoSecurityStatesCPU cpu)
        {
            var cpuEntry = new CPUEntryWithTwoSecurityStates(this, cpu, groups.Keys, GetCPUConnections(connectionId));
            cpu.ExecutionModeChanged += (_, __) => OnExecutionModeChanged(cpuEntry);
            AttachCPUEntry(connectionId, cpuEntry);
        }

        [ConnectionRegion("distributor")]
        public void WriteByteToDistributor(long offset, byte value)
        {
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = IsDistributorByteAccessible(offset) && TryWriteByteToDoubleWordCollection(distributorRegisters, offset, value);
                    LogWriteAccess(registerExists, value, "Distributor (byte access)", offset, (DistributorRegisters)offset);
                }
            );
        }

        [ConnectionRegion("distributor")]
        public byte ReadByteFromDistributor(long offset)
        {
            byte value = 0;
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = IsDistributorByteAccessible(offset) && TryReadByteFromDoubleWordCollection(distributorRegisters, offset, out value);
                    LogReadAccess(registerExists, value, "Distributor (byte access)", offset, (DistributorRegisters)offset);
                }
            );
            return value;
        }

        [ConnectionRegion("distributor")]
        public void WriteDoubleWordToDistributor(long offset, uint value)
        {
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = TryWriteRegisterSecurityView(offset, value, distributorRegisters,
                        distributorRegistersSecureView, distributorRegistersNonSecureView, distributorRegistersDisabledSecurityView);
                    LogWriteAccess(registerExists, value, "Distributor", offset, (DistributorRegisters)offset);
                }
            );
        }

        [ConnectionRegion("distributor")]
        public uint ReadDoubleWordFromDistributor(long offset)
        {
            uint value = 0;
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = TryReadRegisterSecurityView(offset, out value, distributorRegisters,
                        distributorRegistersSecureView, distributorRegistersNonSecureView, distributorRegistersDisabledSecurityView);
                    LogReadAccess(registerExists, value, "Distributor", offset, (DistributorRegisters)offset);
                }
            );
            return value;
        }

        [ConnectionRegion("redistributor")]
        public void WriteByteToRedistributor(long offset, byte value)
        {
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = IsRedistributorByteAccessible(offset) && TryWriteByteToDoubleWordCollection(redistributorDoubleWordRegisters, offset, value);
                    LogWriteAccess(registerExists, value, "Redistributor (byte access)", offset, (RedistributorRegisters)offset);
                }
            );
        }

        [ConnectionRegion("redistributor")]
        public byte ReadByteFromRedistributor(long offset)
        {
            byte value = 0;
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = IsRedistributorByteAccessible(offset) && TryReadByteFromDoubleWordCollection(redistributorDoubleWordRegisters, offset, out value);
                    LogReadAccess(registerExists, value, "Redistributor (byte access)", offset, (RedistributorRegisters)offset);
                }
            );
            return value;
        }

        [ConnectionRegion("redistributor")]
        public void WriteDoubleWordToRedistributor(long offset, uint value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(redistributorDoubleWordRegisters.TryWrite(offset, value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
        }

        [ConnectionRegion("redistributor")]
        public uint ReadDoubleWordFromRedistributor(long offset)
        {
            uint value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(redistributorDoubleWordRegisters.TryRead(offset, out value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
            return value;
        }

        [ConnectionRegion("redistributor")]
        public void WriteQuadWordToRedistributor(long offset, ulong value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(redistributorQuadWordRegisters.TryWrite(offset, value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
        }

        [ConnectionRegion("redistributor")]
        public ulong ReadQuadWordFromRedistributor(long offset)
        {
            ulong value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(redistributorQuadWordRegisters.TryRead(offset, out value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
            return value;
        }

        [ConnectionRegion("cpuInterface")]
        public void WriteDoubleWordToCPUInterface(long offset, uint value)
        {
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = TryWriteRegisterSecurityView(offset, value, cpuInterfaceRegisters,
                        cpuInterfaceRegistersSecureView, cpuInterfaceRegistersNonSecureView, cpuInterfaceRegistersDisabledSecurityView);
                    LogWriteAccess(registerExists, value, "memory-mapped CPU Interface", offset, (CPUInterfaceRegisters)offset);
                }
            );
        }

        [ConnectionRegion("cpuInterface")]
        public uint ReadDoubleWordFromCPUInterface(long offset)
        {
            uint value = 0;
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = TryReadRegisterSecurityView(offset, out value, cpuInterfaceRegisters,
                        cpuInterfaceRegistersSecureView, cpuInterfaceRegistersNonSecureView, cpuInterfaceRegistersDisabledSecurityView);
                    LogReadAccess(registerExists, value, "memory-mapped CPU Interface", offset, (CPUInterfaceRegisters)offset);
                }
            );
            return value;
        }

        public void WriteSystemRegisterCPUInterface(uint offset, ulong value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(cpuInterfaceSystemRegisters.TryWrite(offset, value), value, "CPU Interface", offset, (CPUInterfaceSystemRegisters)offset)
            );
        }

        public ulong ReadSystemRegisterCPUInterface(uint offset)
        {
            ulong value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(cpuInterfaceSystemRegisters.TryRead(offset, out value), value, "CPU Interface", offset, (CPUInterfaceSystemRegisters)offset)
            );
            return value;
        }

        public void OnGPIO(int number, bool value)
        {
            var irqId = new InterruptId((uint)number + (uint)irqsDecoder.SharedPeripheralFirst);
            if(!irqsDecoder.IsSharedPeripheral(irqId))
            {
                this.Log(LogLevel.Warning, "Generated interrupt isn't a Shared Peripheral Interrupt, interrupt identifier: {0}", irqId);
                return;
            }
            this.Log(LogLevel.Debug, "Setting signal of the interrupt with id {0} to {1}.", irqId, value);
            LockExecuteAndUpdate(() =>
                sharedInterrupts[irqId].AssertAsPending(value)
            );
        }

        // Private Peripheral Interrupts are connected using the ILocalGPIOReceiver interface
        // Every CPUEntry class implements the IGPIOReceiver interface used to connect PPIs to each CPU
        // The CPUEntry provides event for handling received interrupts by an external action
        // It's expected to handle all of these interrupts by OnPrivateInterrupt method
        public IGPIOReceiver GetLocalReceiver(int cpuConnectionId)
        {
            return GetCPUByConnectionId((uint)cpuConnectionId);
        }

        public IEnumerable<uint> GetEnabledInterruptIdentifiers(uint cpuConnectionId)
        {
            var cpu = GetCPUByConnectionId(cpuConnectionId);
            lock(locker)
            {
                return GetAllEnabledInterupts(cpu).Select(irq => (uint)irq.Identifier);
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public bool DisabledSecurity
        {
            get => !supportsTwoSecurityStates || disabledSecurity;
            set
            {
                if(value == disabledSecurity)
                {
                    return;
                }

                if(!value)
                {
                    // Reenabling security can only be done through a reset
                    this.Log(LogLevel.Warning, "Trying to enable security when it's disabled, write ignored.");
                    return;
                }

                disabledSecurity = true;
                var allInterrupts = cpuEntries.Values.SelectMany(cpu => cpu.Interrupts.Values).Concat(sharedInterrupts.Values);
                foreach(var irq in allInterrupts)
                {
                    irq.GroupModifierBit = false;
                }
            }
        }

        public ARM_GenericInterruptControllerVersion ArchitectureVersion { get; }
        public uint ProductIdentifier { get; set; }
        public byte CPUInterfaceRevision { get; set; }
        public uint CPUInterfaceImplementer { get; set; }
        public byte DistributorVariant { get; set; }
        public byte DistributorRevision { get; set; }
        public uint DistributorImplementer { get; set; }

        private void OnPrivateInterrupt(CPUEntry cpu, int id, bool value)
        {
            var irqId = new InterruptId((uint)id);
            if(!irqsDecoder.IsPrivatePeripheral(irqId))
            {
                this.Log(LogLevel.Warning, "Generated interrupt isn't a Private Peripheral Interrupt, interrupt identifier: {0}", irqId);
                return;
            }
            this.Log(LogLevel.Debug, "Setting signal of the interrupt with id {0} to {1} for {2}.", irqId, value, cpu.Name);
            LockExecuteAndUpdate(() =>
                cpu.Interrupts[irqId].AssertAsPending(value)
            );
        }

        private void OnExecutionModeChanged(CPUEntry cpu)
        {
            lock(locker)
            {
                cpu.UpdateSignals();
            }
        }

        private void LockExecuteAndUpdate(Action action)
        {
            lock(locker)
            {
                action();
                UpdateBestPendingInterrupts();
                foreach(var cpu in cpuEntries.Values)
                {
                    cpu.UpdateSignals();
                }
            }
        }

        private void UpdateBestPendingInterrupts()
        {
            foreach(var cpu in cpuEntries.Values)
            {
                var pendingCandidates = GetAllPendingCandidateInterupts(cpu);
                var bestPending = pendingCandidates.FirstOrDefault();
                foreach(var irq in pendingCandidates.Skip(1))
                {
                    if(irq.Priority < bestPending.Priority)
                    {
                        bestPending = irq;
                        if(bestPending.Priority == InterruptPriority.Highest)
                        {
                            break;
                        }
                    }
                }
                // Setting the bestPending to null indicates there is no pending interrupt
                cpu.BestPending = bestPending;
            }
        }

        private IEnumerable<Interrupt> GetAllInterupts(CPUEntry cpu)
        {
            return cpu.Interrupts.Values.Concat(sharedInterrupts.Values);
        }

        private IEnumerable<Interrupt> GetAllEnabledInterupts(CPUEntry cpu)
        {
            var enabledGroups = groups.Keys.Where(type => groups[type].Enabled && cpu.Groups[type].Enabled).ToArray();
            return cpu.Interrupts.Values
                .Concat(sharedInterrupts.Values.Where(irq => irq.IsTargetingCPU(cpu)))
                .Where(irq => irq.Enabled && enabledGroups.Contains(irq.GroupType));
        }

        private IEnumerable<Interrupt> GetAllPendingCandidateInterupts(CPUEntry cpu)
        {
            return GetAllEnabledInterupts(cpu).Where(irq => irq.Pending && irq.Priority < cpu.PriorityMask && irq.Priority < cpu.RunningPriority);
        }

        private ReadOnlyDictionary<InterruptSignalType, IGPIO> GetCPUConnections(uint connectionId)
        {
            // The convention of connecting interrupt signals can be found near the InterruptSignalType definition
            var firstGPIO = (int)(connectionId * supportedInterruptSignals.Length);

            Func<InterruptSignalType, IGPIO> getGPIOFromInterruptType = interruptType =>
            {
                if(!Connections.TryGetValue(firstGPIO + (int)interruptType, out var interruptGPIO))
                {
                    throw new RecoverableException($"Attaching the CPU with connection id {connectionId} failed, because there are no available interrupt signal connections.");
                }
                return interruptGPIO;
            };

            return new ReadOnlyDictionary<InterruptSignalType, IGPIO>(supportedInterruptSignals.ToDictionary(type => type, getGPIOFromInterruptType));
        }

        private void AttachCPUEntry(uint connectionId, CPUEntry cpuEntry)
        {
            if(cpuEntries.ContainsKey(connectionId))
            {
                throw new RecoverableException($"The CPU with connection id {connectionId} already exists.");
            }
            cpuEntry.PrivateInterruptChanged += OnPrivateInterrupt;
            cpuEntries.Add(connectionId, cpuEntry);
        }

        private Dictionary<long, DoubleWordRegister> BuildDistributorRegistersMap()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)DistributorRegisters.ControllerType, new DoubleWordRegister(this)
                    .WithValueField(27, 5, name: "SharedPeripheralInterruptsExtendedCount",
                        valueProviderCallback: _ => irqsDecoder.SharedPeripheralExtendedCount / 32 - 1
                    )
                    .WithFlag(26, name: "AffinityLevel0RangeSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(25, name: "1OfNSharedPeripheralInterruptsSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(24, name: "AffinityLevel3Support",
                        valueProviderCallback: _ => false
                    )
                    .WithValueField(19, 5, name: "SupportedInterruptIdentifierBits",
                        valueProviderCallback: _ => irqsDecoder.IdentifierBits - 1
                    )
                    .WithFlag(18, name: "DirectVirtualLocalitySpecificPeripheralInterruptInjectionSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(17, name: "LocalitySpecificPeripheralInterruptSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(16, name: "MessageBasedInterruptActivationByWriteSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithReservedBits(11, 5) // Indicates the lack of the Locality-specific Peripheral Interrupt support
                    .WithFlag(10, name: "SecurityStateSupport",
                        valueProviderCallback: _ => !DisabledSecurity
                    )
                    .WithFlag(9, name: "NonMaskableInterruptSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(8, name: "SharedPeripheralInterruptsExtendedSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithValueField(5, 3, name: "CPUsCountWithoutAffinityRouting",
                        valueProviderCallback: _ => CPUsCountWithoutAffinityRouting - 1
                    )
                    .WithValueField(0, 5, name: "SharedPeripheralInterruptsCount",
                        valueProviderCallback: _ => ((uint)irqsDecoder.SharedPeripheralLast + 1) / 32 - 1
                    )
                },
                {(long)DistributorRegisters.ImplementerIdentification, new DoubleWordRegister(this)
                    .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => ProductIdentifier, name: "ProductIdentifier")
                    .WithReservedBits(20, 4)
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => DistributorVariant, name: "VariantNumber")
                    .WithValueField(12, 4, FieldMode.Read, valueProviderCallback: _ => DistributorRevision, name: "RevisionNumber")
                    .WithValueField(0, 12, FieldMode.Read, valueProviderCallback: _ => DistributorImplementer, name: "ImplementerIdentification")
                },
                {GetPeripheralIdentificationOffset(), new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithEnumField<DoubleWordRegister, ARM_GenericInterruptControllerVersion>(4, 4, FieldMode.Read, name: "ArchitectureVersion",
                        valueProviderCallback: _ => ArchitectureVersion
                    )
                    .WithTag("ImplementiationDefinedIdentificator", 0, 4)
                }
            };

            // All BuildInterrupt*Registers methods create registers with respect for Security State
            // There is no separate view (RegistersCollection) for this kind of registers, because their layout are independent of Security State
            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptSetEnable_0,
                BuildInterruptSetEnableRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptSetEnable")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptClearEnable_0,
                BuildInterruptClearEnableRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptClearEnable")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptPriority_0,
                BuildInterruptPriorityRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptPriority")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptProcessorTargets_0,
                BuildPrivateInterruptTargetsRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.PrivatePeripheralLast, "InterruptProcessorTargets")
            );
            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptProcessorTargets_8,
                BuildSharedInterruptTargetsRegisters(irqsDecoder.SharedPeripheralFirst, irqsDecoder.SharedPeripheralLast, "InterruptProcessorTargets")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptConfiguration_0,
                BuildInterruptConfigurationRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SoftwareGeneratedLast, "InterruptConfiguration", isReadonly: true)
            );
            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptConfiguration_1,
                BuildInterruptConfigurationRegisters(irqsDecoder.PrivatePeripheralFirst, irqsDecoder.SharedPeripheralLast, "InterruptConfiguration")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptSetActive_0,
                BuildInterruptSetActiveRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptSetActive")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptClearActive_0,
                BuildInterruptClearActiveRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptClearActive")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptClearPending_0,
                BuildInterruptClearPendingRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptClearPending")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptGroup_0,
                BuildInterruptGroupRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptGroup")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptGroupModifier_0,
                BuildInterruptGroupModifierRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptGroupModifier")
            );

            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildDistributorRegistersMapSecurityView(bool accessForDisabledSecurity, SecurityState? securityStateAccess = null)
        {
            var controlRegister = new DoubleWordRegister(this)
                .WithFlag(31, FieldMode.Read, name: "RegisterWritePending", valueProviderCallback: _ => false);

            Action<bool, bool> warnOnEnablingAffinityRouting = (_, newValue) => { if(newValue) this.Log(LogLevel.Warning, "Affinity routing isn't currently supported"); };

            if(accessForDisabledSecurity)
            {
                controlRegister
                    .WithReservedBits(9, 22)
                    .WithTaggedFlag("nASSGIreq", 8) // Requires FEAT_GICv4p1 support
                    .WithFlag(7, FieldMode.Read, name: "Enable1ofNWakeup", valueProviderCallback: _ => false) // There is no support for waking up
                    .WithFlag(6, FieldMode.Read, name: "DisableSecurity", valueProviderCallback: _ => true)
                    .WithReservedBits(5, 1)
                    // Bit 4 is a flag only to support read-after-write behaviour in software.
                    .WithFlag(4, name: "EnableAffinityRouting", writeCallback: warnOnEnablingAffinityRouting)
                    .WithReservedBits(2, 2)
                    .WithFlag(1, name: "EnableGroup1",
                        writeCallback: (_, val) => groups[GroupType.Group1].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1].Enabled
                    )
                    .WithFlag(0, name: "EnableGroup0",
                        writeCallback: (_, val) => groups[GroupType.Group0].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group0].Enabled
                    );
            }
            else if(securityStateAccess == SecurityState.Secure)
            {
                controlRegister
                    .WithReservedBits(8, 23)
                    .WithFlag(7, FieldMode.Read, name: "Enable1ofNWakeup", valueProviderCallback: _ => false) // There is no support for waking up
                    .WithFlag(6, name: "DisableSecurity",
                        writeCallback: (_, val) => DisabledSecurity = val,
                        valueProviderCallback: _ => DisabledSecurity)
                    // Bits 4-5 are flags only to support read-after-write behaviour in software.
                    .WithFlag(5, name: "EnableAffinityRoutingNonSecure", writeCallback: warnOnEnablingAffinityRouting)
                    .WithFlag(4, name: "EnableAffinityRoutingSecure", writeCallback: warnOnEnablingAffinityRouting)
                    .WithReservedBits(3, 1)
                    .WithFlag(2, name: "EnableGroup1Secure",
                        writeCallback: (_, val) => groups[GroupType.Group1Secure].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1Secure].Enabled
                    )
                    .WithFlag(1, name: "EnableGroup1NonSecure",
                        writeCallback: (_, val) => groups[GroupType.Group1NonSecure].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1NonSecure].Enabled
                    )
                    .WithFlag(0, name: "EnableGroup0",
                        writeCallback: (_, val) => groups[GroupType.Group0].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group0].Enabled
                    );
            }
            else
            {
                controlRegister
                    .WithReservedBits(5, 26)
                    // Bit 4 is a flag only to support read-after-write behaviour in software.
                    .WithFlag(4, name: "EnableAffinityRoutingNonSecure", writeCallback: warnOnEnablingAffinityRouting)
                    .WithReservedBits(2, 2)
                    .WithFlag(1, name: "EnableGroup1NonSecureAlias",
                        writeCallback: (_, val) => groups[GroupType.Group1NonSecure].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1NonSecure].Enabled
                    )
                    .WithFlag(0, name: "EnableGroup1NonSecureAlias",
                        writeCallback: (_, val) => groups[GroupType.Group1NonSecure].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1NonSecure].Enabled
                    );
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)DistributorRegisters.Control, controlRegister}
            };
            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildRedistributorDoubleWordRegistersMap()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)RedistributorRegisters.Control, new DoubleWordRegister(this)
                    .WithFlag(31, FieldMode.Read, name: "UpstreamWritePending",
                        valueProviderCallback: _ => false
                    )
                    .WithReservedBits(27, 4)
                    .WithTaggedFlag("DisableProcessorSelectionGroup1Secure", 26)
                    .WithTaggedFlag("DisableProcessorSelectionGroup1NonSecure", 25)
                    .WithTaggedFlag("DisableProcessorSelectionGroup0", 24)
                    .WithReservedBits(4, 20)
                    .WithFlag(3, FieldMode.Read, name: "RegisterWritePending",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(2, FieldMode.Read, name: "LocalitySpecificInterruptInvalidateSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(1, FieldMode.Read, name: "LocalitySpecificInterruptClearEnableSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithTaggedFlag("LocalitySpecificInterruptEnable", 0)
                },
                {(long)RedistributorRegisters.Wake, new DoubleWordRegister(this, 0x3)
                    // "There is only one GICR_WAKER.Sleep and one GICR_WAKER.Quiescent bit that can be read and written through the GICR_WAKER register of any Redistributor."
                    .WithTaggedFlag("Quiescent", 31)
                    .WithReservedBits(3, 28)
                    .WithFlag(2, FieldMode.Read, name: "ChildrenAsleep",
                        valueProviderCallback: _ => processorSleep.Value
                    )
                    .WithFlag(1, out processorSleep, name: "ProcessorSleep")
                    .WithTaggedFlag("Sleep", 0)
                },
                {GetPeripheralIdentificationOffset(), new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithEnumField<DoubleWordRegister, ARM_GenericInterruptControllerVersion>(4, 4, FieldMode.Read, name: "ArchitectureVersion",
                        valueProviderCallback: _ => ArchitectureVersion
                    )
                    .WithTag("ImplementiationDefinedIdentificator", 0, 4)
                }
            };

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptSetEnable_0,
                BuildInterruptSetEnableRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.PrivatePeripheralLast, "InterruptSetEnable")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptClearEnable_0,
                BuildInterruptClearEnableRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.PrivatePeripheralLast, "InterruptClearEnable")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptClearPending_0,
                BuildInterruptClearPendingRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.PrivatePeripheralLast, "InterruptClearPending")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptPriority_0,
                BuildInterruptPriorityRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.PrivatePeripheralLast, "InterruptPriority")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.PrivatePeripheralInterruptConfiguration,
                BuildInterruptConfigurationRegisters(irqsDecoder.PrivatePeripheralFirst, irqsDecoder.PrivatePeripheralLast, "PrivatePeripheralInterruptConfiguration")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptGroup_0,
                BuildInterruptGroupRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.PrivatePeripheralLast, "InterruptGroup")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptGroupModifier_0,
                BuildInterruptGroupModifierRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.PrivatePeripheralLast, "InterruptGroupModifier")
            );

            return registersMap;
        }

        private Dictionary<long, QuadWordRegister> BuildRedistributorQuadWordRegistersMap()
        {
            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)RedistributorRegisters.ControllerType, new QuadWordRegister(this)
                    .WithValueField(32, 32, FieldMode.Read, name: "CPUAffinity",
                        valueProviderCallback: _ => GetAskingCPU().Affinity.AllLevels
                    )
                    .WithValueField(27, 5, FieldMode.Read, name: "MaximumPrivatePeripheralInterruptIdentifier",
                        valueProviderCallback: _ => 0b00 // The maximum PPI identifier is 31, because the GIC doesn't support an extended range of PPI
                    )
                    .WithFlag(26, FieldMode.Read, name: "DirectSoftwareGEenratedInterruptInjectionSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithTag("LocalitySpecificInterruptConfigurationSharing", 24, 2)
                    .WithValueField(8, 16, FieldMode.Read, name: "ProcessorNumber",
                        valueProviderCallback: _ => GetAskingCPU().Affinity.Level0
                    )
                    .WithTaggedFlag("vPEResidentIndicator", 7)
                    .WithFlag(6, FieldMode.Read, name: "MPAMSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(5, FieldMode.Read, name: "DPGSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(4, FieldMode.Read, name: "HighestRedistributorInSeries",
                        valueProviderCallback: _ => true) // There is no multi core support yet, so the Redistributor instance always is the latest one
                    .WithFlag(3, FieldMode.Read, name: "LocalitySpecificInterruptDirectInjectionSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithTaggedFlag("DirtyBitControl", 2)
                    .WithFlag(1, FieldMode.Read, name: "VirtualLocalitySpecificInterruptSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(0, FieldMode.Read, name: "PhysicalLocalitySpecificInterruptSupport",
                        valueProviderCallback: _ => false
                    )
                }
            };

            return registersMap;
        }

        private Dictionary<long, QuadWordRegister> BuildCPUInterfaceSystemRegistersMap()
        {
            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)CPUInterfaceSystemRegisters.SystemRegisterEnableEL3, new QuadWordRegister(this)
                    .WithReservedBits(4, 60)
                    .WithFlag(3, FieldMode.Read, name: "EnableAccessOnLowerThanEL3", valueProviderCallback: _ => true)
                    .WithFlag(2, FieldMode.Read, name: "DisableIRQBypass", valueProviderCallback: _ => true)
                    .WithFlag(1, FieldMode.Read, name: "DisableFIQBypass", valueProviderCallback: _ => true)
                    .WithFlag(0, FieldMode.Read, name: "EnableSystemRegisterAccess", valueProviderCallback: _ => true)
                },
                {(long)CPUInterfaceSystemRegisters.SystemRegisterEnableEL2, new QuadWordRegister(this)
                    .WithReservedBits(4, 60)
                    .WithFlag(3, FieldMode.Read, name: "EnableAccessOnLowerThanEL2", valueProviderCallback: _ => true)
                    .WithFlag(2, FieldMode.Read, name: "DisableIRQBypass", valueProviderCallback: _ => true)
                    .WithFlag(1, FieldMode.Read, name: "DisableFIQBypass", valueProviderCallback: _ => true)
                    .WithFlag(0, FieldMode.Read, name: "EnableSystemRegisterAccess", valueProviderCallback: _ => true)
                },
                {(long)CPUInterfaceSystemRegisters.SystemRegisterEnableEL1, new QuadWordRegister(this)
                    .WithReservedBits(3, 61)
                    .WithFlag(2, FieldMode.Read, name: "DisableIRQBypass", valueProviderCallback: _ => true)
                    .WithFlag(1, FieldMode.Read, name: "DisableFIQBypass", valueProviderCallback: _ => true)
                    .WithFlag(0, FieldMode.Read, name: "EnableSystemRegisterAccess", valueProviderCallback: _ => true)
                },
                {(long)CPUInterfaceSystemRegisters.GroupEnable0, new QuadWordRegister(this)
                    .WithReservedBits(1, 63)
                    .WithFlag(0, name: "EnableGroup0",
                        valueProviderCallback: _ => GetAskingCPU().Groups[GroupType.Group0].Enabled,
                        writeCallback: (_, val) => GetAskingCPU().Groups[GroupType.Group0].Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.GroupEnable1, new QuadWordRegister(this)
                    .WithReservedBits(1, 63)
                    .WithFlag(0, name: "EnableGroup1",
                        valueProviderCallback: _ => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group1).Enabled,
                        writeCallback: (_, val) => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group1).Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.GroupEnable1EL3, new QuadWordRegister(this)
                    .WithReservedBits(2, 62)
                    .WithFlag(1, name: "EnableGroup1S",
                        valueProviderCallback: _ => GetAskingCPU().Groups[GroupType.Group1Secure].Enabled,
                        writeCallback: (_, val) => GetAskingCPU().Groups[GroupType.Group1Secure].Enabled = val
                    )
                    .WithFlag(0, name: "EnableGroup1NS",
                        valueProviderCallback: _ => GetAskingCPU().Groups[GroupType.Group1NonSecure].Enabled,
                        writeCallback: (_, val) => GetAskingCPU().Groups[GroupType.Group1NonSecure].Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.RunningPriority, new QuadWordRegister(this)
                    .WithTaggedFlag("PriorityFromNonMaskableInterrupt", 63) // Requires FEAT_GICv3_NMI extension
                    .WithTaggedFlag("PriorityFromNonSecureNonMaskableInterrupt", 62) // Requires FEAT_GICv3_NMI extension
                    .WithReservedBits(8, 54)
                    .WithEnumField<QuadWordRegister, InterruptPriority>(0, 8, FieldMode.Read, name: "RunningPriority",
                        valueProviderCallback: _ => GetAskingCPU().RunningPriority
                    )
                },
                {(long)CPUInterfaceSystemRegisters.PriorityMask, new QuadWordRegister(this)
                    .WithReservedBits(8, 56)
                    .WithEnumField<QuadWordRegister, InterruptPriority>(0, 8, name: "PriorityMask",
                        writeCallback: (_, val) => GetAskingCPU().PriorityMask = val,
                        valueProviderCallback: _ => GetAskingCPU().PriorityMask
                    )
                },
                {(long)CPUInterfaceSystemRegisters.InterruptAcknowledgeGroup0, new QuadWordRegister(this)
                    .WithReservedBits(24, 40)
                    .WithValueField(0, 24, FieldMode.Read, name: "InterruptAcknowledgeGroup0",
                        valueProviderCallback: _ => (ulong)GetAskingCPU().AcknowledgeBestPending(GroupTypeRegister.Group0)
                    )
                },
                {(long)CPUInterfaceSystemRegisters.InterruptAcknowledgeGroup1, new QuadWordRegister(this)
                    .WithReservedBits(24, 40)
                    .WithValueField(0, 24, FieldMode.Read, name: "InterruptAcknowledgeGroup1",
                        valueProviderCallback: _ => (ulong)GetAskingCPU().AcknowledgeBestPending(GroupTypeRegister.Group1)
                    )
                },
                {(long)CPUInterfaceSystemRegisters.InterruptDeactivate, new QuadWordRegister(this)
                    .WithReservedBits(24, 40)
                    // EOI mode with priority drop separated from deactivation is yet to be implemented.
                    // Currently both happen after writing InterruptEnd register which has to be done before InterruptDeactivate nevertheless.
                    // This field just prevents logging an unhandled write warning with every interrupt.
                    .WithValueField(0, 24, FieldMode.Write, name: "INTID")
                    .WithWriteCallback((_, __) => this.Log(LogLevel.Noisy, "Separate interrupt deactivation isn't currently supported."))
                },
                {(long)CPUInterfaceSystemRegisters.InterruptEndGroup0, new QuadWordRegister(this)
                    .WithReservedBits(24, 40)
                    .WithValueField(0, 24, FieldMode.Write, name: "InterruptEndGroup0",
                        writeCallback: (_, val) => GetAskingCPU().CompleteRunning(new InterruptId((uint)val), GroupTypeRegister.Group0)
                    )
                },
                {(long)CPUInterfaceSystemRegisters.InterruptEndGroup1, new QuadWordRegister(this)
                    .WithReservedBits(24, 40)
                    .WithValueField(0, 24, FieldMode.Write, name: "InterruptEndGroup1",
                        writeCallback: (_, val) => GetAskingCPU().CompleteRunning(new InterruptId((uint)val), GroupTypeRegister.Group1)
                    )
                }
            };

            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildCPUInterfaceRegistersMap()
        {
            Func<GroupTypeRegister> getRegisterGroupType = () => (DisabledSecurity || GetAskingCPU().IsStateSecure) ? GroupTypeRegister.Group0 : GroupTypeRegister.Group1;
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)CPUInterfaceRegisters.InterfaceIdentification, new DoubleWordRegister(this)
                    .WithValueField(20, 12, FieldMode.Read, valueProviderCallback: _ => ProductIdentifier, name: "ProductIdentifier")
                    .WithEnumField<DoubleWordRegister, ARM_GenericInterruptControllerVersion>(16, 4, FieldMode.Read, valueProviderCallback: _ => ArchitectureVersion, name: "ArchitectureVersion")
                    .WithValueField(12, 4, FieldMode.Read, valueProviderCallback: _ => CPUInterfaceRevision, name: "RevisionNumber")
                    .WithValueField(0, 12, FieldMode.Read, valueProviderCallback: _ => CPUInterfaceImplementer, name: "ImplementerIdentification")
                },
                {(long)CPUInterfaceRegisters.RunningPriority, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithEnumField<DoubleWordRegister, InterruptPriority>(0, 8, FieldMode.Read, name: "RunningPriority",
                        valueProviderCallback: _ => GetAskingCPU().RunningPriority
                    )
                },
                {(long)CPUInterfaceRegisters.PriorityMask, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithEnumField<DoubleWordRegister, InterruptPriority>(0, 8, name: "PriorityMask",
                        writeCallback: (_, val) => GetAskingCPU().PriorityMask = val,
                        valueProviderCallback: _ => GetAskingCPU().PriorityMask
                    )
                },
                {(long)CPUInterfaceRegisters.InterruptAcknowledge, new DoubleWordRegister(this)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24, FieldMode.Read, name: "InterruptAcknowledge",
                        valueProviderCallback: _ => (ulong)GetAskingCPU().AcknowledgeBestPending(getRegisterGroupType())
                    )
                },
                {(long)CPUInterfaceRegisters.InterruptEnd, new DoubleWordRegister(this)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24, FieldMode.Write, name: "InterruptEnd",
                        writeCallback: (_, val) => GetAskingCPU().CompleteRunning(new InterruptId((uint)val), getRegisterGroupType())
                    )
                },
            };

            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildCPUInterfaceRegistersMapSecurityView(bool accessForDisabledSecurity, SecurityState? securityStateAccess = null)
        {
            var controlRegister = new DoubleWordRegister(this);
            if(accessForDisabledSecurity || securityStateAccess == SecurityState.Secure)
            {
                controlRegister
                    .WithFlag(8, FieldMode.Read, name: "IRQBypassGroup1", valueProviderCallback: _ => false)
                    .WithFlag(7, FieldMode.Read, name: "FIQBypassGroup1", valueProviderCallback: _ => false)
                    .WithFlag(6, FieldMode.Read, name: "IRQBypassGroup0", valueProviderCallback: _ => false)
                    .WithFlag(5, FieldMode.Read, name: "FIQBypassGroup0", valueProviderCallback: _ => false)
                    .WithTaggedFlag("PremptionControl", 4)
                    .WithFlag(3, name: "EnableFIQ",
                        writeCallback: (_, val) => enableFIQ = val,
                        valueProviderCallback: _ => enableFIQ
                    )
                    .WithFlag(2, name: "AcknowledgementControl",
                        writeCallback: (_, val) => { if(val) this.Log(LogLevel.Warning, "Setting deprecated GICC_CTLR.AckCtl flag!"); ackControl = val; },
                        valueProviderCallback: _ => ackControl
                    )
                    .WithFlag(1, name: "EnableGroup1",
                        writeCallback: (_, val) => GetAskingCPU().Groups[GroupType.Group1].Enabled = val,
                        valueProviderCallback: _ => GetAskingCPU().Groups[GroupType.Group1].Enabled
                    )
                    .WithFlag(0, name: "EnableGroup0",
                        writeCallback: (_, val) => GetAskingCPU().Groups[GroupType.Group0].Enabled = val,
                        valueProviderCallback: _ => GetAskingCPU().Groups[GroupType.Group0].Enabled
                    );

                if(accessForDisabledSecurity)
                {
                    controlRegister
                        .WithReservedBits(10, 22)
                        .WithTaggedFlag("EndOfInterruptMode", 9);
                }
                else
                {
                    controlRegister
                        .WithReservedBits(11, 21)
                        .WithTaggedFlag("EndOfInterruptModeNonSecure", 10)
                        .WithTaggedFlag("EndOfInterruptModeSecure", 9);
                }
            }
            else
            {
                controlRegister
                    .WithReservedBits(10, 22)
                    .WithTaggedFlag("EndOfInterruptModeNonSecure", 9)
                    .WithReservedBits(7, 2)
                    .WithFlag(6, FieldMode.Read, name: "IRQBypassGroup1", valueProviderCallback: _ => false)
                    .WithFlag(5, FieldMode.Read, name: "FIQBypassGroup1", valueProviderCallback: _ => false)
                    .WithReservedBits(1, 4)
                    .WithFlag(0, name: "EnableGroup1",
                        writeCallback: (_, val) => GetAskingCPU().Groups[GroupType.Group1].Enabled = val,
                        valueProviderCallback: _ => GetAskingCPU().Groups[GroupType.Group1].Enabled
                    );
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)CPUInterfaceRegisters.Control, controlRegister},

                // Aliases for acknowledging/ending non-secure interrupts in secure state.
                {(long)CPUInterfaceRegisters.InterruptAcknowledgeAlias, new DoubleWordRegister(this)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24, FieldMode.Read, name: "InterruptAcknowledgeAlias",
                        valueProviderCallback: _ => (ulong)GetAskingCPU().AcknowledgeBestPending(GroupTypeRegister.Group1)
                    )
                },
                {(long)CPUInterfaceRegisters.InterruptEndAlias, new DoubleWordRegister(this)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24, FieldMode.Read, name: "InterruptEndAlias",
                        writeCallback: (_, val) => GetAskingCPU().CompleteRunning(new InterruptId((uint)val), GroupTypeRegister.Group1)
                    )
                },
            };
            return registersMap;
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptSetEnableRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Enabled |= val,
                valueProviderCallback: irq => irq.Enabled
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptClearEnableRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Enabled &= !val,
                valueProviderCallback: irq => irq.Enabled
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptPriorityRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptEnumRegisters<InterruptPriority>(startId, endId, name, 4,
                writeCallback: (irq, val) => irq.Priority = val,
                valueProviderCallback: irq => irq.Priority
            );
        }

        private IEnumerable<DoubleWordRegister> BuildPrivateInterruptTargetsRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptValueRegisters(startId, endId, name, 4,
                valueProviderCallback: _ => GetAskingCPU().Affinity.TargetFieldFlag
            );
        }

        private IEnumerable<DoubleWordRegister> BuildSharedInterruptTargetsRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptValueRegisters(startId, endId, name, 4,
                writeCallback: (irq, val) => ((SharedInterrupt)irq).TargetCPU = (byte)val,
                valueProviderCallback: irq => ((SharedInterrupt)irq).TargetCPU
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptConfigurationRegisters(InterruptId startId, InterruptId endId, string name, bool isReadonly = false)
        {
            Action<Interrupt, InterruptTriggerType> writeCallback = null;
            if(!isReadonly)
            {
                writeCallback = (irq, val) =>
                {
                    irq.TriggerType = val;
                    if(val != InterruptTriggerType.LevelSensitive && val != InterruptTriggerType.EdgeTriggered)
                    {
                        this.Log(LogLevel.Error, "Setting an unknown interrupt trigger type, value {0}", val);
                    }
                };
            }
            return BuildInterruptEnumRegisters<InterruptTriggerType>(startId, endId, name, 16,
                writeCallback: writeCallback,
                valueProviderCallback: irq => irq.TriggerType
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptSetActiveRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Active |= val,
                valueProviderCallback: irq => irq.Active
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptClearActiveRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Active &= !val,
                valueProviderCallback: irq => irq.Active
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptClearPendingRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Pending &= !val,
                valueProviderCallback: irq => irq.Pending
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptGroupRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.GroupBit = val,
                valueProviderCallback: irq => irq.GroupBit,
                allowAccessWhenNonSecureGroup: false
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptGroupModifierRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) =>
                    {
                        if(!DisabledSecurity)
                        {
                            irq.GroupModifierBit = val;
                        }
                        else
                        {
                            // The Zephyr uses this field as usual, so the log message isn't a warning
                            this.Log(LogLevel.Debug, "The group modifier register is reserved for the disabled security, write ignored.");
                        }
                    },
                valueProviderCallback: irq => irq.GroupModifierBit,
                allowAccessWhenNonSecureGroup: false
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptFlagRegisters(InterruptId startId, InterruptId endId, string name,
            Action<Interrupt, bool> writeCallback = null, Func<Interrupt, bool> valueProviderCallback = null, bool allowAccessWhenNonSecureGroup = true)
        {
            const int BitsPerRegister = 32;
            return BuildInterruptRegisters(startId, endId, BitsPerRegister,
                (register, irqGetter, irqId, fieldIndex) =>
                    {
                        FieldMode fieldMode = 0;
                        Action<bool, bool> writeCallbackWrapped = null;
                        if(writeCallback != null)
                        {
                            fieldMode |= FieldMode.Write;
                            writeCallbackWrapped = (_, val) => { if(CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup)) writeCallback(irqGetter(), val); };
                        }
                        Func<bool, bool> valueProviderCallbackWrapped = null;
                        if(valueProviderCallback != null)
                        {
                            fieldMode |= FieldMode.Read;
                            valueProviderCallbackWrapped = _ => CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup) ? valueProviderCallback(irqGetter()) : false;
                        }
                        register.WithFlag(fieldIndex, fieldMode, name: $"{name}_{(uint)irqId}",
                            writeCallback: writeCallbackWrapped, valueProviderCallback: valueProviderCallbackWrapped);
                    },

                (register, fieldIndex) => register.WithReservedBits(fieldIndex, 1)
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptValueRegisters(InterruptId startId, InterruptId endId, string name, int fieldsPerRegister,
            Action<Interrupt, ulong> writeCallback = null, Func<Interrupt, ulong> valueProviderCallback = null, bool allowAccessWhenNonSecureGroup = true)
        {
            const int registerWidth = 32;
            var fieldWidth = registerWidth / fieldsPerRegister;
            return BuildInterruptRegisters(startId, endId, fieldsPerRegister,
                (register, irqGetter, irqId, fieldIndex) =>
                    {
                        FieldMode fieldMode = 0;
                        Action<ulong, ulong> writeCallbackWrapped = null;
                        if(writeCallback != null)
                        {
                            fieldMode |= FieldMode.Write;
                            writeCallbackWrapped = (_, val) => { if(CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup)) writeCallback(irqGetter(), val); };
                        }
                        Func<ulong, ulong> valueProviderCallbackWrapped = null;
                        if(valueProviderCallback != null)
                        {
                            fieldMode |= FieldMode.Read;
                            valueProviderCallbackWrapped = _ => CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup) ? valueProviderCallback(irqGetter()) : 0;
                        }
                        register.WithValueField(fieldIndex * fieldWidth, fieldWidth, fieldMode, name: $"{name}_{(uint)irqId}",
                            writeCallback: writeCallbackWrapped, valueProviderCallback: valueProviderCallbackWrapped);
                    },
                (register, fieldIndex) => register.WithReservedBits(fieldIndex * fieldWidth, fieldWidth)
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptEnumRegisters<TEnum>(InterruptId startId, InterruptId endId, string name, int fieldsPerRegister,
            Action<Interrupt, TEnum> writeCallback = null, Func<Interrupt, TEnum> valueProviderCallback = null, bool allowAccessWhenNonSecureGroup = true) where TEnum : struct, IConvertible
        {
            const int registerWidth = 32;
            var fieldWidth = registerWidth / fieldsPerRegister;
            return BuildInterruptRegisters(startId, endId, fieldsPerRegister,
                (register, irqGetter, irqId, fieldIndex) =>
                    {
                        FieldMode fieldMode = 0;
                        Action<TEnum, TEnum> writeCallbackWrapped = null;
                        if(writeCallback != null)
                        {
                            fieldMode |= FieldMode.Write;
                            writeCallbackWrapped = (_, val) => { if(CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup)) writeCallback(irqGetter(), val); };
                        }
                        Func<TEnum, TEnum> valueProviderCallbackWrapped = null;
                        if(valueProviderCallback != null)
                        {
                            fieldMode |= FieldMode.Read;
                            valueProviderCallbackWrapped = _ => CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup) ? valueProviderCallback(irqGetter()) : default(TEnum);
                        }
                        register.WithEnumField<DoubleWordRegister, TEnum>(fieldIndex * fieldWidth, fieldWidth, fieldMode, name: $"{name}_{(uint)irqId}",
                            writeCallback: writeCallbackWrapped, valueProviderCallback: valueProviderCallbackWrapped);
                    },
                (register, fieldIndex) => register.WithReservedBits(fieldIndex * fieldWidth, fieldWidth)
            );
        }

        private bool CheckInterruptAccess(Func<Interrupt> irqGetter, bool allowAccessWhenNonSecureGroup)
        {
            if(DisabledSecurity || GetAskingCPU().IsStateSecure
                || (allowAccessWhenNonSecureGroup && irqGetter().GroupType == GroupType.Group1NonSecure))
            {
                return true;
            }
            this.Log(LogLevel.Debug, "Trying to access a field of the interrupt {0}, which isn't accessible from the current security state.", irqGetter().Identifier);
            return false;
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptRegisters(InterruptId startId, InterruptId endId, int fieldsPerRegister,
            Action<DoubleWordRegister, Func<Interrupt>, InterruptId, int> fieldAction,
            Action<DoubleWordRegister, int> fieldPlaceholderAction
        )
        {
            var interruptsCount = (int)endId - (int)startId + 1;
            var registersCount = (interruptsCount + fieldsPerRegister - 1) / fieldsPerRegister;
            var fieldIndex = 0;
            foreach(var registerFirstIrqId in InterruptId.GetRange(startId, endId, (uint)fieldsPerRegister))
            {
                var register = new DoubleWordRegister(this);
                var fieldsCount = fieldsPerRegister;
                if(fieldIndex + fieldsCount > interruptsCount)
                {
                    fieldsCount = interruptsCount % fieldsPerRegister;
                }
                foreach(var irqId in InterruptId.GetRange(registerFirstIrqId, new InterruptId((uint)registerFirstIrqId + (uint)fieldsCount - 1)))
                {
                    var inRegisterIndex = fieldIndex % fieldsPerRegister;
                    if(irqsDecoder.Type(irqId) == InterruptType.Reserved)
                    {
                        fieldPlaceholderAction(register, inRegisterIndex);
                    }
                    else if(irqsDecoder.IsSoftwareGenerated(irqId) || irqsDecoder.IsPrivatePeripheral(irqId))
                    {
                        fieldAction(register, () => GetAskingCPU().Interrupts[irqId], irqId, inRegisterIndex);
                    }
                    else
                    {
                        fieldAction(register, () => sharedInterrupts[irqId], irqId, inRegisterIndex);
                    }
                    fieldIndex++;
                }
                // Always fill the whole register
                for(; (fieldIndex % fieldsPerRegister) != 0; fieldIndex++)
                {
                    fieldPlaceholderAction(register, fieldIndex % fieldsPerRegister);
                }

                yield return register;
            }
        }

        private long GetPeripheralIdentificationOffset()
        {
            return (ArchitectureVersion <= ARM_GenericInterruptControllerVersion.GICv2) ?
                (long)RedistributorRegisters.PeripheralIdentification2_v1v2 : (long)RedistributorRegisters.PeripheralIdentification2_v3v4;
        }

        private CPUEntry GetAskingCPU()
        {
            return GetCPUByConnectionId(0);
        }

        private CPUEntry GetCPUByConnectionId(uint cpuConnectionId)
        {
            if(!cpuEntries.ContainsKey(cpuConnectionId))
            {
                throw new RecoverableException($"There is no CPU with a connection id equal to {cpuConnectionId}.");
            }
            return cpuEntries[cpuConnectionId];
        }

        private void AddRegistersAtOffset(Dictionary<long, DoubleWordRegister> registersMap, long offset, IEnumerable<DoubleWordRegister> registers)
        {
            foreach(var register in registers)
            {
                if(registersMap.ContainsKey(offset))
                {
                    throw new ConstructionException($"The register map already constains register at 0x{offset:x} offset.");
                }
                registersMap[offset] = register;
                offset += BytesPerDoubleWordRegister;
            }
        }

        private bool TryWriteByteToDoubleWordCollection(DoubleWordRegisterCollection registers, long offset, uint value)
        {
            AlignByteRegisterOffset(offset, BytesPerDoubleWordRegister, out var allignedOffset, out var byteShift);
            var registerExists = registers.TryRead(allignedOffset, out var currentValue);
            if(registerExists)
            {
                BitHelper.UpdateWithShifted(ref currentValue, value, byteShift, BitsPerByte);
                registerExists &= registers.TryWrite(allignedOffset, currentValue);
            }
            return registerExists;
        }

        private bool TryReadByteFromDoubleWordCollection(DoubleWordRegisterCollection registers, long offset, out byte value)
        {
            AlignByteRegisterOffset(offset, BytesPerDoubleWordRegister, out var registerOffset, out var byteShift);
            var registerExists = registers.TryRead(registerOffset, out var registerValue);
            value = (byte)(registerValue >> byteShift);
            return registerExists;
        }

        private void AlignByteRegisterOffset(long offset, int bytesPerRegister, out long allignedOffset, out int byteShift)
        {
            var byteOffset = (int)(offset % bytesPerRegister);
            allignedOffset = offset - byteOffset;
            byteShift = byteOffset * BitsPerByte;
        }

        private void LogWriteAccess(bool registerExists, object value, string collectionName, long offset, object prettyOffset)
        {
            if(!registerExists)
            {
                this.Log(LogLevel.Warning, "Unhandled write to 0x{0:X} register of {1}, value 0x{2:X}.", offset, collectionName, value);
            }
            this.Log(LogLevel.Noisy, "{0} writes to 0x{1:X} ({2}) register of {3}, value 0x{4:X}.", GetAskingCPU().Name, offset, prettyOffset, collectionName, value);
        }

        private void LogReadAccess(bool registerExists, object value, string collectionName, long offset, object prettyOffset)
        {
            if(!registerExists)
            {
                this.Log(LogLevel.Warning, "Unhandled read from 0x{0:X} register of {1}.", offset, collectionName);
            }
            this.Log(LogLevel.Noisy, "{0} reads from 0x{1:X} ({2}) register of {3}, returned 0x{4:X}.", GetAskingCPU().Name, offset, prettyOffset, collectionName, value);
        }

        private bool TryWriteRegisterSecurityView(long offset, uint value, DoubleWordRegisterCollection notBankedRegisters,
            DoubleWordRegisterCollection secureRegisters, DoubleWordRegisterCollection nonSecureRegisters, DoubleWordRegisterCollection disabledSecurityRegisters)
        {
            bool registerExists;
            if(DisabledSecurity)
            {
                registerExists = disabledSecurityRegisters.TryWrite(offset, value);
            }
            else if(GetAskingCPU().IsStateSecure)
            {
                registerExists = secureRegisters.TryWrite(offset, value);
            }
            else
            {
                registerExists = nonSecureRegisters.TryWrite(offset, value);
            }

            return registerExists || notBankedRegisters.TryWrite(offset, value);
        }

        private bool TryReadRegisterSecurityView(long offset, out uint value, DoubleWordRegisterCollection notBankedRegisters,
            DoubleWordRegisterCollection secureRegisters, DoubleWordRegisterCollection nonSecureRegisters, DoubleWordRegisterCollection disabledSecurityRegisters)
        {
            bool registerExists;
            if(DisabledSecurity)
            {
                registerExists = disabledSecurityRegisters.TryRead(offset, out value);
            }
            else if(GetAskingCPU().IsStateSecure)
            {
                registerExists = secureRegisters.TryRead(offset, out value);
            }
            else
            {
                registerExists = nonSecureRegisters.TryRead(offset, out value);
            }

            return registerExists || notBankedRegisters.TryRead(offset, out value);
        }

        private bool IsDistributorByteAccessible(long offset)
        {
            return IsByteOffsetInDistributorRegistersRange(offset, DistributorRegisters.InterruptPriority_0, DistributorRegisters.InterruptPriority_254)
                || IsByteOffsetInDistributorRegistersRange(offset, DistributorRegisters.InterruptProcessorTargets_0, DistributorRegisters.InterruptProcessorTargets_254)
                || IsByteOffsetInDistributorRegistersRange(offset, DistributorRegisters.SoftwareGeneratedIntrruptClearPending_0, DistributorRegisters.SoftwareGeneratedIntrruptClearPending_3)
                || IsByteOffsetInDistributorRegistersRange(offset, DistributorRegisters.SoftwareGeneratedIntrruptSetPending_0, DistributorRegisters.SoftwareGeneratedIntrruptSetPending_3);
        }

        private bool IsByteOffsetInDistributorRegistersRange(long offset, DistributorRegisters startOffset, DistributorRegisters endOffset)
        {
            const long maxByteOffset = 3;
            return (long)startOffset <= offset && offset <= (long)endOffset + maxByteOffset;
        }

        private bool IsRedistributorByteAccessible(long offset)
        {
            const long maxByteOffset = 3;
            return (long)RedistributorRegisters.InterruptPriority_0 <= offset && offset <= (long)RedistributorRegisters.InterruptPriority_7 + maxByteOffset;
        }

        private bool ackControl;
        private bool enableFIQ;
        private bool disabledSecurity;

        // This field should be redistributor-specific.
        private IFlagRegisterField processorSleep;

        private readonly Object locker = new Object();
        private readonly Dictionary<uint, CPUEntry> cpuEntries;
        private readonly InterruptSignalType[] supportedInterruptSignals;
        private readonly ReadOnlyDictionary<InterruptId, SharedInterrupt> sharedInterrupts;
        private readonly ReadOnlyDictionary<GroupType, InterruptGroup> groups;
        private readonly DoubleWordRegisterCollection distributorRegisters;
        private readonly DoubleWordRegisterCollection distributorRegistersSecureView;
        private readonly DoubleWordRegisterCollection distributorRegistersNonSecureView;
        private readonly DoubleWordRegisterCollection distributorRegistersDisabledSecurityView;
        private readonly DoubleWordRegisterCollection redistributorDoubleWordRegisters;
        private readonly QuadWordRegisterCollection redistributorQuadWordRegisters;
        private readonly DoubleWordRegisterCollection cpuInterfaceRegisters;
        private readonly DoubleWordRegisterCollection cpuInterfaceRegistersSecureView;
        private readonly DoubleWordRegisterCollection cpuInterfaceRegistersNonSecureView;
        private readonly DoubleWordRegisterCollection cpuInterfaceRegistersDisabledSecurityView;
        private readonly QuadWordRegisterCollection cpuInterfaceSystemRegisters;

        private readonly bool supportsTwoSecurityStates;
        private readonly InterruptsDecoder irqsDecoder;

        private const ARM_GenericInterruptControllerVersion DefaultArchitectureVersion = ARM_GenericInterruptControllerVersion.GICv3;
        private const uint DefaultProductIdentifier = 0x0;
        private const byte DefaultVariantNumber = 0x0;
        private const byte DefaultRevisionNumber = 0x0;
        private const uint DefaultImplementerIdentification = 0x43B; // This value indicates the JEP106 code of the Arm as an implementer

        private const int CPUsCountWithoutAffinityRouting = 1;
        private const int BytesPerDoubleWordRegister = 4;
        private const int BitsPerByte = 8;

        private const long RedistributorPrivateInterruptsFrameOffset = 0x10000;

        private class CPUEntry : IGPIOReceiver
        {
            public CPUEntry(ARM_GenericInterruptController gic, IARMSingleSecurityStateCPU cpu, IEnumerable<GroupType> groupTypes, IReadOnlyDictionary<InterruptSignalType, IGPIO> interruptConnections)
            {
                this.gic = gic;
                this.cpu = cpu;
                Affinity = new CPUAffinity(cpu.Affinity0);
                Name = $"cpu{Affinity.Level0}";
                interruptSignals = interruptConnections;

                var irqIds = InterruptId.GetRange(gic.irqsDecoder.SoftwareGeneratedFirst, gic.irqsDecoder.SoftwareGeneratedLast)
                    .Concat(InterruptId.GetRange(gic.irqsDecoder.PrivatePeripheralFirst, gic.irqsDecoder.PrivatePeripheralLast))
                    .Concat(InterruptId.GetRange(gic.irqsDecoder.ExtendedPrivatePeripheralFirst, gic.irqsDecoder.ExtendedPrivatePeripheralLast));
                Interrupts = new ReadOnlyDictionary<InterruptId, Interrupt>(irqIds.ToDictionary(id => id, id => new Interrupt(id)));

                Groups = new ReadOnlyDictionary<GroupType, InterruptGroup>(groupTypes.ToDictionary(type => type, _ => new InterruptGroup()));
                RunnningInterrupts = new Stack<Interrupt>();
            }

            public void Reset()
            {
                foreach(var irq in Interrupts.Values)
                {
                    irq.Reset();
                }
                foreach(var group in Groups.Values)
                {
                    group.Reset();
                }
                BestPending = null;
                RunnningInterrupts.Clear();
                PriorityMask = InterruptPriority.Idle;
                UpdateSignals();
            }

            // It's expected to pass handling of private interrupts to the ARM_GenericInterruptController class using an event handler
            public void OnGPIO(int number, bool value)
            {
                PrivateInterruptChanged?.Invoke(this, number, value);
            }

            public virtual InterruptId AcknowledgeBestPending(GroupTypeRegister groupTypeRegister)
            {
                var pendingIrq = BestPending;
                if(pendingIrq == null)
                {
                    return gic.irqsDecoder.NoPending;
                }
                var groupType = GetGroupTypeForRegister(groupTypeRegister);
                if(pendingIrq.GroupType != groupType)
                {
                    // In GICv2, Secure (Group 0) access can acknowledge Group 1 interrupt if GICC_CTLR.AckCtl is set. Otherwise, the returned Interrupt ID is 1022 (NoPending=1023).
                    if(gic.ArchitectureVersion == ARM_GenericInterruptControllerVersion.GICv2 && groupTypeRegister == GroupTypeRegister.Group0)
                    {
                        if(!gic.ackControl)
                        {
                            gic.Log(LogLevel.Warning, "Trying to acknowledge pending Group 1 interrupt (#{0}) with secure GIC access while GICC_CTLR.AckCtl isn't set", (uint)pendingIrq.Identifier);
                            return gic.irqsDecoder.NonMaskableInterruptOrGICv2GroupMismatch;
                        }
                    }
                    else
                    {
                        gic.Log(LogLevel.Warning, "Trying to acknowledge pending interrupt using register of an incorrect interrupt group ({0}), expected {1}.", groupType, pendingIrq.GroupType);
                        return gic.irqsDecoder.NoPending;
                    }
                }
                pendingIrq.Acknowledge();
                RunnningInterrupts.Push(pendingIrq);
                BestPending = null;

                return pendingIrq.Identifier;
            }

            public void CompleteRunning(InterruptId id, GroupTypeRegister groupTypeRegister)
            {
                if(RunnningInterrupts.Count == 0)
                {
                    gic.Log(LogLevel.Warning, "Trying to complete interrupt when there is no running one.");
                    return;
                }
                var runningIrq = RunnningInterrupts.Peek();
                var groupType = GetGroupTypeForRegister(groupTypeRegister);
                if(runningIrq.GroupType != groupType)
                {
                    // In GICv2, Secure (Group 0) access can affect Group 1 interrupts if GICC_CTLR.AckCtl is set.
                    if(gic.ArchitectureVersion == ARM_GenericInterruptControllerVersion.GICv2 && groupTypeRegister == GroupTypeRegister.Group0)
                    {
                        if(!gic.ackControl)
                        {
                            gic.Log(LogLevel.Warning, "Trying to complete the running Group 1 interrupt (#{0}) with secure GIC access while GICC_CTLR.AckCtl isn't set, request ignored.", (uint)runningIrq.Identifier);
                            return;
                        }
                    }
                    else
                    {
                        gic.Log(LogLevel.Warning, "Trying to complete the running interrupt using the register of an incorrect interrupt group ({0}), expected {1}, request ignored.", groupType, runningIrq.GroupType);
                        return;
                    }
                }
                if(!runningIrq.Identifier.Equals(id))
                {
                    gic.Log(LogLevel.Error, "Incorrect interrupt identifier for interrupt end, expected INTID {0}, given {1}, request ignored.", runningIrq.Identifier, id);
                    return;
                }
                runningIrq.Active = false;
                // The interrupt are just removed from stack of currently running interrupts
                // It's still accessible using one of the read only collections of interrupts (shared or private ones)
                RunnningInterrupts.Pop();
            }

            public void UpdateSignals()
            {
                if(BestPending == null)
                {
                    foreach(var signal in interruptSignals.Values)
                    {
                        signal.Set(false);
                    }
                    return;
                }

                var signalType = GetBestPendingInterruptSignalType();
                foreach(var signal in interruptSignals)
                {
                    signal.Value.Set(signal.Key == signalType);
                }
            }

            public InterruptGroup GetGroupForRegister(GroupTypeRegister type)
            {
                return Groups[GetGroupTypeForRegister(type)];
            }

            public GroupType GetGroupTypeForRegister(GroupTypeRegister type)
            {
                if(type == GroupTypeRegister.Group0)
                {
                    return GroupType.Group0;
                }
                else if(type == GroupTypeRegister.Group1)
                {
                    var securityState = cpu.SecurityState;
                    if(gic.DisabledSecurity || securityState == SecurityState.NonSecure)
                    {
                        return GroupType.Group1NonSecure;
                    }
                    else if(securityState == SecurityState.Secure)
                    {
                        return GroupType.Group1Secure;
                    }
                }
                throw new ArgumentOutOfRangeException($"There is no valid InterruptGroupType for value: {type}.");
            }

            public event Action<CPUEntry, int, bool> PrivateInterruptChanged;

            public IReadOnlyDictionary<InterruptId, Interrupt> Interrupts { get; }
            public IReadOnlyDictionary<GroupType, InterruptGroup> Groups { get; }
            public CPUAffinity Affinity { get; }
            public string Name { get; }
            public Interrupt BestPending { get; set; }
            public Stack<Interrupt> RunnningInterrupts { get; }
            public InterruptPriority RunningPriority => RunnningInterrupts.Count > 0 ? RunnningInterrupts.Peek().Priority : InterruptPriority.Idle;
            public InterruptPriority PriorityMask { get; set; }
            public bool IsStateSecure => cpu.SecurityState == SecurityState.Secure;

            protected virtual InterruptSignalType GetBestPendingInterruptSignalType()
            {
                if(gic.enableFIQ && BestPending.GroupType == GroupType.Group0)
                {
                    return InterruptSignalType.FIQ;
                }
                return InterruptSignalType.IRQ;
            }

            protected readonly ARM_GenericInterruptController gic;

            private readonly IARMSingleSecurityStateCPU cpu;
            private readonly IReadOnlyDictionary<InterruptSignalType, IGPIO> interruptSignals;
        }

        private class CPUEntryWithTwoSecurityStates : CPUEntry
        {
            public CPUEntryWithTwoSecurityStates(ARM_GenericInterruptController gic, IARMTwoSecurityStatesCPU cpu, IEnumerable<GroupType> groupTypes, IReadOnlyDictionary<InterruptSignalType, IGPIO> interruptConnections)
                : base(gic, cpu, groupTypes, interruptConnections)
            {
                this.cpu = cpu;
            }

            public override InterruptId AcknowledgeBestPending(GroupTypeRegister groupTypeRegister)
            {
                if(BestPending != null && groupTypeRegister == GroupTypeRegister.Group0 && cpu.ExceptionLevel == ExceptionLevel.EL3_MonitorMode)
                {
                    if(BestPending.GroupType == GroupType.Group1Secure)
                    {
                        return gic.irqsDecoder.ExpectedToHandleAtSecure;
                    }
                    else if(BestPending.GroupType == GroupType.Group1NonSecure)
                    {
                        return gic.irqsDecoder.ExpectedToHandleAtNonSecure;
                    }
                }
                return base.AcknowledgeBestPending(groupTypeRegister);
            }

            protected override InterruptSignalType GetBestPendingInterruptSignalType()
            {
                // Based on the "4.6.2 Interrupt assignment to IRQ and FIQ signals" subsection of GICv3 and GICv4 Architecture Specification
                if(cpu.HasSingleSecurityState || gic.DisabledSecurity)
                {
                    return base.GetBestPendingInterruptSignalType();
                }

                var groupType = BestPending.GroupType;
                cpu.GetAtomicExceptionLevelAndSecurityState(out var exceptionLevel, out var securityState);
                if(groupType == GroupType.Group0
                    || (groupType == GroupType.Group1NonSecure && securityState == SecurityState.Secure) // Includes the case when the current EL is EL3_MonitorMode
                    || (groupType == GroupType.Group1Secure && securityState == SecurityState.NonSecure)
                    || (groupType == GroupType.Group1Secure && exceptionLevel == ExceptionLevel.EL3_MonitorMode && !cpu.IsEL3UsingAArch32State))
                {
                    return InterruptSignalType.FIQ;
                }
                return InterruptSignalType.IRQ;
            }

            private readonly IARMTwoSecurityStatesCPU cpu;
        }

        private class Interrupt
        {
            public Interrupt(InterruptId identifier)
            {
                Identifier = identifier;
            }

            public virtual void Reset()
            {
                Enabled = false;
                GroupBit = false;
                GroupModifierBit = false;
                TriggerType = default(InterruptTriggerType);
                State = InterruptState.Inactive;
                Priority = default(InterruptPriority);
            }

            public void Acknowledge()
            {
                if(State == InterruptState.Inactive)
                {
                    throw new InvalidOperationException("It's invalid to acknowledge an interrupt in the inactive state.");
                }
                if(TriggerType == InterruptTriggerType.EdgeTriggered)
                {
                    State = InterruptState.Active;
                }
                else if(TriggerType == InterruptTriggerType.LevelSensitive)
                {
                    State = InterruptState.ActiveAndPending;
                }
            }

            public void AssertAsPending(bool signal)
            {
                if(TriggerType == InterruptTriggerType.EdgeTriggered)
                {
                    Pending |= signal;
                }
                else if(TriggerType == InterruptTriggerType.LevelSensitive)
                {
                    Pending = signal;
                }
            }

            public InterruptId Identifier { get; }
            public bool Enabled { get; set; }
            public bool GroupBit { get; set; }
            public bool GroupModifierBit { get; set; }
            public InterruptTriggerType TriggerType { get; set; }
            public InterruptState State { get; private set; }
            public InterruptPriority Priority { get; set; }

            public bool Pending
            {
                get => (State & InterruptState.Pending) != 0;
                set
                {
                    if(value)
                    {
                        State |= InterruptState.Pending;
                    }
                    else
                    {
                        State &= ~InterruptState.Pending;
                    }
                }
            }

            public bool Active
            {
                get => (State & InterruptState.Active) != 0;
                set
                {
                    if(value)
                    {
                        State |= InterruptState.Active;
                    }
                    else
                    {
                        State &= ~InterruptState.Active;
                    }
                }
            }

            public GroupType GroupType
            {
                get
                {
                    if(GroupBit)
                    {
                        return GroupType.Group1NonSecure;
                    }
                    else if(GroupModifierBit)
                    {
                        return GroupType.Group1Secure;
                    }
                    else
                    {
                        return GroupType.Group0;
                    }
                }
            }
        }

        private class SharedInterrupt : Interrupt
        {
            public SharedInterrupt(InterruptId irqId) : base(irqId) { }

            public override void Reset()
            {
                base.Reset();
                TargetCPU = 0;
            }

            public bool IsTargetingCPU(CPUEntry cpu)
            {
                return true;
                // TODO: Instead of always returning true
                // (which in this case means broadcasting the interrupt to every core available)
                // uncomment line below once affinity routing is properly implemented
                // return (TargetCPU & cpu.Affinity.TargetFieldFlag) != 0;
            }

            public byte TargetCPU { get; set; }
        }

        // This class will be extended at least for the Binary Point register support
        // It may be needed to separate it for CPUInterface and Distributor
        private class InterruptGroup
        {
            public void Reset()
            {
                Enabled = false;
            }

            public bool Enabled { get; set; }
        }

        private struct InterruptId
        {
            public static IEnumerable<InterruptId> GetRange(InterruptId start, InterruptId end, uint step = 1)
            {
                for(var id = (uint)start; id <= (uint)end; id += step)
                {
                    yield return new InterruptId(id);
                }
            }

            public static explicit operator uint(InterruptId id) => id.id;
            public static explicit operator int(InterruptId id) => (int)id.id;

            public InterruptId(uint interruptId)
            {
                id = interruptId;
            }

            public override string ToString()
            {
                return $"{id}";
            }

            private readonly uint id;
        }

        private class InterruptsDecoder
        {
            public InterruptsDecoder(uint sharedPeripheralCount, uint identifierBits)
            {
                this.sharedPeripheralCount = sharedPeripheralCount;
                this.identifierBits = identifierBits;

                sharedPeripheralLast = new InterruptId((uint)SharedPeripheralFirst + sharedPeripheralCount - 1);
                extendedSharedPeripheralLast = new InterruptId((uint)extendedSharedPeripheralFirst + SharedPeripheralExtendedCount - 1);
            }

            public InterruptType Type(InterruptId id)
            {
                if(IsSoftwareGenerated(id))
                {
                    return InterruptType.SoftwareGenerated;
                }
                else if(IsPrivatePeripheral(id))
                {
                    return InterruptType.PrivatePeripheral;
                }
                else if(IsSharedPeripheral(id))
                {
                    return InterruptType.SharedPeripheral;
                }
                else if(IsSpecial(id))
                {
                    return InterruptType.SpecialIdentifier;
                }
                else if(IsLocalitySpecificPeripheral(id))
                {
                    return InterruptType.LocalitySpecificPeripheral;
                }
                return InterruptType.Reserved;
            }

            public bool IsSoftwareGenerated(InterruptId id) => (uint)id <= (uint)SoftwareGeneratedLast;

            public bool IsPrivatePeripheral(InterruptId id) => ((uint)PrivatePeripheralFirst <= (uint)id && (uint)id <= (uint)PrivatePeripheralLast)
                || ((uint)ExtendedPrivatePeripheralFirst <= (uint)id && (uint)id <= (uint)ExtendedPrivatePeripheralLast);

            public bool IsSharedPeripheral(InterruptId id) => ((uint)SharedPeripheralFirst <= (uint)id && (uint)id <= (uint)SharedPeripheralLast)
                || ((uint)ExtendedSharedPeripheralFirst <= (uint)id && (uint)id <= (uint)ExtendedSharedPeripheralLast);

            public bool IsSpecial(InterruptId id) => (uint)ExpectedToHandleAtSecure <= (uint)id && (uint)id <= (uint)NoPending;

            public bool IsLocalitySpecificPeripheral(InterruptId id) => (uint)id <= (uint)LocalitySpecificPeripheralFirst;

            public InterruptId SoftwareGeneratedFirst => softwareGeneratedFirst;
            public InterruptId SoftwareGeneratedLast => softwareGeneratedLast;
            public InterruptId PrivatePeripheralFirst => privatePeripheralFirst;
            public InterruptId PrivatePeripheralLast => privatePeripheralLast;
            public InterruptId SharedPeripheralFirst => sharedPeripheralFirst;
            public InterruptId SharedPeripheralLast => sharedPeripheralLast;
            public InterruptId ExpectedToHandleAtSecure => expectedToHandleAtSecure;
            public InterruptId ExpectedToHandleAtNonSecure => expectedToHandleAtNonSecure;
            public InterruptId NonMaskableInterruptOrGICv2GroupMismatch => nonMaskableInterruptOrGICv2GroupMismatch;
            public InterruptId NoPending => noPending;
            public InterruptId ExtendedPrivatePeripheralFirst => extendedPrivatePeripheralFirst;
            public InterruptId ExtendedPrivatePeripheralLast => extendedPrivatePeripheralLast;
            public InterruptId ExtendedSharedPeripheralFirst => extendedSharedPeripheralFirst;
            public InterruptId ExtendedSharedPeripheralLast => extendedSharedPeripheralLast;
            public InterruptId LocalitySpecificPeripheralFirst => localitySpecificPeripheralFirst;
            public uint IdentifierBits => identifierBits;

            public readonly uint SharedPeripheralExtendedCount = 1024;

            private readonly InterruptId softwareGeneratedFirst = new InterruptId(0);
            private readonly InterruptId softwareGeneratedLast = new InterruptId(15);
            private readonly InterruptId privatePeripheralFirst = new InterruptId(16);
            private readonly InterruptId privatePeripheralLast = new InterruptId(31);
            private readonly InterruptId sharedPeripheralFirst = new InterruptId(32);
            private readonly InterruptId sharedPeripheralLast; // set in the constructor
            private readonly InterruptId expectedToHandleAtSecure = new InterruptId(1020);
            private readonly InterruptId expectedToHandleAtNonSecure = new InterruptId(1021);
            private readonly InterruptId nonMaskableInterruptOrGICv2GroupMismatch = new InterruptId(1022);
            private readonly InterruptId noPending = new InterruptId(1023);
            private readonly InterruptId extendedPrivatePeripheralFirst = new InterruptId(1056);
            private readonly InterruptId extendedPrivatePeripheralLast = new InterruptId(1119);
            private readonly InterruptId extendedSharedPeripheralFirst = new InterruptId(4096);
            private readonly InterruptId extendedSharedPeripheralLast; // set in the constructor
            private readonly InterruptId localitySpecificPeripheralFirst = new InterruptId(819);

            private readonly uint sharedPeripheralCount;
            private readonly uint identifierBits;

            public const uint MaximumSharedPeripheralCount = 988;
        }

        private struct CPUAffinity
        {
            // TODO: Add other affinity levels too
            public CPUAffinity(uint cpuIdentifier)
            {
                Level0 = (byte)cpuIdentifier;
            }

            public byte Level0 { get; }
            public uint AllLevels => Level0;
            public byte TargetFieldFlag => (byte)(1 << Level0);
        }

        private enum InterruptPriority : byte
        {
            Highest = 0x00,
            Idle = 0xFF
        }

        [Flags]
        private enum InterruptState
        {
            Inactive = 0b00,
            Pending = 0b01,
            Active = 0b10,
            ActiveAndPending = Pending | Active
        }

        private enum InterruptTriggerType : byte
        {
            LevelSensitive = 0b00,
            EdgeTriggered = 0b10
        }

        private enum InterruptType
        {
            SoftwareGenerated,
            PrivatePeripheral,
            SharedPeripheral,
            SpecialIdentifier,
            LocalitySpecificPeripheral,
            Reserved
        }

        private enum GroupType
        {
            Group0,
            Group1NonSecure,
            Group1Secure,
            Group1 = Group1NonSecure,
        }

        private enum GroupTypeRegister
        {
            Group0,
            Group1
        }

        private enum DistributorRegisters : long
        {
            Control = 0x0000, // GICD_CTLR
            ControllerType = 0x0004, // GICD_TYPER
            ImplementerIdentification = 0x0008, // GICD_IIDR
            ControllerType2 = 0x000C2, // GICD_TYPER2
            ErrorReportingStatus = 0x0010, // GICD_STATUSR
            SharedPeripheralInterruptSetNonSecure = 0x0040, // GICD_SETSPI_NSR
            SharedPeripheralInterruptClearNonSecure = 0x0048, // GICD_CLRSPI_NSR
            SharedPeripheralInterruptSetSecure = 0x0050, // GICD_SETSPI_SR
            SharedPeripheralInterruptClearSecure = 0x0058, // GICD_CLRSPI_SR
            InterruptGroup_0 = 0x0080, // GICD_IGROUPR<n>
            InterruptSetEnable_0 = 0x0100, // GICD_ISENABLER<n>
            InterruptClearEnable_0 = 0x0180, // GICD_ICENABLER<n>
            InterruptSetPending_0 = 0x0200, // GICD_ISPENDR<n>
            InterruptClearPending_0 = 0x0280, // GICD_ICPENDR<n>
            InterruptSetActive_0 = 0x0300, // GICD_ISACTIVER<n>
            InterruptClearActive_0 = 0x0380, // GICD_ICACTIVER<n>
            InterruptPriority_0 = 0x0400, // GICD_IPRIORITYR<n>
            InterruptPriority_254 = 0x07F8, // GICD_IPRIORITYR<n>
            InterruptProcessorTargets_0 = 0x0800, // GICD_ITARGETSR<n>
            InterruptProcessorTargets_8 = 0x0820, // GICD_ITARGETSR<n>
            InterruptProcessorTargets_254 = 0x0AF8, // GICD_ITARGETSR<n>
            InterruptConfiguration_0 = 0x0C00, // GICD_ICFGR<n>
            InterruptConfiguration_1 = 0x0C04, // GICD_ICFGR<n>
            InterruptGroupModifier_0 = 0x0D00, // GICD_IGRPMODR<n>
            NonSecureAccessControl_0 = 0x0E00, // GICD_NSACR<n>
            SoftwareGeneratedInterruptControl = 0x0F00, // GICD_SGI
            SoftwareGeneratedIntrruptClearPending_0 = 0x0F10, // GICD_CPENDSGIR<n>
            SoftwareGeneratedIntrruptClearPending_3 = 0x0F1C, // GICD_CPENDSGIR<n>
            SoftwareGeneratedIntrruptSetPending_0 = 0x0F20, // GICD_SPENDSGIR<n>
            SoftwareGeneratedIntrruptSetPending_3 = 0x0F2C, // GICD_SPENDSGIR<n>
            NonMaskableInterrupt_0 = 0x0F80, // GICD_INMIR<n>
            PeripheralIdentification2_v1v2 = 0xFE8, // GICD_PIDR2 for GICv1 and GICv2
            SharedPeripheralInterruptExtendedGroup_0 = 0x1000, // GICD_IGROUPR<n>E
            SharedPeripheralInterruptExtendedSetEnable_0 = 0x1200, // GICD_ISENABLER<n>E
            SharedPeripheralInterruptExtendedClearEnable_0 = 0x1400, // GICD_ICENABLER<n>E
            SharedPeripheralInterruptExtendedSetPending_0 = 0x1600, // GICD_ISPENDR<n>E
            SharedPeripheralInterruptExtendedClearPending_0 = 0x1800, // GICD_ICPENDR<n>E
            SharedPeripheralInterruptExtendedSetActive_0 = 0x1A00, // GICD_ISACTIVER<n>E
            SharedPeripheralInterruptExtendedClearActive_0 = 0x1C00, // GICD_ICACTIVER<n>E
            SharedPeripheralInterruptExtendedPriority_0 = 0x2000, // GICD_IPRIORITYR<n>E
            SharedPeripheralInterruptExtendedConfiguration_0 = 0x3000, // GICD_ICFGR<n>E
            SharedPeripheralInterruptExtendedGroupModifier_0 = 0x3400, // GICD_IGRPMODR<n>E
            SharedPeripheralInterruptExtendedNonSecureAccessControl_0 = 0x3600, // GICD_NSACR<n>E
            SharedPeripheralInterruptExtendedNonMaskable_0 = 0x3B00, // GICD_INMIR<n>E
            InterruptRouting_0 = 0x6100, // GICD_IROUTER<n>
            SharedPeripheralInterruptExtendedRouting_0 = 0x8000, // GICD_IROUTER<n>E
            PeripheralIdentification2_v3v4 = 0xFFE8, // GICD_PIDR2 for GICv3 and GICv4
        }

        private enum RedistributorRegisters : long
        {
            Control = 0x0000, // GICR_CTLR
            ImplementerIdentification = 0x0004, // GICR_IIDR
            ControllerType = 0x0008, // GICR_TYPER
            ErrorReportingStatus = 0x0010, // GICR_STATUSR
            Wake = 0x0014, // GICR_WAKER
            MaximumPARTIDAndPMG = 0x0018, // GICR_MPAMIDR
            SetPARTIDAndPMG = 0x001C, // GICR_PARTIDR
            SetLocalitySpecificPeripheralInterruptPending = 0x0040, // GICR_SETLPIR
            ClearLocalitySpecificPeripheralInterruptPending = 0x0048, // GICR_CLRLPIR
            PropertiesBaseAddress = 0x0070, // GICR_PROPBASER
            LocalitySpecificPeripheralInterruptPendingTableBaseAddress = 0x0078, // GICR_PENDBASER
            InvalidateLocalitySpecificPeripheralInterrupt = 0x00A0, // GICR_INVLPIR
            InvalidateAll = 0x00B0, // GICR_INVALLR
            Synchronize = 0x00C0, // GICR_SYNCR
            PeripheralIdentification2_v1v2 = 0xFE8, // GICR_PIDR2 for GICv1 and GICv2

            // Registers from the SGI_base frame
            InterruptGroup_0 = 0x0080 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGROUPR0
            PrivatePeripheralInterruptExtendedGroup_0 = 0x0084 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGROUPR<n>E
            InterruptSetEnable_0 = 0x0100 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISENABLER0
            PrivatePeripheralInterruptExtendedSetEnable_0 = 0x0104 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISENABLER<n>E
            PrivatePeripheralInterruptExtendedClearEnable_0 = 0x0184 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICENABLER<n>E
            InterruptClearEnable_0 = 0x0180 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICENABLER0
            InterruptSetPending0 = 0x0200 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISPENDR0
            PrivatePeripheralInterruptExtendedSetPending_0 = 0x0204 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISPENDR<n>E
            InterruptClearPending_0 = 0x0280 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICPENDR0
            PrivatePeripheralInterruptExtendedClearPending_0 = 0x0284 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICPENDR<n>E
            InterruptSetActive_0 = 0x0300 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISACTIVER0
            PrivatePeripheralInterruptExtendedSetActive_0 = 0x0304 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISACTIVER<n>E
            InterruptClearActive_0 = 0x0380 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICACTIVER0
            PrivatePeripheralInterruptExtendedClearActive_0 = 0x0384 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICACTIVER<n>E
            InterruptPriority_0 = 0x0400 + RedistributorPrivateInterruptsFrameOffset, // GICR_IPRIORITYR<n>
            InterruptPriority_7 = 0x041C + RedistributorPrivateInterruptsFrameOffset, // GICR_IPRIORITYR<n>
            PrivatePeripheralInterruptExtendedPriority_0 = 0x0420 + RedistributorPrivateInterruptsFrameOffset, // GICR_IPRIORITYR<n>E
            SoftwareGeneratedInterruptConfiguration = 0x0C00 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICFGR0
            PrivatePeripheralInterruptConfiguration = 0x0C04 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICFGR1
            PrivatePeripheralInterruptExtendedConfiguration_0 = 0x0C08 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICFGR<n>E
            InterruptGroupModifier_0 = 0x0D00 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGRPMODR0
            PrivatePeripheralInterruptExtendedGroupModifier_0 = 0x0D04 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGRPMODR<n>E
            NonSecureAccessControl = 0x0E00 + RedistributorPrivateInterruptsFrameOffset, // GICR_NSACR
            PrivatePeripheralInterruptNonMaskable = 0x0F80 + RedistributorPrivateInterruptsFrameOffset, // GICR_INMIR0
            PrivatePeripheralInterruptExtendeNonMaskable_0 = 0x0F84 + RedistributorPrivateInterruptsFrameOffset, // GICR_INMIR<n>E
            PeripheralIdentification2_v3v4 = 0xFFE8, // GICR_PIDR2 for GICv3 and GICv4
        }

        private enum CPUInterfaceRegisters : long
        {
            Control = 0x0000, // GICC_CTLR
            PriorityMask = 0x0004, // GICC_PMR
            PriorityBinaryPoint = 0x0008, // GICC_BPR
            InterruptAcknowledge = 0x000C, // GICC_IAR
            InterruptEnd = 0x0010, // GICC_EOIR
            RunningPriority = 0x0014, // GICC_RPR
            HighestPriorityPendingInterrupt = 0x0018, // GICC_HPPIR
            ActivePriority = 0x001C, // GICC_ABPR
            InterruptAcknowledgeAlias = 0x0020, // GICC_AIAR
            InterruptEndAlias = 0x0024, // GICC_AEOIR
            HighestPriorityPendingInterruptAlias = 0x0028, // GICC_AHPPIR
            ErrorReportingStatus = 0x002C, // GICC_STATUSR
            ActivePriorities_0 = 0x00D0, // GICC_APR<n>
            ActivePrioritiesNonSecure_0 = 0x00E0, // GICC_NSAPR<n>
            InterfaceIdentification = 0x00FC, // GICC_IIDR
            InterruptDeactivate = 0x1000, // GICC_DIR
        }

        private enum CPUInterfaceSystemRegisters : long
        {
            // Enum values are created from op0, op1, CRn, CRm and op2 fields of the MRS instruction
            SystemRegisterEnableEL3 = 0xF665, // ICC_SRE_EL3
            SystemRegisterEnableEL1 = 0xC665, // ICC_SRE_EL1
            PriorityMask = 0xC230, // ICC_PMR_EL1
            GroupEnable1 = 0xC667, // ICC_IGRPEN1_EL1
            ActivePriorityGroup0_0 = 0xC644, // ICC_AP0R0_EL1
            ActivePriorityGroup0_1 = 0xC645, // ICC_AP0R1_EL1
            ActivePriorityGroup0_2 = 0xC646, // ICC_AP0R2_EL1
            ActivePriorityGroup0_3 = 0xC647, // ICC_AP0R3_EL1
            ActivePriorityGroup1_0 = 0xC648, // ICC_AP1R0_EL1
            ActivePriorityGroup1_1 = 0xC649, // ICC_AP1R1_EL1
            ActivePriorityGroup1_2 = 0xC64A, // ICC_AP1R2_EL1
            ActivePriorityGroup1_3 = 0xC64B, // ICC_AP1R3_EL1
            SoftwareGeneratedInterruptGroup1GenerateAlias = 0xC65E, // ICC_ASGI1R_EL1
            PriorityBinaryPointGroup0 = 0xC643, // ICC_BPR0_EL1
            PriorityBinaryPointGroup1 = 0xC663, // ICC_BPR1_EL1
            ControlEL1 = 0xC664, // ICC_CTLR_EL1
            ControlEL3 = 0xF664, // ICC_CTLR_EL3
            InterruptDeactivate = 0xC659, // ICC_DIR_EL1
            InterruptEndGroup0 = 0xC641, // ICC_EOIR0_EL1
            InterruptEndGroup1 = 0xC661, // ICC_EOIR1_EL1
            HighestPriorityPendingInterruptGroup0 = 0xC642, // ICC_HPPIR0_EL1
            HighestPriorityPendingInterruptGroup1 = 0xC662, // ICC_HPPIR1_EL1
            InterruptAcknowledgeGroup0 = 0xC640, // ICC_IAR0_EL1
            InterruptAcknowledgeGroup1 = 0xC660, // ICC_IAR1_EL1
            GroupEnable0 = 0xC666, // ICC_IGRPEN0_EL1
            GroupEnable1EL3 = 0xF667, // ICC_IGRPEN1_EL3
            InterruptAcknowladgeNonMaskable = 0xC64D, // ICC_NMIAR1_EL1
            RunningPriority = 0xC65B, // ICC_RPR_EL1
            SoftwareGeneratedInterruptGroup0Generate = 0xC65F, // ICC_SGI0R_EL1
            SoftwareGeneratedInterruptGroup1Generate = 0xC65D, // ICC_SGI1R_EL1
            SystemRegisterEnableEL2 = 0xE64D, // ICC_SRE_EL2
        }
    }

    public enum ARM_GenericInterruptControllerVersion : byte
    {
        GICv1 = 1,
        GICv2 = 2,
        GICv3 = 3,
        GICv4 = 4
    }
}
