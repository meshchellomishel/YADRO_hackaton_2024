// Copyright 2017 ETH Zurich and University of Bologna.
// Copyright and related rights are licensed under the Solderpad Hardware
// License, Version 0.51 (the “License”); you may not use this file except in
// compliance with the License.  You may obtain a copy of the License at
// http://solderpad.org/licenses/SHL-0.51. Unless required by applicable law
// or agreed to in writing, software, hardware and materials distributed under
// this License is distributed on an “AS IS” BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.

module uart_interrupt
#(
    parameter TX_FIFO_DEPTH = 32,
    parameter RX_FIFO_DEPTH = 32
)
(
    input  logic                      clk_i,
    input  logic                      rstn_i,

    // registers
    input  logic [2:0]                IER_i, // interrupt enable register
    input  logic                      RDA_i, // receiver data available
    input  logic                      CTI_i, // character timeout indication

    // control logic
    input  logic                      error_i,
    input  logic [$clog2(RX_FIFO_DEPTH):0]    rx_elements_i,
    input  logic [$clog2(TX_FIFO_DEPTH):0]    tx_elements_i,
    input  logic [1:0]                trigger_level_i,

    input  logic [3:0]                clr_int_i, // one hot

    output logic                      interrupt_o,
    output logic [3:0]                IIR_o
);

    logic [3:0] iir_n, iir_q;
    logic trigger_level_reached;

    always_comb
    begin
        trigger_level_reached = 1'b0;
        if (trigger_level_i == 2'b00) 
        begin
            trigger_level_reached = rx_elements_i >= 1 ? 1'b1 : 1'b0; 
        end 
        else if (trigger_level_i == 2'b01)
        begin
            trigger_level_reached = rx_elements_i >= 4 ? 1'b1 : 1'b0;
        end
        else if (trigger_level_i == 2'b10)
        begin
            trigger_level_reached = rx_elements_i >= 8 ? 1'b1 : 1'b0;
        end 
        else 
        begin
            trigger_level_reached = rx_elements_i >= 14 ? 1'b1 : 1'b0;
        end
    end

    always_comb
    begin

        if (clr_int_i == 4'b0)
            iir_n = iir_q;
        else
            iir_n = iir_q & ~(clr_int_i);

        // Receiver line status interrupt on: Overrun error, parity error, framing error or break interrupt
        if (IER_i[2] & error_i)
            iir_n = 4'b0001;
        //  trigger level reached in FIFO mode
        else if (IER_i[0] & (trigger_level_reached))
            iir_n = 4'b0010;
        // Transmitter holding register empty
        else if (IER_i[1] & tx_elements_i == 0)
            iir_n = 4'b0100;
    end


    always_ff @(posedge clk_i, negedge rstn_i)
    begin
        if (~rstn_i)
        begin
            iir_q <= 4'b0000;
        end
        else
        begin
            iir_q <= iir_n;
        end
    end

    assign IIR_o = iir_q;
    assign interrupt_o = iir_q[0] | iir_q[1] | iir_q[2] | iir_q[3];

endmodule

