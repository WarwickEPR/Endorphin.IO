// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.Generic.Line

open Endorphin.Core
open System

[<AutoOpen>]
module LineAgent =

    type private Message =
    | Receive of string
    | Send of string

    /// Agent to serialise writing linemode command and read emitted data into
    /// lines asynchronously
    /// This is useful for devices which don't implement a VISA-style command-response
    /// e.g. if commands are not acknowledged but the device may stream line-mode data
    type LineAgent(writeLine,handleLine,logname:string) =
        let logger = log4net.LogManager.GetLogger logname

        let extractLine (data:string) =
            match data.IndexOfAny([| '\r'; '\n' |]) with
            | -1 ->
                (None,data)
            | i ->
                let line = data.[0..i-1]
                let remainder =
                    if data.Length <= i+1 then
                        "" // 
                    else
                        if data.Chars(i+1) = '\n' then
                            data.Substring (i+2)
                        else
                            data.Substring (i+1)
                (Some line,remainder)
        
        let extractLines data =
            let rec extractLines' lines (data:string) =
                let line, remainder' = extractLine data
                match line with
                | None -> (lines,remainder')
                | Some line -> extractLines' (line :: lines) remainder'
            let lines, remainder = extractLines' [] data
            (List.rev lines, remainder)

        let messageHandler (mbox:Agent<Message>) =
            let rec loop (remainder:string)  = async {
                do! Async.SwitchToThreadPool()
                let! msg = mbox.Receive()
                match msg with
                | Receive newData ->
                    // Received data will not be aligned with newlines.
                    // Combine data already received with the new data and
                    // emit any completed lines.
                    // Save any incomplete line fragment to combine with the next block
                    
                    let lines, remainder' = extractLines (remainder + newData)
                    lines |> List.iter (sprintf "Received line: %s" >> logger.Debug)
                    lines |> List.iter handleLine
                    return! loop remainder'
                | Send s -> 
                    do! writeLine s
                    return! loop remainder
            }
            loop ""

        let agent = Agent.Start messageHandler
        
        /// Write a line to the serial port
        member __.WriteLine = Send >> agent.Post
        member __.Receive = Receive >> agent.Post
        member __.Logger = logger


    type ObservableLineAgent(writeLine,logname:string) as agent =
        inherit LineAgent(writeLine,agent.Notify,logname)

        let notifier = new NotificationEvent<string>()

        member x.Notify = Next >> notifier.Trigger
            
        member x.Lines() : IObservable<string> = notifier.Publish |> Observable.fromNotificationEvent
        member x.Complete() = Completed |> notifier.Trigger
        member x.Error = Error >> notifier.Trigger


