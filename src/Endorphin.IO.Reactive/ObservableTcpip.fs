// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.
namespace Endorphin.IO.Reactive

open Endorphin.Core
open System
open FSharp.Control.Reactive

[<AbstractClass>]
type TcpipInstrument<'T>(logname,hostname,port) =
    inherit Endorphin.IO.TcpipInstrument<'T>(logname,hostname,port)
    let notifier = new NotificationEvent<string[]>()
    let notify = Next >> notifier.Trigger
    override __.HandleLines lines = lines |> notify
    member __.Lines() : IObservable<string[]> = notifier.Publish |> Observable.fromNotificationEvent |> Observable.observeOn System.Reactive.Concurrency.NewThreadScheduler.Default
    member __.Complete() = Completed |> notifier.Trigger
    member __.Error = Error >> notifier.Trigger

    member x.OnFinish() = x.Complete(); base.OnFinish()
    interface IDisposable with member x.Dispose() = x.OnFinish()

type LineObservableTcpipInstrument(logname,host,port) =
    inherit TcpipInstrument<string>(logname,host,port)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.nextLine received

type PromptObservableTcpipInstrument(logname,prompt,host,port) =
    inherit TcpipInstrument<string list>(logname,host,port)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.uptoPrompt prompt received
