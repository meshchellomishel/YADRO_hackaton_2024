*** Variables ***
${SCRIPT}                     ${CURDIR}/../../../scripts/YMP/elct_dpi.resc
${UART}                       sysbus.uart0

*** Keywords ***
Prepare machine
    Execute Script            ${SCRIPT}
    Execute Command           dpi DPIBusConnect ${BUSNUM}

*** Test Cases ***
Run test
    [Documentation]           Execute test.
    Prepare Machine
    Log to console            ELCT prepared
    ${cntd} =  Execute Command  dpi isNotConnected
    WHILE  ${cntd}
        ${cntd} =  Execute Command  dpi isNotConnected
    END
    Log to console            VCS Connected
    Start Emulation
    Log to console            Started
    Wait For Pause            120
    Log to console            Pause waited
    ${retval} =  Execute Command  dpi GetRetVal
    Should Be Equal As Integers   ${retval}    0    Return value should be 0.

