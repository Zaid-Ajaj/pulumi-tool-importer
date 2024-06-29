[<RequireQualifiedAccess>]
module Server

open Shared
open Fable.Remoting.Client

let api =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<ImporterApi>
