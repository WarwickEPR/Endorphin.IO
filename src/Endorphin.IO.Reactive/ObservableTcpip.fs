// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.
namespace Endorphin.IO.Reactive

open Endorphin.Core
open System

type TcpipInstrument(logname,hostname,port) =
    let notifier = new NotificationEvent<string>()
    let notify = Next >> notifier.Trigger
    let tcpipInstrument = new Endorphin.IO.TcpipInstrument(logname,notify,hostname,port)
            
    member __.Lines() : IObservable<string> = notifier.Publish |> Observable.fromNotificationEvent
    member __.Complete() = Completed |> notifier.Trigger
    member __.Error = Error >> notifier.Trigger
    member __.Start = tcpipInstrument.Start
    member __.WriteLine = tcpipInstrument.WriteLine
    member __.QueryLine = tcpipInstrument.QueryLine
    member __.QueryLineAsync = tcpipInstrument.QueryLineAsync


    interface IDisposable
        with member x.Dispose() = x.Complete()
                                  (tcpipInstrument :> IDisposable).Dispose()
