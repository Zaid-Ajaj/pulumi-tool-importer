module AwsCloudFormationTypes

open System.Collections.Generic
open Shared

type CustomRemapSpecification = {
    pulumiType: string
    delimiter: string
    importIdentityParts: string list
    remapFunc: AwsCloudFormationResource -> Dictionary<string, Dictionary<string,string>> -> CustomRemapSpecification -> (string * string * string)
}

type RemapSpecification = {
    pulumiType: string
    delimiter: string
    importIdentityParts: string list
}



