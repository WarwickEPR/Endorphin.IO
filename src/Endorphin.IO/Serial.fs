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
            try
                serialPort.WriteLine(msg)
            with
            | exn -> exn.ToString() |> sprintf "Unhandled write exception on writing line %s" |> logger.Error }

        let writeBytes (bytes:byte[]) = async {
            logger.Debug <| sprintf "Sending %d bytes" bytes.Length
            try
                serialPort.Write(bytes,0,bytes.Length)
            with
            | exn -> exn.ToString() |> sprintf "Unhandled write exception on writing bytes %s" |> logger.Error }

        let bufferLen = 2 <<< 15 // 64k
        let buffer : byte[] = Array.zeroCreate(bufferLen)

        do
            try
                serialPort.ReadBufferSize <- 2 <<< 16
                serialPort.Open()
                logger.Info <| sprintf "Opened serial port %s" comPort
            with
            | exn -> exn.ToString() |> sprintf "Failed to open serial port %s" |> logger.Error
                     failwithf "Failed to open serial port: %A" exn
        
        override __.WriteLine line = writeLine line
        override __.WriteBytes bytes = writeBytes bytes

        member x.StartReading() =
            try
                let readLoop = async {
                    if not serialPort.IsOpen then
                        failwith "Serial port is not open"
                    do! Async.SwitchToNewThread()
                    while serialPort.IsOpen do
                        try
                            if serialPort.BytesToRead > 0 then
                                let read = serialPort.Read(buffer,0,bufferLen)
                                if read > 0 then
                                    let str = System.Text.Encoding.UTF8.GetString buffer.[0..read-1]
//                                    logger.Debug <| sprintf "Read %d bytes" read
                                    str |> x.Receive
                            do! Async.Sleep 50
                        with
                        // no timeout set at the moment
                        | :? TimeoutException -> do! Async.Sleep 200
                        | exn -> exn.ToString() |> sprintf "Read exception: %s" |> logger.Error }
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
    inherit SerialInstrument<string[]>(logname,comPort,configuration)
    override __.ExtractReply(received) = LineAgent.uptoPrompt prompt received
