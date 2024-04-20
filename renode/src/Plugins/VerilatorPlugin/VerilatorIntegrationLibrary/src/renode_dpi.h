//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#ifndef RENODE_DPI_H
#define RENODE_DPI_H
#include "renode.h"

extern "C"
{
  void renodeDPIConnect(int receiverPort, int senderPort, const char *address);
  void renodeDPIDisconnect();
  bool renodeDPIIsConnected();
  bool renodeDPIReceive(uint32_t *actionId, uint64_t *address, uint64_t *value);
  bool renodeDPISend(uint32_t actionId, uint64_t address, uint64_t value);
  bool renodeDPISendToAsync(uint32_t actionId, uint64_t address, uint64_t value);
  void renodeDPILog(int logLevel, const char *data);
  //dpi uart
  int uart_tx_is_data_available();
  int uart_tx_get_data();
  void uart_rx_new_data(char chr);
  void uart_init();
}

#endif
