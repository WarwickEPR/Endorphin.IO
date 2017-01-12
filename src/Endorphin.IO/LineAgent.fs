// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.IO

open System

type ReceivedLines = string[] * string  // lines so far, plus any incomplete line
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

    let extractLines (data:string) =
        // The remove empty lines option might be a problem
        // Not all sources have consistent line endings, but it might be
        // worth switching to split on \n and strip \r instead
        data.Split([|'\r';'\n'|],StringSplitOptions.RemoveEmptyEntries)

    let updateReceived (receivedLines,_) newLines remainder =
        (receivedLines @ newLines,remainder)

    let messageHandler (mbox:MailboxProcessor<Message<'T>>) =
        let rec loop (received : ReceivedLines) (repliesExpected : ('T -> unit) list ) = async {
            let! msg = mbox.Receive()
            match msg with
            | Receive newData ->
                // Received data will not be aligned with newlines.
                // Combine data already received with the new data and
                // emit any completed lines.
                // Keep incomplete line fragment to combine with the next block
                    
                let receivedLines = fst received
                let remainder = snd received
                let lines = extractLines (remainder + newData)
                let (newLines,remainder) =
                    let last = newData.Chars (newData.Length-1)
                    if last = '\n' || last = '\r' then
                        (lines,"")
                    else
                        let remainder' = Array.last lines
                        let newLines = Array.sub lines 0 (lines.Length-1)
                        (newLines,remainder)
                let receivedLines' = Array.concat [receivedLines; newLines]
                let received' = (receivedLines',remainder)

                newLines |> this.HandleLines

                let rec handleQueryReplies received (pending : ('T -> unit) list) =
                    match pending with
                    | next :: rest ->
                        let (received',reply) = this.ExtractReply received'
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
        async {
            do! Async.SwitchToNewThread()
            logger.Debug <| sprintf "Starting on new thread %A" System.Threading.SynchronizationContext.Current
            return! loop ([||],"") [] }

    let agent = MailboxProcessor.Start messageHandler

    abstract member WriteLine : string -> Async<unit>
    abstract member HandleLines : string[] -> unit
    default __.HandleLines(_) = ()
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
        if lines.Length > 0 then
            let received' = (Array.sub lines 1 (lines.Length-1),remainder)
            (received',Some lines.[0])
        else
            (received,None)

    let uptoPrompt (prompt:string) (received:ReceivedLines) =
        let (lines,remainder) = received
        let condition (line:string) = line.StartsWith prompt
        match Array.tryFindIndex condition lines with
        | None ->
            if condition remainder then
                ((([||],""):ReceivedLines),Some lines)
            else
                (received,None)
        | Some i ->
            let (reply,rest) = Array.splitAt i lines
            let received' : ReceivedLines = (Array.sub rest 1 (Array.length rest - 1),remainder)
            (received',Some reply)
