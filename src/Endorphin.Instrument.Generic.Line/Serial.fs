// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.Generic.Line

open Endorphin.Core
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
        Parity      : Parity }

    let defaultSerialConfiguration = {
        BaudRate = 115200
        DataBits = 8
        StopBits = StopBits.One
        Parity   = Parity.None }

    let configurationOrDefault = function Some c -> c | None -> defaultSerialConfiguration

    let createSerialPort comPort configuration =
        let serialPort = new SerialPort(comPort, configuration.BaudRate,
                                        configuration.Parity,
                                        configuration.DataBits,
                                        configuration.StopBits)
        serialPort.NewLine <- "\r\n"
        serialPort
        
    type private Message =
    | Receive of string
    | Send of string

    /// Agent to serialise writing linemode commands to a serial port and
    /// to read emitted data into lines asynchronously (avoiding some quirks in SerialPort)
    /// This is useful for devices which don't implement a VISA-style command-response
    /// e.g. if commands are not acknowledged but the device may stream line-mode data
    type SerialInstrument(logname:string, lineHandler, comPort, ?configuration) =
        let logger = log4net.LogManager.GetLogger logname
        let cts = new CancellationTokenSource()

        let serialPort = createSerialPort comPort (configurationOrDefault configuration)

        let writeLine msg = async {
            logger.Debug <| sprintf "Sending line: %s" msg
            serialPort.WriteLine(msg) }

        // create line agent
        let lineAgent = new LineAgent( writeLine, lineHandler, logname )

        let bufferLen = 2 <<< 13 // 16k
        let buffer :byte[] = Array.zeroCreate(bufferLen)

        member __.Start() =
            try
                serialPort.Open()

                let readLoop = async {
                    if not serialPort.IsOpen then
                        failwith "Serial port is not open"
//                    serialPort.ReadTimeout <- 100 // Just wait asynchronously
                    do! Async.SwitchToNewThread()
                    while serialPort.IsOpen do
                        try
                            let! read = serialPort.BaseStream.ReadAsync(buffer,0,bufferLen) |> Async.AwaitTask
                            if read > 0 then
                                let str = System.Text.Encoding.UTF8.GetString buffer.[0..read-1]
                                logger.Debug <| sprintf "Read %d bytes" read
                                str |> lineAgent.Receive
                        with
                        // no timeout set at the moment
                        | :? TimeoutException -> do! Async.Sleep 100 }
                Async.Start (readLoop,cts.Token)

            with
            | exn -> failwithf "Failed to open serial port: %A" exn

        interface IDisposable with member __.Dispose() = cts.Cancel(); serialPort.Close()

        member __.WriteLine = lineAgent.WriteLine
        member __.QueryLine = lineAgent.QueryLine

    type ObservableSerialInstrument(logname,port,?configuration) =
        let notifier = new NotificationEvent<string>()
        let notify = Next >> notifier.Trigger
        let serialInstrument = new SerialInstrument(logname,notify,port,(configurationOrDefault configuration))
            
        member __.Lines() : IObservable<string> = notifier.Publish |> Observable.fromNotificationEvent
        member __.Complete() = Completed |> notifier.Trigger
        member __.Error = Error >> notifier.Trigger
        member __.Start = serialInstrument.Start
        member __.WriteLine = serialInstrument.WriteLine
        member __.QueryLine = serialInstrument.QueryLine

        interface IDisposable
            with member x.Dispose() = x.Complete()
                                      (serialInstrument :> IDisposable).Dispose()
