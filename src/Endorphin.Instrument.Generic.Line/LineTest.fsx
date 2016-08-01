let extractLine (data:string) =
    match data.IndexOfAny([| '\r'; '\n' |]) with
    | -1 ->
        (None,data)
    | i ->
        let line = data.[0..i-1]
        let remainder =
            if data.Length <= i+1 then
                "" // 
            else
                if data.Chars(i+1) = '\n' then
                    data.Substring (i+2)
                else
                    data.Substring (i+1)
        (Some line,remainder)
        
let extractLines data =
    let rec extractLines' lines (data:string) =
        let line, remainder' = extractLine data
        match line with
        | None -> (lines,remainder')
        | Some line -> extractLines' (line :: lines) remainder'
    let lines, remainder = extractLines' [] data
    (List.rev lines, remainder)

let sample1 = "Hello world\r\nThis is simple\r\n"
let sample2 = "Hello world\nThis is simple\n"
let sample3 = "Hello world\r\nThis is simple\r\nleft overs"
let sample4 = "Hello world\nThis is simple\nleft overs"

let a1 = extractLine sample1
let b1 = extractLines sample1
let a2 = extractLine sample2
let b2 = extractLines sample2
let a3 = extractLine sample3
let b3 = extractLines sample3
let a4 = extractLine sample4
let b4 = extractLines sample4
