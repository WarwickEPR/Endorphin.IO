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

// test with nc -l 4000 using the terminal settings "stty -icanon min 1 time 0" for character mode IO

let handleLine (line:string) =
    if line.EndsWith "0000" then printfn "Line: %s" line

let queryTcpip = async {
    try
        use tcpipInstrument = new LineTcpipInstrument("Tcpip test","localhost",4000)
        tcpipInstrument.Start()
        tcpipInstrument.Query "Hello?" |> printfn "Answered: %s"
        tcpipInstrument.Query "Hello?" |> printfn "Answered 2: %s"
        let! answer = tcpipInstrument.QueryAsync "Hello again?"
        printfn "Answered async: %s" answer
        do! Async.Sleep 1000
        do! tcpipInstrument.WriteLine "Boo!"
        Console.ReadLine() |> ignore
    with
    | exn -> failwithf "Failed: %A" exn }
Async.RunSynchronously queryTcpip

//        tcpipInstrument.QueryUntil (fun x -> x.StartsWith "> ") "Again?" |> List.iteri (printfn "Answered %d: %s")


