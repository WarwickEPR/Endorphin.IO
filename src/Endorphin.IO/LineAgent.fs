// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.IO

type ReceivedLines = string list * string  // lines so far, plus any incomplete line
type ExtractReply<'T> = ReceivedLines -> ReceivedLines * 'T option

type private Message<'T> =
| Receive of string
| Send of string
| Query of query: string * AsyncReplyChannel<'T>


/// Agent to serialise writing linemode command and read emitted data into
/// lines asynchronously. StreamBuffer doesn't
/// This is useful for devices which don't implement a VISA-style command-response
/// e.g. if commands are not acknowledged but the device may stream line-mode data
[<AbstractClass>]
type LineAgent<'T>(logname:string) as this =
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

    let updateReceived (receivedLines,_) newLines remainder =
        (receivedLines @ newLines,remainder)

    let messageHandler (mbox:MailboxProcessor<Message<'T>>) =
        let rec loop (received : ReceivedLines) (repliesExpected : ('T -> unit) list ) = async {
            do! Async.SwitchToThreadPool()
            let! msg = mbox.Receive()
            match msg with
            | Receive newData ->
                // Received data will not be aligned with newlines.
                // Combine data already received with the new data and
                // emit any completed lines.
                // Keep incomplete line fragment to combine with the next block
                    
                let (newLines,remainder) = extractLines (snd received + newData)
                let received' = updateReceived received newLines remainder
                newLines |> List.iter (sprintf "Received line: %s" >> logger.Debug)
                newLines |> List.iter this.HandleLine

                let rec handleQueryReplies received (pending : ('T -> unit) list) =
                    match pending with
                    | next :: rest ->
                        let (received',reply) = this.ExtractReply received
                        match reply with
                        | Some reply -> // Satisfied this one, try to satisfy the rest with the remaining lines
                            reply |> next
                            handleQueryReplies received' rest
                        | None -> // Did not find enough to satisfy consumer
                            (received',pending)
                    | [] ->
                        (received,[])
                    

                if repliesExpected.IsEmpty then
                    return! loop received' [] // Forget pending lines as no-one is waiting for them
                else
                    let waitingInOrder = List.rev repliesExpected
                    let (received'',stillWaiting) = handleQueryReplies received' waitingInOrder
                    return! loop received'' (List.rev stillWaiting)

            | Send line ->
                do! this.WriteLine line
                return! loop received repliesExpected
            | Query (query,replyChannel) ->
                Send query |> mbox.Post
                let reply = replyChannel.Reply
                return! loop received (reply :: repliesExpected)
        }
        loop ([],"") []

    let agent = MailboxProcessor.Start messageHandler

    abstract member WriteLine : string -> Async<unit>
    abstract member HandleLine : string -> unit
    default __.HandleLine(_) = ()
    abstract member ExtractReply : ReceivedLines -> ReceivedLines * 'T option

    /// Write a line to the serial port
    member __.Send = Send >> agent.Post
    member __.Receive a = a |> Receive |> agent.Post
    member __.Query q = (fun rc -> Query (q,rc)) |> agent.PostAndReply
    member __.QueryAsync q = (fun rc -> Query (q,rc)) |> agent.PostAndAsyncReply
    member __.Logger = logger

module LineAgent =

    let nextLine (received:ReceivedLines) =
        let (lines,remainder) = received
        match lines with
        | nextLine :: rest ->
            let received' : ReceivedLines = (rest,remainder)
            (received',Some nextLine)
        | [] ->
            (received,None)

    let uptoPrompt (prompt:string) (received:ReceivedLines) =
        let (lines,remainder) = received
        let condition (line:string) = line.StartsWith prompt
        match List.tryFindIndex condition lines with
        | None ->
            if condition remainder then
                ((([],""):ReceivedLines),Some lines)
            else
                (received,None)
        | Some i ->
            let (reply,rest) = List.splitAt i lines
            let received' : ReceivedLines = (rest.Tail,remainder)
            (received',Some reply)


