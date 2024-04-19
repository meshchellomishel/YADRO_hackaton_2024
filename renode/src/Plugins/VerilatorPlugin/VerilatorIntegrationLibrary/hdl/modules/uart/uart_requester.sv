module uart_requester (
    input logic clk,
    renode_apb3_if cfg_bus_connection,
    input renode_pkg::uart_connection communication_bus_connection,
    output logic tx_o,
    input logic rx_i
);

endmodule