`timescale 1ns / 1ps

module sim;
  parameter int unsigned ApbDataWidth = 32;
  parameter int unsigned ApbAddrWidth = 32;
  parameter int unsigned InterruptsCount = 1; // zero irq is ignored
  parameter int ClockPeriod = 100;
  parameter int ReceiverPort = 0;
  parameter int SenderPort = 0;
  parameter string Address = "";

  logic clk = 1;
  logic[InterruptsCount - 1:0] interrupts;



  renode # (
      .BusControllersCount(1),
      .InterruptsCount(InterruptsCount)
  ) renode (
      .clk(clk),
      .interrupts(interrupts)
  );

  renode_apb3_if #(
    .AddressWidth(ApbAddrWidth),
    .DataWidth(ApbDataWidth)
  ) apb (clk);

  renode_apb3_requester renode_apb3_requester (
      .bus(apb),
      .connection(renode.bus_controller)
  );

  logic requester_input_uart_output;
  logic requester_output_uart_input;

  uart_requester uart_requester (
    .clk(clk),
    .cfg_bus_connection(apb),
    .communication_bus_connection(renode.uart_controller),
    .tx_o(requester_output_uart_input),
    .rx_i(requester_input_uart_output)
  );

  initial begin
    if (Address != "") renode.connection.connect(ReceiverPort, SenderPort, Address);
    renode.reset();
  end

  always @(posedge clk) begin
    // The receive method blocks execution of the simulation.
    // It waits until receive a message from Renode.
    renode.receive_and_handle_message();
    if (!renode.connection.is_connected()) $finish;
  end

  always #(ClockPeriod / 2) clk = ~clk;


  apb_uart #(

  ) dut (
    .CLK(clk),
    .RSTN(apb.presetn),
    .PADDR(apb.paddr),
    .PWDATA(apb.pwdata),
    .PWRITE(apb.pwrite),
    .PSEL(apb.pselx),
    .PENABLE(apb.penable),
    .PRDATA(apb.prdata),
    .PREADY(apb.pready),
    .PSLVERR(apb.pslverr),
    .rx_i(requester_output_uart_input),
    .tx_o(requester_input_uart_output),
    .event_o(apb.perror)
);

endmodule
