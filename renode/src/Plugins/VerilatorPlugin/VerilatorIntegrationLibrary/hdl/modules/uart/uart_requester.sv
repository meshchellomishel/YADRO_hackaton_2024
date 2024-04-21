module uart_requester #(parameter DATA_WIDTH = 8)(
    input logic clk,
    renode_apb3_if cfg_bus_connection,
    input renode_pkg::uart_connection communication_bus_connection,
    output logic tx_o,
    input logic rx_i
);

//=====================================================================//
//                                                                     //
//                                                                     //
//                                                                     //
//                                                                     //                                                                   //
//               Very strange errors here                              //                                                                   //
//                                                                     //                                                                   //
//                                                                     //                                                                 //
//                                                                     //                                                                 //
//                                                                     //                                                                  //
//                                                                     //
//=====================================================================//


    // import "DPI-C" function int uart_tx_is_data_available();
    // import "DPI-C" function int uart_tx_get_data();
    // //import "DPI-C" function void uart_rx_new_data(input integer chr);
    //import "DPI-C" function void uart_init();


    typedef logic [cfg_bus_connection.AddressWidth-1:0] address_t;
    typedef logic [cfg_bus_connection.DataWidth-1:0] data_t;

    // Renaming the bus is a style preference
    // assign clk = cfg_bus_connection.pclk;

    logic rst_n;
    assign cfg_bus_connection.presetn = rst_n;

    address_t paddr;
    logic     pselx;
    logic     penable;
    logic     pwrite;
    data_t    pwdata;
    logic     pready;
    data_t    prdata;
    logic     pslverr;

    assign cfg_bus_connection.paddr = paddr;
    assign cfg_bus_connection.pselx = pselx;
    assign cfg_bus_connection.penable = penable;
    assign cfg_bus_connection.pwrite = pwrite;
    assign cfg_bus_connection.pwdata = pwdata;

    assign pready = cfg_bus_connection.pready;
    assign prdata = cfg_bus_connection.prdata;
    assign pslverr = cfg_bus_connection.pslverr;

    //int unsigned b2b_counter;
    address_t write_address;
    address_t read_address;
    data_t send_data;
    data_t write_data;

    logic start_transaction;
    logic write_mode;

    // Only value of 1 is currently supported
    // localparam int unsigned Back2BackNum = 1;

    // Internal state
    typedef enum {
        S_IDLE,
        S_SETUP,
        S_ACCESS
    } state_t;
    state_t state = S_IDLE;

    // initial
    // begin
    //     uart_init();
    //     // send_data = data_t'(98);
    //     // communication_bus_connection.write_to_peripheral(send_data);
    // end


    // description about receive UART signal
    typedef enum logic [1:0] {STT_DATA,
                                STT_STOP,
                                STT_WAIT
                              } statetype;
    statetype                 state_rx;
    logic [DATA_WIDTH-1:0]   data_tmp_r;
    int clk_cnt = 8;
    logic rx_done;

    always_ff @(posedge clk) begin
        if(!rst_n) begin
            state_rx      = STT_WAIT;
            data_tmp_r = 0;
            clk_cnt    = 0;
        end
        else begin
            case(state_rx)
            // state_rx      : STT_DATA
            // behavior   : deserialize and recieve data
            // next state : when all data have recieved -> STT_STOP
            STT_DATA: begin
                if(0 < clk_cnt) begin
                    clk_cnt = clk_cnt - 1;
                    data_tmp_r[clk_cnt] = rx_i;
                end
                else begin
                    state_rx = STT_STOP;
                end
            end
            // state_rx      : STT_STOP
            // behavior   : watch stop bit
            // next state : STT_WAIT
            STT_STOP: begin
                if(0 < clk_cnt) begin
                    clk_cnt = clk_cnt - 1;
                end
                else if(tx_o) begin
                    state_rx = STT_WAIT;
                end
            end
            // state_rx      : STT_WAIT
            // behavior   : watch start bit
            // next state : when start bit is observed -> STT_DATA
            STT_WAIT: begin
                if(tx_o == 0) begin
                    clk_cnt = 8;
                    state_rx = STT_DATA;
              end
            end
            default: begin
                state_rx = STT_WAIT;
            end
            endcase
        end
    end

    assign rx_done = (state_rx == STT_STOP);


    // always_ff @(posedge clk) begin
    initial begin
        send_data = data_t'(97);
        communication_bus_connection.write_to_peripheral(send_data);
    end
/*
    always @(new_command) begin
        case(new_command)
        0: send_data = data_t'(97); //a
        1: send_data = data_t'(98); //b
        2: send_data = data_t'(99); //c
        endcase
        communication_bus_connection.write_to_peripheral(send_data);
    end
*/
    // RX
    always @(communication_bus_connection.write_transaction_request) begin
        // write_address = address_t'(communication_bus_connection.write_transaction_address);
        write_data = data_t'(communication_bus_connection.write_transaction_data);
        write_mode = 1'b1;
        start_transaction = 1'b1;

        @(posedge clk) start_transaction <= 1'b0;
        communication_bus_connection.write_respond();  // Notify Renode that write is done
    end
/*
     //   TX
    always @(communication_bus_connection.read_transaction_request) begin
        assert (communication_bus_connection.read_transaction_data == renode_pkg::DoubleWord)
        else begin
        communication_bus_connection.fatal_error("Read transaction data bits must be DoubleWord.");
        communication_bus_connection.read_respond(1);
        endcommunication_bus_connection.write_transaction_request
        //read_address = address_t'(communication_bus_connection.read_transaction_address);
        write_mode = 1'b0;
        start_transaction = 1'b1;
        @(posedge clk) start_transaction <= 1'b0;
    end
*/

    // description about transmit UART signal
int data_cnt = 6;
int clk_cnt_tx = 8;
statetype                 state_tx;
logic                      sig_r;
logic [DATA_WIDTH-1:0]     data_r;
logic ready_r;

always_ff @(posedge clk) begin
      if(!rst_n) begin
         state_tx    = STT_WAIT;
         data_r   = 0;
         ready_r  = 1;
         data_cnt = 0;
         clk_cnt_tx  = 0;
      end
      else begin
         case(state)
           // state_tx      : STT_DATA
           // behavior   : serialize and transmit data
           // next state : when all data have transmited -> STT_STOP
           STT_DATA: begin
              if(0 < clk_cnt_tx) begin
                 clk_cnt_tx = clk_cnt_tx - 1;
              end
              else begin
                 sig_r   = data_r[data_cnt];
                 clk_cnt_tx = 1;

                 if(data_cnt == DATA_WIDTH - 1) begin
                    state_tx = STT_STOP;
                 end
                 else begin
                    data_cnt = data_cnt + 1;
                 end
              end
           end

           // state_tx      : STT_STOP
           // behavior   : assert stop bit
           // next state : STT_WAIT
           STT_STOP: begin
              if(0 < clk_cnt_tx) begin
                 clk_cnt_tx = clk_cnt_tx - 1;
              end
              else begin
                 state_tx   = STT_WAIT;
                 sig_r   = 1;
                 clk_cnt_tx = 8;
              end
           end
           // state_tx      : STT_WAIT
           // behavior   : watch valid signal, and assert start bit when valid signal assert
           // next state : when valid signal assert -> STT_STAT
           STT_WAIT: begin
              if(0 < clk_cnt_tx) begin
                 clk_cnt_tx = clk_cnt_tx - 1;
              end
              else if(!ready_r) begin
                 ready_r = 1;
              end
           end

           default: begin
              state_tx = STT_WAIT;
           end
         endcase
      end
   end


/*
    state_t next_state;
    always_comb begin : proc_next_state
        case (state)
        S_IDLE: begin
            if (start_transaction) begin
            next_state = S_SETUP;
            end else begin
            next_state = S_IDLE;
            end
        end
        S_SETUP: begin
            next_state = S_ACCESS;
        end
        S_ACCESS: begin
            if (pready) begin
            if (b2b_counter == 0) begin
                next_state = S_IDLE;
            end else begin
                next_state = S_SETUP;
            end
            end else begin
            next_state = S_ACCESS;
            end
        end
        default: begin
            next_state = S_IDLE;
        end
        endcase
    end


    always_ff @(posedge clk or negedge rst_n) begin
        if (rst_n == '0) begin
            state <= S_IDLE;
        end else begin
            state <= next_state;

            case (state)
            S_IDLE: begin
                b2b_counter <= Back2BackNum;
            end
            S_SETUP: begin
                b2b_counter <= b2b_counter - 1;
            end
            S_ACCESS: begin
                if (pready) begin
                if (write_mode) begin
                    communication_bus_connection.write_respond();  // Notify Renode that write is done
                end else begin
                    communication_bus_connection.read_respond(renode_pkg::data_t'(prdata));
                end
                end
            end
            default: begin
                b2b_counter <= Back2BackNum;
            end
            endcase
        end
    end
*/

    assign tx_o = rx_i;

endmodule
