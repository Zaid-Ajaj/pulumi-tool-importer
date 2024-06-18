module AzureResourceTokens

open System
open Humanizer

let moduleAndResource (fullQualifiedTypeName: string) =
    match fullQualifiedTypeName.Split "/" with
    | [|  |] ->
        "unknown", "unknown"
    | segments ->
        let resourceNamespace = segments[0]
        let resourceName = segments[segments.Length - 1]
        let resourceModule =
            if resourceNamespace.StartsWith "Microsoft." then
                resourceNamespace.Substring(10).Replace(".", "").ToLower()
            else
                resourceNamespace.Replace(".", "").ToLower()

        let resource = resourceName.Pascalize().Singularize(true)
        match resourceModule, resource with
        | "network", "Dnszone" ->
            "network", "Zone"
        | _ ->
            resourceModule, resource

let fromAzureSpecToPulumi(token: string) =
    if String.IsNullOrWhiteSpace token then
        "azure-native:unknown:unknown"
    else
        match token.Split "@" with
        | [| fullQualifiedTypeName; version |] ->
            let moduleName, resource = moduleAndResource fullQualifiedTypeName
            $"azure-native:{moduleName}:{resource}"

        | [| fullQualifiedTypeName |] ->
            let moduleName, resource = moduleAndResource fullQualifiedTypeName
            $"azure-native:{moduleName}:{resource}"
        | _ ->
            "azure-native:unknown:unknown"