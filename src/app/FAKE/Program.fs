﻿open System
open Fake
open System.IO

let printVersion() =
    traceFAKE "FakePath: %s" fakePath
    traceFAKE "%s" fakeVersionStr

let printEnvironment cmdArgs args =
    printVersion()

    if buildServer = LocalBuild then
        trace localBuildLabel
    else
        tracefn "Build-Version: %s" buildVersion

    if cmdArgs |> Array.length > 1 then
        traceFAKE "FAKE Arguments:"
        args 
          |> Seq.map fst
          |> Seq.iter (tracefn "%A")

    log ""
    traceFAKE "FSI-Path: %s" fsiPath
    traceFAKE "MSBuild-Path: %s" msBuildExe

let containsParam param = Seq.map toLower >> Seq.exists ((=) (toLower param))

let buildScripts = !! "*.fsx" |> Seq.toList

try
    try
        AutoCloseXmlWriter <- true
        let cmdArgs = System.Environment.GetCommandLineArgs()
        if containsParam "version" cmdArgs then printVersion() else
        if (cmdArgs.Length = 2 && cmdArgs.[1].ToLower() = "help") || (cmdArgs.Length = 1 && List.length buildScripts = 0) then CommandlineParams.printAllParams() else
        match Boot.ParseCommandLine(cmdArgs) with
        | None ->
            let buildScriptArg = if cmdArgs.Length > 1 && cmdArgs.[1].EndsWith ".fsx" then cmdArgs.[1] else Seq.head buildScripts
            let args = CommandlineParams.parseArgs (cmdArgs |> Seq.filter ((<>) buildScriptArg) |> Seq.filter ((<>) "details"))
            let fakeArgs = args |> List.filter (fun (k,v) -> k.StartsWith "-d:" = false)
            let fsiArgs = args |> List.filter (fun (k,v) -> k.StartsWith "-d:") |> List.map fst
            traceStartBuild()
            let printDetails = containsParam "details" cmdArgs
            if printDetails then
                printEnvironment cmdArgs fakeArgs
            if not (runBuildScript printDetails buildScriptArg fsiArgs fakeArgs) then
                Environment.ExitCode <- 1
            else
                if printDetails then log "Ready."
        | Some handler ->
            handler.Interact()
    with
    | exn -> 
        if exn.InnerException <> null then
            sprintf "Build failed.\nError:\n%s\nInnerException:\n%s" exn.Message exn.InnerException.Message
            |> traceError
        else
            sprintf "Build failed.\nError:\n%s" exn.Message
            |> traceError

        sendTeamCityError exn.Message
        Environment.ExitCode <- 1

    if buildServer = BuildServer.TeamCity then
        killFSI()
        killMSBuild()

finally
    traceEndBuild()
