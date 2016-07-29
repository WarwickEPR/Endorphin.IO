// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

// Warning: generated file; your changes could be lost when a new file is generated.
#I __SOURCE_DIRECTORY__
#r "../../packages/Endorphin.Core/lib/net452/Endorphin.Core.dll"
#r "../../packages/log4net/lib/net45-full/log4net.dll"
#r "System.Core.dll"
#r "System.dll"
#r "System.Numerics.dll"
#r "./bin/Debug/Endorphin.Instrument.Generic.Serial.dll"

open Endorphin.Instrument.Generic.Serial
open System.IO.Ports
open System

let querySerial = async {
    let serialPort = serialPort "COM4" defaultSerialConfiguration
    use a = new SerialAgent(serialPort,(printfn "Received line: %s"))
    printfn "Sending: ?"
    a.WriteLine "?" }

Async.RunSynchronously querySerial


