// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.
namespace Endorphin.IO.Reactive

open Endorphin.Core
open System

[<AbstractClass>]
type TcpipInstrument<'T>(logname,hostname,port) =
    inherit Endorphin.IO.TcpipInstrument<'T>(logname,hostname,port)
    let notifier = new NotificationEvent<string>()
    let notify = Next >> notifier.Trigger
    override __.HandleLine line = line |> notify
    member __.Lines() : IObservable<string> = notifier.Publish |> Observable.fromNotificationEvent
    member __.Complete() = Completed |> notifier.Trigger
    member __.Error = Error >> notifier.Trigger

    interface IDisposable with member x.Dispose() = x.Complete(); base.OnFinish()

type LineObservableTcpipInstrument(logname,host,port) =
    inherit TcpipInstrument<string>(logname,host,port)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.nextLine received

type PromptObservableTcpipInstrument(logname,prompt,host,port) =
    inherit TcpipInstrument<string list>(logname,host,port)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.uptoPrompt prompt received
