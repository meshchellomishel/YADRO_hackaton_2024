*** Variables ***
${ELF_FILE}                         none.elf
${PLATFORM_DESC}                    none.repl
${SYSBUS_MODULE}                    sysbus.none
${SIMULATION_SCRIPT}                none.sh
${LOG_TIMEOUT}                      1
${DEFUALT_TIMEOUT}                  10s

*** Keywords ***
Create Machine With Socket Based Communication
    Execute Command                             using sysbus
    Execute Command                             mach create "yadro"

*** Test Cases ***
# Starting emulation
Should Run
    #Create Log Tester               ${LOG_TIMEOUT}
    Execute Command                 mach create "yadro"
    Execute Command                 machine LoadPlatformDescription @${PLATFORM_DESC}
    ${stdout}=  Execute Command     ${SYSBUS_MODULE} ConnectionParameters
    @{words} =  Split String    ${stdout}       ${SPACE} 
    Log To Console  ${words}[0]
    Log To Console  ${words}[1]
    ${proc}=    Start process   ${SIMULATION_SCRIPT}      ${words}[0]      ${words}[1]     shell=True
    Sleep   2s
    Execute Command                 ${SYSBUS_MODULE} Connect
    Execute Command                 sysbus LoadELF @${ELF_FILE}
    Start Emulation
    ${result}=  Wait For Process    ${proc}    timeout=${DEFUALT_TIMEOUT}

