// Copyright (c) University of Warwick. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.
#I __SOURCE_DIRECTORY__
#r "../../packages/Endorphin.Core/lib/net452/Endorphin.Core.dll"
#r "../../packages/log4net/lib/net45-full/log4net.dll"
#r "System.Core.dll"
#r "System.dll"
#r "System.Numerics.dll"
#r "./bin/Debug/Endorphin.IO.dll"

open Endorphin.IO
open System

log4net.Config.BasicConfigurator.Configure()

let querySerial = async {
    try
        use serialInstrument = new SerialInstrument("Serial test",(printfn "Line: %s"),"COM4")
        serialInstrument.Start()
        serialInstrument.WriteLine("HIDE DATA")
        serialInstrument.WriteLine("?")
        Console.ReadLine() |> ignore
    with
    | exn -> failwithf "Failed: %A" exn }
Async.RunSynchronously querySerial


