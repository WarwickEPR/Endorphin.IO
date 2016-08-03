// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

namespace Endorphin.IO.Reactive

open System
open Endorphin.Core

type SerialInstrument(logname,port,?configuration) =
    let notifier = new NotificationEvent<string>()
    let notify = Next >> notifier.Trigger
    let serialInstrument =
        match configuration with
        | None ->
            new Endorphin.IO.Serial.SerialInstrument(logname,notify,port)
        | Some serialConfig -> 
            new Endorphin.IO.Serial.SerialInstrument(logname,notify,port,serialConfig)
            
    member __.Lines() : IObservable<string> = notifier.Publish |> Observable.fromNotificationEvent
    member __.Complete() = Completed |> notifier.Trigger
    member __.Error = Error >> notifier.Trigger
    member __.Start = serialInstrument.Start
    member __.WriteLine = serialInstrument.WriteLine
    member __.QueryLine = serialInstrument.QueryLine
    member __.QueryLineAsync = serialInstrument.QueryLineAsync

    interface IDisposable
        with member x.Dispose() = x.Complete()
                                  (serialInstrument :> IDisposable).Dispose()
