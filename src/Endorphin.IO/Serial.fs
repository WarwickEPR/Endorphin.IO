// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.IO

open System
open System.IO.Ports
open System.Threading

[<AutoOpen>]
module Serial =

    /// Serial port stop bit mode.
    type StopBitMode =
        | One
        | OnePointFive
        | Two

    /// Serial port parity mode.
    type ParityMode =
        | NoParity
        | Odd
        | Even

    /// Serial port configuration.
    type SerialConfiguration = {
        BaudRate    : int
        DataBits    : int
        StopBits    : StopBits
        Parity      : Parity
        LineEnding  : string }

    let DefaultSerialConfiguration = {
        BaudRate   = 115200
        DataBits   = 8
        StopBits   = StopBits.One
        Parity     = Parity.None
        LineEnding = "\r\n" }

    let private createSerialPort comPort configuration =
        let serialPort = new SerialPort(comPort, configuration.BaudRate,
                                        configuration.Parity,
                                        configuration.DataBits,
                                        configuration.StopBits)
        serialPort.NewLine <- configuration.LineEnding
        serialPort
        
    /// Agent to serialise writing linemode commands to a serial port and
    /// to read emitted data into lines asynchronously (avoiding some quirks in SerialPort)
    /// This is useful for devices which don't implement a VISA-style command-response
    /// e.g. if commands are not acknowledged but the device may stream line-mode data
    [<AbstractClass>]
    type SerialInstrument<'T>(logname,comPort, configuration) =
        inherit LineAgent<'T>(logname)

        let logger = log4net.LogManager.GetLogger logname
        let cts = new CancellationTokenSource()

        let serialPort = createSerialPort comPort configuration

        let writeLine msg = async {
            logger.Debug <| sprintf "Sending line: %s" msg
            serialPort.WriteLine(msg) }

        let bufferLen = 2 <<< 13 // 16k
        let buffer : byte[] = Array.zeroCreate(bufferLen)

        do
            try
                serialPort.Open()
                logger.Info <| sprintf "Opened serial port %s" comPort
            with
            | exn -> failwithf "Failed to open serial port: %A" exn
        
        override __.WriteLine line = writeLine line

        member x.StartReading() =
            try
                let readLoop = async {
                    if not serialPort.IsOpen then
                        failwith "Serial port is not open"
                    do! Async.SwitchToNewThread()
                    while serialPort.IsOpen do
                        try
                            let! read = serialPort.BaseStream.ReadAsync(buffer,0,bufferLen) |> Async.AwaitTask
                            if read > 0 then
                                let str = System.Text.Encoding.UTF8.GetString buffer.[0..read-1]
                                logger.Debug <| sprintf "Read %d bytes" read
                                str |> x.Receive
                        with
                        // no timeout set at the moment
                        | :? TimeoutException -> do! Async.Sleep 100 }
                Async.Start (readLoop,cts.Token)

            with
            | exn -> failwithf "Failed to start serial port read loop: %A" exn

        member __.Serial = serialPort

        member __.OnFinish() =
            cts.Cancel()
            serialPort.Close()
            logger.Info <| sprintf "Closed serial port %s" comPort

        interface System.IDisposable with member x.Dispose() = x.OnFinish()

type LineSerialInstrument(logname,comPort,configuration) =
    inherit SerialInstrument<string>(logname,comPort,configuration)
    override __.ExtractReply(received) = LineAgent.nextLine received

type PromptSerialInstrument(logname,comPort,prompt,configuration) =
    inherit SerialInstrument<string list>(logname,comPort,configuration)
    override __.ExtractReply(received) = LineAgent.uptoPrompt prompt received
