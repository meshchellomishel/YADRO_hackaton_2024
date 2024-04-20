//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

#include "renode_dpi.h"
#include "communication/socket_channel.h"

#include <stdbool.h>
// #include "/home/fs.studymail/system_verification2024/common/sc_print.h"
#include "../../../../../../../system_verification2024/common/mem.h"




#define UART_BASE_ADDR  0xe0000000
#define UART_REG_RBR ( UART_BASE_ADDR + 0x00) // Receiver Buffer Register (Read Only)
#define UART_REG_DLL ( UART_BASE_ADDR + 0x00) // Divisor Latch (LS)
#define UART_REG_THR ( UART_BASE_ADDR + 0x00) // Transmitter Holding Register (Write Only)
#define UART_REG_DLM ( UART_BASE_ADDR + 0x01) // Divisor Latch (MS)
#define UART_REG_IER ( UART_BASE_ADDR + 0x01) // Interrupt Enable Register
#define UART_REG_IIR ( UART_BASE_ADDR + 0x02) // Interrupt Identity Register (Read Only)
#define UART_REG_FCR ( UART_BASE_ADDR + 0x02) // FIFO Control Register (Write Only)
#define UART_REG_LCR ( UART_BASE_ADDR + 0x03) // Line Control Register
#define UART_REG_MCR ( UART_BASE_ADDR + 0x04) // MODEM Control Register
#define UART_REG_LSR ( UART_BASE_ADDR + 0x05) // Line Status Register
#define UART_REG_MSR ( UART_BASE_ADDR + 0x06) // MODEM Status Register
#define UART_REG_SCR ( UART_BASE_ADDR + 0x07) // Scratch Register


#define UART_LSR_THR_EMPTY      0b01000000
#define UART_LSR_PARITY_ERR     0b01000100
#define UART_LSR_DATA_AVAILABLE 0b00000001

#define UART_TRANSMITTER_FIFO_EMPTY 0b00100000
#define UART_TRANSMITTER_EMPTY 0b01000000


static SocketCommunicationChannel *socketChannel;

bool renodeDPIReceive(uint32_t* actionId, uint64_t* address, uint64_t* value)
{
    if(!socketChannel->getIsConnected())
    {
        return false;
    }
    Protocol *message = socketChannel->receive();
    *actionId = message->actionId;
    *address = message->addr;
    *value = message->value;
    delete message;
    return true;
}

void renodeDPIConnect(int receiverPort, int senderPort, const char* address)
{
    socketChannel = new SocketCommunicationChannel();
    socketChannel->connect(receiverPort, senderPort, address);
}

void renodeDPIDisconnect()
{
    socketChannel->disconnect();
}

bool renodeDPIIsConnected()
{
    return socketChannel->getIsConnected();
}

bool renodeDPISend(uint32_t actionId, uint64_t address, uint64_t value)
{
    if(!socketChannel->getIsConnected())
    {
        return false;
    }
    socketChannel->sendMain(Protocol(actionId, address, value));
    return true;
}

bool renodeDPISendToAsync(uint32_t actionId, uint64_t address, uint64_t value)
{
    if(!socketChannel->getIsConnected())
    {
        return false;
    }
    socketChannel->sendSender(Protocol(actionId, address, value));
    return true;
}

void renodeDPILog(int logLevel, const char* data)
{
    socketChannel->log(logLevel, data);
}


///uart dpi

    int uart_tx_is_data_available()
    {
        uint8_t read = READ_MEMORY(UART_REG_LSR, 8);
        int ret = 0;
        if((read & UART_TRANSMITTER_FIFO_EMPTY)&&(read & UART_TRANSMITTER_EMPTY)) ret = 1;
        return ret;
    }

    int uart_tx_get_data()
    {
        //ввод в uart_requester
        char something = 15;
        return something;
    }

    // void uart_rx_new_data(char chr)
    // {
    //     //вывод из uart_requester
    //     sc_printf("ret_val: %x\n", chr);
    // }

    void uart_rx_is_data_available()
    {
        uint8_t read = 0;
        while (!(read & UART_LSR_DATA_AVAILABLE) || (read & UART_LSR_PARITY_ERR))
        read = READ_MEMORY(UART_REG_LSR, 8);
    }

    void uart_init()
    {
        int key;

        key = UART_REG_LCR;
        WRITE_MEMORY(key, 8, 3 | 0x80);

        key = UART_REG_DLL;
        WRITE_MEMORY(key, 8, 100);

        key = UART_REG_LCR;
        WRITE_MEMORY(key, 8, 3);
    }


    int uart_parity_check()
    {

    }

    int uart_stop_bit()
    {

    }
