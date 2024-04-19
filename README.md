# Структура репозитория

```bash
├───apb_uart
│   ├───cmake
│   ├───rtl
│   │   ├───sim.sv -  Top модуль. Здесь подключаются: renode, apb_uart, uart_requester и другие модули
│   │	├───apb_uart.sv
│   │	├───uart_interrupt.sv
│   │	├───uart_rx.sv
│   │	├───uart_rx.sv
│   │	└───io_generic_fifo.sv
│   └───sim
├───common
│	├───irq.c irq.h - обработка прерываний.
│	├───sc_print.c sc_print.h - реализация ввода/вывода в консоль
│	└───mem.h - обращения к памяти.
├───renode
│   ├───src
│       ├───Plugins
│           ├───VerilatorPlugin
│               └───VerilatorIntegrationLibrary
│                   ├───hdl
│                   │   ├───renode.sv - Обработка входящих запросов по DPI
│                   │   ├───imports
│                   │   │   └───renode_pkg.sv - Реализация DPI функций для взаимодействия Renode и Verilator.
│                   │   └───modules
│                   │       ├───apb3
│                   │       ├───axi
│                   │       ├───uart
│                   │       │   └───uart_requester.sv - Реализация коннектора DPI для UART, этот модуль предстоит реализовать!
│                   │       └──renode_interrupts.sv - Передача сигналов прерываний между Renode и Verilator.
│                   └───src
│                       ├───buses
│                       ├───communication
│                       └───peripherals
│
│       
│       
├───tests - Директория с тестами UART
│   └───apb_uart_example
│   	├───app.c - код теста
│	└───Makefile - мейкфайл теста
└───Makefile - Корневой мейкфайл проекта с таргетами для сборки и запуска тестов

```