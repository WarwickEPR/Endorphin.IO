// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.IO

type private Message =
| Receive of string
| Send of string
| QueryNextLine of query : string * AsyncReplyChannel<string>
| QueryUntil of query: string * condition: (string -> bool) * AsyncReplyChannel<string list>

type ReplyConsumer = string list -> string list option


/// Agent to serialise writing linemode command and read emitted data into
/// lines asynchronously. StreamBuffer doesn't
/// This is useful for devices which don't implement a VISA-style command-response
/// e.g. if commands are not acknowledged but the device may stream line-mode data
type LineAgent(writeLine,handleLine,logname:string) =
    let logger = log4net.LogManager.GetLogger logname

    let nextLineReply (rc:AsyncReplyChannel<string>) =
        function
        | next :: rest ->
            rc.Reply next
            Some rest
        | _ -> None

    let consumeUntilReply (rc:AsyncReplyChannel<string list>) expected (lines: string list) =
        match List.tryFindIndex expected lines with
        | None -> None
        | Some i ->
            let (reply,rest) = List.splitAt i lines
            rc.Reply reply
            Some rest.Tail

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
        let rec loop (remainder:string) (receivedLines:string list) (pendingQueries:ReplyConsumer list) = async {
            do! Async.SwitchToThreadPool()
            let! msg = mbox.Receive()
            match msg with
            | Receive newData ->
                // Received data will not be aligned with newlines.
                // Combine data already received with the new data and
                // emit any completed lines.
                // Save any incomplete line fragment to combine with the next block
                    
                let newLines, remainder' = extractLines (remainder + newData)
                newLines |> List.iter (sprintf "Received line: %s" >> logger.Debug)
                newLines |> List.iter handleLine

                let rec handleConsumers lines (consumers : ReplyConsumer list) =
                    match consumers with
                    | consumer :: rest ->
                        match consumer lines with
                        | Some lines' -> // Satisfied this one, try to satisfy the rest with the remaining lines
                            handleConsumers lines' rest
                        | None -> // Did not find enough to satisfy consumer
                            (lines,consumers)
                    | [] ->
                        (lines,[])
                    

                if pendingQueries.IsEmpty then
                    return! loop remainder' [] [] // Forget pending lines as no-one is waiting for them
                else
                    let lines = receivedLines @ newLines
                    let consumerQ = List.rev pendingQueries
                    let (remainingLines',consumerQ') = handleConsumers lines consumerQ
                    return! loop remainder' remainingLines' (List.rev consumerQ')

            | Send line ->
                do! writeLine line
                return! loop remainder receivedLines pendingQueries
            | QueryNextLine (line,replyChannel) ->
                Send line |> mbox.Post // send string
                return! loop remainder receivedLines ((nextLineReply replyChannel) :: pendingQueries) // reply with next line
            | QueryUntil (query,condition,replyChannel) ->
                Send query |> mbox.Post
                return! loop remainder receivedLines ((consumeUntilReply replyChannel condition) :: pendingQueries)
        }
        loop "" [] []

    let agent = MailboxProcessor.Start messageHandler

    /// Write a line to the serial port
    member __.WriteLine = Send >> agent.Post
    member __.Receive a = a |> Receive |> agent.Post
    member __.QueryLineAsync line = (fun rc -> QueryNextLine (line,rc)) |> agent.PostAndAsyncReply
    member __.QueryLine line = (fun rc -> QueryNextLine (line,rc)) |> agent.PostAndReply
    member __.QueryUntilAsync condition query = (fun rc -> QueryUntil (query,condition,rc)) |> agent.PostAndAsyncReply
    member __.QueryUntil condition query = (fun rc -> QueryUntil (query,condition,rc)) |> agent.PostAndReply
    member __.Logger = logger
    