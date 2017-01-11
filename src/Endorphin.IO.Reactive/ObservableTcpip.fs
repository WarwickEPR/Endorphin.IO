// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.
namespace Endorphin.IO.Reactive

open Endorphin.Core
open System
open FSharp.Control.Reactive
open System.Threading

[<AbstractClass>]
type TcpipInstrument<'T>(logname,hostname,port,?eventContext:SynchronizationContext) =
    inherit Endorphin.IO.TcpipInstrument<'T>(logname,hostname,port)
    let notifier = new NotificationEvent<string[]>()
    let notify =
        match eventContext with
        | None
        | Some null -> Next >> notifier.Trigger
        | Some ctx -> (fun x -> ctx.Post((fun _ -> Next x |> notifier.Trigger),null))

    override __.HandleLines lines = lines |> notify
    member __.Lines() : IObservable<string[]> = notifier.Publish |> Observable.fromNotificationEvent |> Observable.observeOn System.Reactive.Concurrency.NewThreadScheduler.Default
    member __.Complete() = Completed |> notifier.Trigger
    member __.Error = Error >> notifier.Trigger

    member x.OnFinish() = x.Complete(); base.OnFinish()
    interface IDisposable with member x.Dispose() = x.OnFinish()

type LineObservableTcpipInstrument(logname,host,port,eventContext) =
    inherit TcpipInstrument<string>(logname,host,port,eventContext)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.nextLine received

type PromptObservableTcpipInstrument(logname,prompt,host,port,eventContext) =
    inherit TcpipInstrument<string[]>(logname,host,port,eventContext)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.uptoPrompt prompt received
