module uart_requester (
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
    // import "DPI-C" function void uart_init();


    typedef logic [cfg_bus_connection.AddressWidth-1:0] address_t;
    typedef logic [cfg_bus_connection.DataWidth-1:0] data_t;

    // Renaming the bus is a style preference
    // assign clk = cfg_bus_connection.pclk;

    // logic rst_n;
    // assign rst_n = cfg_bus_connection.presetn;

    // address_t paddr;
    // logic     pselx;
    // logic     penable;
    // logic     pwrite;
    // data_t    pwdata;
    // logic     pready;
    // data_t    prdata;
    // logic     pslverr;

    // assign cfg_bus_connection.paddr = paddr;
    // assign cfg_bus_connection.pselx = pselx;
    // assign cfg_bus_connection.penable = penable;
    // assign cfg_bus_connection.pwrite = pwrite;
    // assign cfg_bus_connection.pwdata = pwdata;

    // assign pready = cfg_bus_connection.pready;
    // assign cfg_bus_connection.prdata = prdata;
    // assign pslverr = cfg_bus_connection.pslverr;

    // int unsigned b2b_counter;
    // address_t write_address;
    // address_t read_address;
    // data_t send_data;
    // data_t write_data;
    // data_t rx_data;

    // logic tx_ready;
    // logic tx_valid;

    // data_t word [15:0];

    // logic start_transaction;
    // logic write_mode;
    // logic [31:0] counter;
    // logic [31:0] counte2;
    // // Only value of 1 is currently supported
    // localparam int unsigned Back2BackNum = 1;

    // // Internal state
    // typedef enum {
    //     S_IDLE,
    //     S_SETUP,
    //     S_ACCESS
    // } state_t;

    // state_t next_state;
    // state_t state = S_IDLE;

    // // reg [7:0] tx_reg_r;
    // // initial
    // // begin
    // //     uart_init();
    // // end

    // // always @(clk) begin
    // initial begin
    //     send_data = data_t'(98);
    //     communication_bus_connection.write_to_peripheral(send_data);
    // end

    // parameter RBR = 3'h0, THR = 3'h0, DLL = 3'h0, IER = 3'h1, DLM = 3'h1, IIR = 3'h2,
    //           FCR = 3'h2, LCR = 3'h3, MCR = 3'h4, LSR = 3'h5, MSR = 3'h6, SCR = 3'h7;

    // logic             rx_valid;
    // logic             rx_ready;
    // logic             parity_error;
    // logic [9:0][7:0]  regs_q, regs_n;

    // uart_rx uart_rx_i
    // (
    //     .clk_i              ( clk                           ),
    //     .rstn_i             ( apb.presetn                   ),
    //     .rx_i               ( rx_i                          ),
    //     .cfg_en_i           ( 1'b1                          ),
    //     .cfg_div_i          ( 'd10    ),
    //     .cfg_parity_en_i    ( regs_q[LCR][3]                ),
    //     .cfg_bits_i         ( regs_q[LCR][1:0]              ),
    //     // .cfg_stop_bits_i    ( regs_q[LCR][2]                ),
    //     /* verilator lint_off PINCONNECTEMPTY */
    //     .busy_o             (                               ),
    //     /* lint_on */
    //     .err_o              ( parity_error                  ),
    //     .err_clr_i          ( 1'b0                          ),
    //     .rx_data_o          ( rx_data                       ),
    //     .rx_valid_o         ( rx_valid                      ),
    //     .rx_ready_i         ( rx_ready                      )
    // );

    // uart_tx uart_tx_i
    // (
    //     .clk_i              ( clk                           ),
    //     .rstn_i             ( apb.presetn                   ),
    //     .tx_o               ( tx_o                          ),
    //     /* verilator lint_off PINCONNECTEMPTY */
    //     .busy_o             (                               ),
    //     /* lint_on */
    //     .cfg_en_i           ( 1'b1                          ),
    //     .cfg_div_i          ( 'd5  ),
    //     .cfg_parity_en_i    ( regs_q[LCR][3]                ),
    //     .cfg_bits_i         ( 'd3              ),
    //     .cfg_stop_bits_i    ( 'd1                ),

    //     .tx_data_i          ( pwdata                        ),
    //     .tx_valid_i         ( 1'b0                      ),
    //     .tx_ready_o         ( cfg_bus_connection.pready                      )
    // );

    // // RX
    // always @(communication_bus_connection.write_transaction_request) begin
    //     assert (communication_bus_connection.write_transaction_data == renode_pkg::DoubleWord)
    //     else begin
    //         communication_bus_connection.fatal_error("Write transaction data bits must be DoubleWord.");
    //         communication_bus_connection.write_respond();
    //     end

    //     counter <= counter + 1;
    //     // write_address = address_t'(communication_bus_connection.write_transaction_address);
    //     pwdata <= data_t'(communication_bus_connection.write_transaction_data);
    //     pwrite = 1'b0;
    //     start_transaction = 1'b1;

    //     @(posedge clk) start_transaction <= 1'b0;
    //     communication_bus_connection.write_respond();  // Notify Renode that write is done

    // end

    //  //   TX
    // always @(communication_bus_connection.read_transaction_request) begin
    //     //read_address = address_t'(communication_bus_connection.read_transaction_address);
    //     pwrite = 1'b1;
    //     start_transaction = 1'b1;
    //     @(posedge clk) start_transaction <= 1'b0;
    // end


    // always_comb begin
    //     case (state)
    //     S_IDLE: begin
    //         if (start_transaction) begin
    //         next_state = S_SETUP;
    //         end else begin
    //         next_state = S_IDLE;
    //         end
    //     end
    //     S_SETUP: begin
    //         next_state = S_ACCESS;
    //     end
    //     S_ACCESS: begin
    //         if (pready) begin
    //         if (b2b_counter == 0) begin
    //             next_state = S_IDLE;
    //         end else begin
    //             next_state = S_SETUP;
    //         end
    //         end else begin
    //         next_state = S_ACCESS;
    //         end
    //     end
    //     default: begin
    //         next_state = S_IDLE;
    //     end
    //     endcase
    // end


    // always_ff @(posedge clk or negedge rst_n) begin
    //     if (rst_n == '0) begin
    //         state <= S_IDLE;
    //     end else begin
    //         counte2 = 1;
    //         state <= next_state;

    //         case (state)
    //         S_IDLE: begin
    //             b2b_counter <= Back2BackNum;
    //         end
    //         S_SETUP: begin
    //             b2b_counter <= b2b_counter - 1;
    //         end
    //         S_ACCESS: begin
    //             if (pready) begin
    //             if (write_mode) begin
    //                 communication_bus_connection.write_respond();  // Notify Renode that write is done
    //             end else begin
    //                 communication_bus_connection.read_respond(renode_pkg::data_t'(prdata));
    //             end
    //             end
    //         end
    //         default: begin
    //             b2b_counter <= Back2BackNum;
    //         end
    //         endcase
    //     end
    // end


    assign tx_o = rx_i;

endmodule
