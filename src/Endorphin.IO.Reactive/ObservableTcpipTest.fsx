// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.
#I __SOURCE_DIRECTORY__
#r "../../packages/Endorphin.Core/lib/net452/Endorphin.Core.dll"
#r "../../packages/log4net/lib/net45-full/log4net.dll"
#r "System.Core.dll"
#r "System.dll"
#r "System.Numerics.dll"
#r "./bin/Debug/Endorphin.IO.dll"
#r "./bin/Debug/Endorphin.IO.Reactive.dll"

open Endorphin.IO.Reactive
open System

log4net.Config.BasicConfigurator.Configure()

let queryTcpip = async {
    try
        use tcpipInstrument = new LineObservableTcpipInstrument("Tcpip test","localhost",4000)
        use __ = tcpipInstrument.Lines() |> Observable.subscribe((printfn "ObsLine: %s"))
        tcpipInstrument.Start()
        Console.ReadLine() |> ignore
    with
    | exn -> failwithf "Failed: %A" exn }
Async.RunSynchronously queryTcpip


