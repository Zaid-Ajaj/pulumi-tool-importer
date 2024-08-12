module AwsCloudFormationTypes

type RemapSpecification = {
    pulumiType: string
    delimiter: string
    importIdentityParts: string list
}

