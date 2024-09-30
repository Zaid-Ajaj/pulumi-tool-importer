module AwsCloudFormationTypes

open System.Collections.Generic
open Shared

type customRemapSpecification = {
    pulumiType: string
    delimiter: string
    importIdentityParts: string list
    remapFunc: AwsCloudFormationResource -> Dictionary<string, Dictionary<string,string>> -> customRemapSpecification -> (string * string * string)
}

type RemapSpecification = {
    pulumiType: string
    delimiter: string
    importIdentityParts: string list
}



