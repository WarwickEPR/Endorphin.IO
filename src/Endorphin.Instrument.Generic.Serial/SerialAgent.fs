// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.Generic.Serial

open Endorphin.Core
open System
open System.IO.Ports
open System.Threading

[<AutoOpen>]
module Initialisation =

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

    let serialPort comPort configuration =
        let port = new SerialPort(comPort, configuration.BaudRate,
                                           configuration.Parity,
                                           configuration.DataBits,
                                           configuration.StopBits)
        port.Open()
        port
        

[<AutoOpen>]
module Agent =

    type private Message =
    | Receive of string
    | Send of string

    /// Agent to serialise writing linemode commands to a serial port and
    /// to read emitted data into lines asynchronously (avoiding some quirks in SerialPort)
    type SerialAgent(serialPort:SerialPort, lineHandler, ?ct:CancellationToken) =
        let logger = log4net.LogManager.GetLogger "Serial"

        let readLoop (serialPort:SerialPort) receiveBlock = async {
            if not serialPort.IsOpen then
                failwith "Serial port is not open"
            serialPort.ReadTimeout <- -1 // Just wait asynchronously
            while serialPort.IsOpen do
                try
                    let bufferLen = 4096
                    let buffer :byte[] = Array.zeroCreate(bufferLen)
                    let! read = serialPort.BaseStream.ReadAsync(buffer,0,bufferLen) |> Async.AwaitTask
                    if read > 0 then
                        let str = System.Text.Encoding.UTF8.GetString buffer.[0..read-1]
                        logger.Debug <| sprintf "Read %d bytes: %s" read str
                        str |> receiveBlock
                with
                // no timeout set at the moment
                | :? TimeoutException -> Thread.Sleep 100 }

        let writeLine msg =
            logger.Debug <| sprintf "Sending line: %s" msg
            serialPort.WriteLine(msg)

        let handler (mbox:Agent<Message>) =
            let rec loop (partialLine:string) = async {
                do! Async.SwitchToThreadPool()
                let! msg = mbox.Receive()
                match msg with
                | Receive newData ->
                    // Received data will not be aligned with newlines.
                    // Combine data already received with the new data and
                    // emit any completed lines.
                    // Save any incomplete line fragment to combine with the next block
                    let rec handleData unfinished (d:string) =
                        match d.IndexOfAny([| '\r'; '\n' |]) with
                        | -1 -> // not found
                            unfinished + d
                        | i  ->
                            let line = unfinished + d.[0..i-1]
                            logger.Debug <| sprintf "Complete line: %s" line
                            line |> lineHandler
                            if d.Chars(i+1) = '\n' then
                                handleData "" <| d.Substring (i+2)
                            else
                                handleData "" <| d.Substring (i+1)
                    return! loop (handleData partialLine newData)
                | Send s -> 
                    writeLine s
                    return! loop partialLine
            }
            loop ""

        let agent = Agent.Start handler

        do
            // This agent needs to be passed an open connection with any preparatory commands applied
            let readAsync = async { do! Async.SwitchToNewThread()
                                    do! readLoop serialPort (Receive >> agent.Post) }
            match ct with
            | None -> Async.Start <| readAsync
            | Some ct ->
                Async.Start (readAsync,ct)

        interface IDisposable with member __.Dispose() = serialPort.Close()
        
        /// Write a line to the serial port
        member __.WriteLine line =
            Send line |> agent.Post
                
                
                
                
                
                
                
                
                
                
                
                
