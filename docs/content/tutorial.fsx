(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

(**
Introduction
============

This agent handles line-mode serial communication with a device,
serialising commands sent and handling line-mode reads asynchronously

*)

let serialPort = serialPort "COM3" defaultSerialConfiguration

use serial = new SerialAgent(serialPort,(fun x -> handle x))
serial.WriteLine "Hello world"

(**
For more information see the test script
*)
