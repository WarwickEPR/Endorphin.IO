// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.IO

open System
open System.Net.Sockets
open System.Threading

/// Serialises writing linemode commands to a Tcpip port and read emitted data
/// into lines asynchronously.
/// This is useful for devices which don't implement a VISA-style command-response
/// e.g. if commands are not acknowledged but the device may stream line-mode data
[<AbstractClass>]
type TcpipInstrument<'T>(logname,hostname:string,port,?lineEnding) as this =
    inherit LineAgent<'T>(logname)
    
    let logger = log4net.LogManager.GetLogger logname

    let cts = new CancellationTokenSource()
    let lineEnding' = match lineEnding with None -> "\r\n" | Some nl -> nl

    let client =
        let client = new TcpClient()
        client.ReceiveBufferSize <- 512
        client.NoDelay <- true
        client

    let writeLine (client:TcpClient) line =
        logger.Debug <| sprintf "Sending line: %s" line
        let chunk = System.Text.Encoding.UTF8.GetBytes (line + lineEnding')
        client.GetStream().WriteAsync(chunk,0,chunk.Length) |> Async.AwaitTask
        
    let bufferLen = 2 <<< 13 // 64k
    let buffer:byte[] = Array.zeroCreate bufferLen

    override __.WriteLine str =
        writeLine client str

    member x.Start() =
        // connect to server
        client.Connect(hostname,port)
        logger.Info <| sprintf "Opened TCP/IP connection to %s:%d" hostname port

        // start pumping data
        let rec readLoop = async {
            // Assume the client is connected until after a read
            // "Connected" only gives the state of the most recent connection
            do! Async.SwitchToNewThread()
            let ctx = Threading.SynchronizationContext.Current
            while not cts.IsCancellationRequested do
                // This version of AsyncRead returns whilst the other blocks
                try
                    let! read = client.GetStream().AsyncRead(buffer,0,bufferLen)
                    if read > 0 then
                        let stringChunk = System.Text.Encoding.UTF8.GetString buffer.[0..read-1]
//                        logger.Debug <| sprintf "Read %d bytes" read
                        do! Async.SwitchToThreadPool()
                        stringChunk |> this.Receive
                        do! Async.SwitchToContext ctx
                with :?TimeoutException -> () }
        Async.Start (readLoop,cts.Token)

    member __.OnFinish() = cts.Cancel(); client.Close(); logger.Info <| sprintf "Closed TCP/IP connection to %s:%d" hostname port
    interface System.IDisposable with member x.Dispose() = x.OnFinish()

type LineTcpipInstrument(logname,host,port) =
    inherit TcpipInstrument<string>(logname,host,port)
    override __.ExtractReply(received) = LineAgent.nextLine received

type PromptTcpipInstrument(logname,prompt,host,port) =
    inherit TcpipInstrument<string[]>(logname,host,port)
    override __.ExtractReply(received) = LineAgent.uptoPrompt prompt received
