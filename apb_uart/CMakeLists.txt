cmake_minimum_required(VERSION 3.8)

# Name of the project
project(apb_uart)

# Verilog file containing the simulation top module
set(SIM_TOP sim)
set(SIM_TOP_FILE rtl/sim.sv) # top file, contains DUT and communication with renode
set(SIM_FILES rtl/apb_uart.sv rtl/uart_rx.sv rtl/uart_tx.sv rtl/uart_interrupt.sv rtl/io_generic_fifo.sv) # uart sources

# Verilator variables
set(VERILATOR_CSOURCES sim/sim_main.cpp)
set(VERILATOR_ARGS -Wno-WIDTH -Wno-CASEINCOMPLETE -Wno-UNSIGNED --timing --trace --coverage-line)

# CMake file doing the hard job
include(cmake/build-cosimulation.cmake)
