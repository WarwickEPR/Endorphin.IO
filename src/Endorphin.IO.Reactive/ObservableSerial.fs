// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.IO.Reactive

open System
open Endorphin.Core
open FSharp.Control.Reactive
open System.Threading

[<AbstractClass>]
type SerialInstrument<'T>(logname,port,configuration,?eventContext:SynchronizationContext) =
    inherit Endorphin.IO.Serial.SerialInstrument<'T>(logname,port,configuration)

    let notifier = new NotificationEvent<string[]>()
    let logger = log4net.LogManager.GetLogger logname
    let notify =
        match eventContext with
        | None
        | Some null -> Next >> notifier.Trigger
        | Some ctx -> (fun x -> ctx.Post((fun _ -> Next x |> notifier.Trigger),null))
    override __.HandleLines lines = notify lines
            
    member x.Lines() : IObservable<string[]> = notifier.Publish |> Observable.fromNotificationEvent
    member __.Complete() = Completed |> notifier.Trigger
    member __.Error = Error >> notifier.Trigger

    member x.OnFinish() = x.Complete(); base.OnFinish()
    interface IDisposable with member x.Dispose() = x.OnFinish()


type LineObservableSerialInstrument(logname,comPort,configuration,eventContext) =
    inherit SerialInstrument<string>(logname,comPort,configuration,eventContext)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.nextLine received

type PromptObservableSerialInstrument(logname,comPort,prompt,configuration,eventContext) =
    inherit SerialInstrument<string[]>(logname,comPort,configuration,eventContext)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.uptoPrompt prompt received
