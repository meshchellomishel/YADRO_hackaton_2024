//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.RobotFramework
{
    internal abstract class TestersProvider<TTester, TPeripheral>
        where TPeripheral: class, IEmulationElement
        where TTester: class
    {
        public TestersProvider()
        {
            testers = new Dictionary<int, TTester>();
            peripheralsWithTesters = new List<TPeripheral>();
            EmulationManager.Instance.EmulationChanged += () =>
            {
                lock(testers)
                {
                    testers.Clear();
                    peripheralsWithTesters.Clear();
                }
            };
        }

        public int CreateNewTester(Func<TPeripheral, TTester> creator, string peripheralName, string machine = null)
        {
            lock(testers)
            {
                IMachine machineObject;
                if(machine == null)
                {
                    if(!EmulationManager.Instance.CurrentEmulation.Machines.Any())
                    {
                        throw new KeywordException("There is no machine in the emulation. Could not create tester for peripheral: {0}", peripheralName);
                    }
                    machineObject = EmulationManager.Instance.CurrentEmulation.Machines.Count() == 1
                        ? EmulationManager.Instance.CurrentEmulation.Machines.First()
                        : null;
                    if(machineObject == null)
                    {
                        throw new KeywordException("No machine name provided. Don't know which one to choose. Available machines: [{0}]",
                            string.Join(", ", EmulationManager.Instance.CurrentEmulation.Machines.Select(x => EmulationManager.Instance.CurrentEmulation[x])));
                    }
                }
                else if(!EmulationManager.Instance.CurrentEmulation.TryGetMachineByName(machine, out machineObject))
                {
                    throw new KeywordException("Machine with name {0} not found. Available machines: [{1}]", machine,
                            string.Join(", ", EmulationManager.Instance.CurrentEmulation.Machines.Select(x => EmulationManager.Instance.CurrentEmulation[x])));
                }

                if(!machineObject.TryGetByName(peripheralName, out IPeripheral typeLessPeripheral))
                {
                    throw new KeywordException("Peripheral for machine '{0}' not found or of wrong type: '{1}'. Available peripherals: [{2}]", machine, peripheralName,
                            string.Join(", ", machineObject.GetAllNames()));
                }
                var peripheral = typeLessPeripheral as TPeripheral;
                if(peripheral == null)
                {
                    throw new KeywordException("Peripheral for machine '{0}' not found or of wrong type: '{1}'. Available peripherals: [{2}]", machine, peripheralName,
                            string.Join(", ", machineObject.GetAllNames()));
                }

                var testerId = peripheralsWithTesters.IndexOf(peripheral);
                if(testerId != -1)
                {
                    return testerId;
                }

                var tester = creator(peripheral);
                peripheralsWithTesters.Add(peripheral);
                testers.Add(peripheralsWithTesters.Count - 1, tester);

                return peripheralsWithTesters.Count - 1;
            }
        }

        public void SetDefaultTesterId(int? id)
        {
            lock(testers)
            {
                if(id.HasValue)
                {
                    if(!testers.TryGetValue(id.Value, out var tester))
                    {
                        throw new KeywordException($"Tester #{id.Value} was not found. Create a tester before setting it as default");
                    }
                    defaultTester = tester;
                }
                else
                {
                    defaultTester = null;
                }
            }
        }

        protected TTester GetTesterOrThrowException(int? testerId)
        {
            lock(testers)
            {
                if(testerId == null)
                {
                    if(defaultTester != null)
                    {
                        return defaultTester;
                    }

                    if(testers.Count != 1)
                    {
                        throw new KeywordException(testers.Count == 0
                            ? "There are no testers available."
                            : "There is more than one tester available - please specify ID of the desired tester.");
                    }
                    return testers.Single().Value;
                }

                if(!testers.TryGetValue(testerId.Value, out var tester))
                {
                    throw new KeywordException("Tester for given ID={0} was not found. Did you forget to create the tester?", testerId);
                }
                return tester;
            }
        }

        private TTester defaultTester;
        private readonly Dictionary<int, TTester> testers;
        private readonly List<TPeripheral> peripheralsWithTesters;
    }
}
