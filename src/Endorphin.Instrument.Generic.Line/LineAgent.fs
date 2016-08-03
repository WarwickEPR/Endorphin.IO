// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.Instrument.Generic.Line

type private Message =
| Receive of string
| Send of string
| Query of string * AsyncReplyChannel<string>

/// Agent to serialise writing linemode command and read emitted data into
/// lines asynchronously. StreamBuffer doesn't
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
            | Some line ->
                match remainder' with
                | "" -> (line :: lines,"")
                | _  -> extractLines' (line :: lines) remainder'
        let lines, remainder = extractLines' [] data
        (List.rev lines, remainder)

    let messageHandler (mbox:MailboxProcessor<Message>) =
        let rec loop (remainder:string) (pendingQueries:_ list) = async {
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

                if pendingQueries.IsEmpty then
                    return! loop remainder' []
                else
                    if pendingQueries.Length >= lines.Length then
                        let (stillWaiting,ready) = List.splitAt (pendingQueries.Length - lines.Length) pendingQueries
                        let replyChannels = List.rev ready
                        List.iter2 (fun (rc:AsyncReplyChannel<string>) line -> rc.Reply line) replyChannels lines
                        return! loop remainder' stillWaiting
                    else
                        let (linesAwaited,spare) = List.splitAt pendingQueries.Length lines
                        let replyChannels = List.rev pendingQueries
                        List.iter2 (fun (rc:AsyncReplyChannel<string>) line -> rc.Reply line) replyChannels linesAwaited
                        return! loop remainder' []
            | Send line ->
                do! writeLine line
                return! loop remainder pendingQueries
            | Query (line,replyChannel) ->
                Send line |> mbox.Post // send string
                return! loop remainder (replyChannel :: pendingQueries) // reply with next line
        }
        loop "" []

    let agent = MailboxProcessor.Start messageHandler
        
    /// Write a line to the serial port
    member __.WriteLine = Send >> agent.Post
    member __.Receive a = a |> Receive |> agent.Post
    member __.QueryLineAsync line = (fun rc -> Query (line,rc)) |> agent.PostAndAsyncReply
    member __.QueryLine line = (fun rc -> Query (line,rc)) |> agent.PostAndReply
    member __.Logger = logger
