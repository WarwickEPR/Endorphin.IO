// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.IO.Reactive

open System
open Endorphin.Core

[<AbstractClass>]
type SerialInstrument<'T>(logname,port,configuration) =
    inherit Endorphin.IO.Serial.SerialInstrument<'T>(logname,port,configuration)

    let notifier = new NotificationEvent<string>()
    let notify = Next >> notifier.Trigger
            
    member __.Lines() : IObservable<string> = notifier.Publish |> Observable.fromNotificationEvent
    member __.Complete() = Completed |> notifier.Trigger
    member __.Error = Error >> notifier.Trigger

    interface IDisposable with member x.Dispose() = x.Complete(); base.OnFinish()

type LineObservableSerialInstrument(logname,comPort,configuration) =
    inherit SerialInstrument<string>(logname,comPort,configuration)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.nextLine received

type PromptObservableSerialInstrument(logname,comPort,prompt,configuration) =
    inherit SerialInstrument<string list>(logname,comPort,configuration)
    override __.ExtractReply(received) = Endorphin.IO.LineAgent.uptoPrompt prompt received
