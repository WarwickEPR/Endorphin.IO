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

//log4net.Config.BasicConfigurator.Configure()

let queryTcpip = async {
    try
        use tcpipInstrument = new TcpipInstrument("Tcpip test",(fun x -> if x.EndsWith "0000" then printfn "Line: %s" x),"localhost",4000)
        tcpipInstrument.Start()
        tcpipInstrument.QueryLine "Hello?" |> printfn "Answered: %s"
        tcpipInstrument.QueryLine "Hello?" |> printfn "Answered 2: %s"
        tcpipInstrument.QueryUntil (fun x -> x.StartsWith ">") "Again?" |> List.iteri (printfn "Answered %d: %s")
        let! answer = tcpipInstrument.QueryLineAsync "Hello again?"
        printfn "Answered async: %s" answer
        do! Async.Sleep 1000
        tcpipInstrument.WriteLine "Boo!"
        Console.ReadLine() |> ignore
    with
    | exn -> failwithf "Failed: %A" exn }
Async.RunSynchronously queryTcpip


