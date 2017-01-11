// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.
#I __SOURCE_DIRECTORY__
#r "../../packages/Endorphin.Core/lib/net452/Endorphin.Core.dll"
#r "../../packages/log4net/lib/net45-full/log4net.dll"
#r "System.Core.dll"
#r "System.dll"
#r "System.Numerics.dll"
#r "../../packages/System.Reactive.Core/lib/net45/System.Reactive.Core.dll"
#r "../../packages/System.Reactive.Interfaces/lib/net45/System.Reactive.Interfaces.dll"
#r "../../packages/System.Reactive.Linq/lib/net45/System.Reactive.Linq.dll"
#r "../../packages/System.Reactive.PlatformServices/lib/net45/System.Reactive.PlatformServices.dll"
#r "./bin/Debug/Endorphin.IO.dll"
#r "./bin/Debug/Endorphin.IO.Reactive.dll"
#r "../../packages/FSharp.Control.Reactive/lib/net45/FSharp.Control.Reactive.dll"

open Endorphin.IO.Reactive
open System
open FSharp.Control.Reactive

log4net.Config.BasicConfigurator.Configure()

let querySerial = async {
    do! Async.SwitchToNewThread()
    try
        use serialInstrument = new LineObservableSerialInstrument("Serial test","COM4",Endorphin.IO.Serial.DefaultSerialConfiguration,null)
        use __ = serialInstrument.Lines()
//               |> Observable.subscribeOn Reactive.Concurrency.ThreadPoolScheduler.Instance
                 |> Observable.subscribe (Array.iter (printfn "ObsLine: %s"))
        serialInstrument.StartReading()
        serialInstrument.Send("HIDE DATA")
        serialInstrument.Send("TB 10ms")
        serialInstrument.Send("SHOW RATE")
        Console.ReadLine() |> ignore 
    with
    | exn -> failwithf "Failed: %A" exn }
Async.RunSynchronously querySerial


