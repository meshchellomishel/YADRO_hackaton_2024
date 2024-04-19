# APB UART 

Блок представляет собой реализацию UART 16550. Доступ к регистрам UART со стороны процессора осуществляется по APB3 интерфейсу. 

# Описание регистров

Для программирования APB UART используется набор 8-битных регистров, описание которых представлено ниже.

## Register map
 RegisterName | Offset |Access | Reset Value | Description | 
 --- | --- | --- | --- | --- | 
 RBR | 0x0 | r | 0x0 | Receiver Buffer Register |
 THR | 0x0 | w | 0x0 | Transmitter Holding Register |
 DLL | 0x0 | rw | 0x0 | Divisor Latch, LSB. It's accessable when DLAB bit is set |
 IER | 0x1 | rw | 0x0 | Interrupt Enable Register |
 DLM | 0x1 | rw | 0x0 | Divisor Latch, MSB. It's accessable when DLAB bit is set |
 IIR | 0x2 | r | 0xC0 | Interrupt Identification Register |
 FCR | 0x2 | w | 0x0 | FIFO Control Register |
 LCR | 0x3 | rw | 0x0 | Line Control Register |
 LSR | 0x5 | r | 0x60 | Line Status Register |

## Receiver Buffer Register

 Name | Bits |Access | Reset Value | Description
 --- | --- | --- | --- | --- | 
 Rx Data | [7:0] | r | 0x0 | Read Character from Rx FIFO |


## Transmitter Holding Register

 Name | Bits |Access | Reset Value | Description
 --- | --- | --- | --- | --- | 
 Tx Data | [7:0] | w | 0x0 | Write Character to TX FIFO |

## Divisor Latch
The Divisor Latch is a 16-bit register, whose most
significant byte is hold in DLM and its least
significant byte is hold in DLL. The access to these
two registers, located at addresses 1 and 0
respectively, is conditioned to the value of the DLAB
bit in LCR register

### Divisor Latch, LSB

 Name | Bits |Access | Reset Value | Description
 --- | --- | --- | --- | --- | 
 DLL | [7:0] | rw | 0x0 | LSB in Divisor Latch |

### Divisor Latch, MSB

 Name | Bits |Access | Reset Value | Description
 --- | --- | --- | --- | --- | 
 DLM | [7:0] | rw | 0x0 | MSB in Divisor Latch |
 

## Interrupt Enable Register

 Name | Bits |Access | Reset Value | Description
 --- | --- | --- | --- | --- | 
  Data Ready | [0] | rw | 0x0 | Set an interrupt when:  Trigger level reached (Trigger Level field in FCR)  |
  THR empty flag | [1] | rw | 0x0 | Set an interrupt when: Tx FIFO is empty |
  Error flag | [2] | rw | 0x0 | Set an interrupt when: overrun error, parity error, framing error or break interrupt

## Interrupt Identification Register

 Name | Bits |Access | Reset Value | Description
 --- | --- | --- | --- | --- | 
  Data Ready | [3:0] | r | 0x0 | Contatins Id for interrupt:  0b0001 - Errorflag int on 0b0010 - Received data available 0b0100 - THR empty |
  Reserved | [7:4] | r | 0xC | - |

## FIFO Control Register

 Name | Bits |Access | Reset Value | Description
 --- | --- | --- | --- | --- | 
 Rx FIFO Clear | [1] | w | 0x0 | if bit is set, reset Rx FIFO  |
 Tx FIFO clear | [2] | w | 0x0 |  if bit is set, reset Tx FIFO|
 Trigger Level | [7:6] | w | 0x0 | The number of items in the Rx FIFO at which the interrupt is initiated: 0b0 - 1 0b1 - 4 0b10 - 8 0b11 - 14 |

 ## Line Control Register

  Name | Bits |Access | Reset Value | Description
 --- | --- | --- | --- | --- | 
 Len bits| [1:0] | rw | 0x0 | Count of Data bits 0b0 - 5 0b01 - 6 0b10 - 7 0b11 - 8 |
 Stop bits| [2] | rw | 0x0 | Count of Stop bits 0b0 - 1 0b1 - 2 |
 Parity  check| [3] | rw | 0x0 | Use Parity bit if this bit is set |
 DLAB  | [7] | rw | 0x0 | Divisor Latch Access Bit, DLL/DLM accessable when this bit is one|


## Line Status Register

 Name | Bits |Access | Reset Value | Description
 --- | --- | --- | --- | --- | 
 Data availabe flag | [0] | rw | 0x0 | Rx FIFO is not empty|
 Parity Error flag | [2] | rw | 0x0 | Parity error detected|
 Transmitter FIFO empty flag| [5] | rw | 0x0 | Tx FIFO is empty|
 Transmitter Empty  flag| [6] | rw | 0x0 |  Tx FIFO is empty and Uart Tx is in IDLE state |

 
 # Конфигурация UART

Далее будет рассмотрен процесс конфигурации блока APB_UART и передачи данных по UART. Пример можно найти в - /workspaces/gcc-renode-verilator/tests/apb_uart_example. 

Для конфигурации uart необходимо выполнить следующие действия:

  * Установить DLAB бит регистра LCR. Для этого необходимо записать значение 0x80 в LCR

  * Установить необходимый baud rate. Для этого необходимо записать значение в Divisor Latch регистр, который представлен двумя регистрами DLL и DLM. Доступ к этим регистрам возможен только при установленом DLAB. Также, для корректной работы UART значение в DL регистре не должно равняться 0. Итоговый BR можно рассчитать по формуле: $$ BR = Frq/DL$$ 
  BR - итоговый  baud  rate, Frq - тактовая частота, на которой работает UART, DL - значение, записанное в регистр DL в виде: $$DL = [DLM,DLL]$$  
  
  * (Опционально). Установить остальные параметры UART. В примере записывается значение 0x3 в регистр LCR, что будет означать, что данные будут пересылаться в формате 8N1: 8 бит данных, без проверки четности, 1 стоп бит в конце передачи сообщения.

  * (Опционально). Конфигурация прерываний

  * После завершения конфигурации UART необходимо сбросить бит DLAB в регистре LCR. При этом значения в остальных полях регистра не должны перезаписываться.

  * Теперь интерфейс готов для приема/передачи данных


  # Передача данных по опросу

  С блоком APB_UART можно работать в режиме "по опросу". Для работы в таком режиме Прерывания в UART не устанавливаются.

  ## Отправка данных
  * Для отправки данных необходимо записать данные в THR регистр. Опрашивая биты  Transmitter FIFO empty flag и Transmitter Empty  flag регистра LSR можно определить, выполняет ли передачу данных UART. 
  * При поптыке отправить данные по UART, когда Tx FIFO заполнена, передаваемые данные будут проигнорированы. 

  ## Получение данных
  * Необходимо дождаться, пока не прибудут какие то данные в Rx FIFO. Для этого необходимо дождаться, когда установится бит Data availabe flag регистра LSR.
  * Для получения данных из Rx FIFO необходимо инициировать чтение из RBR регистра. 


 
 # Работа с прерываниями.

 ## Конфигурация прерываний
  * Для раазрешения прерываний необходимо установить в регистре IER соответсвтующие биты для разрешения конкретных типов прерываний (Data Ready, THR empty, Error) 
  * При получении прерывания в соответсвующем обработчике прерываний необходимо:
    * Запретить прерывания по UART: сбросить соответсвующие биты в регистре IER
    * Очистить существующую линию прерывания по UART:
      * Для прерывания Data Ready - чтение соответсвующего числа символов из RBR
      * Для прерывания THR Empty - чтение IIR
      * Для прерывания Eror - чтение LSR 
    *  Основная работа обработчика прерываний (определяется пользователем)
    * (Опционально) - разрешение прерываний по UART: установить соответсвующие биты в регистре IER

## Пример
  * Пример работы с прерываниями показан в - /workspaces/gcc-renode-verilator/tests/apb_uart_irq_check

