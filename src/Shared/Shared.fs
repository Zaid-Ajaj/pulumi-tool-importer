namespace Shared

open PulumiSchema.Types

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName



[<RequireQualifiedAccess>]
type RateLimited<'T> =
    | Response of 'T
    | RateLimitReached

type AwsCallerIdentity = {
    accountId: string
    arn: string
    userId: string
}

type AwsResource = {
    arn: string
    resourceType: string
    resourceId: string
    region: string
    owningAccountId: string
    service: string
    tags: Map<string, string>
}

type AwsSearchResponse = {
    resources: AwsResource list
    pulumiImportJson: string
}

type AzureResource = {
    resourceId: string
    resourceType: string
    name: string
}

type AzureSearchResponse = {
    azureResources: AzureResource list
    pulumiImportJson: string
}

type AzureAccount = {
    subscriptionId: string
    subscriptionName: string
    userName: string
}

type ImportPreviewRequest = {
    pulumiImportJson: string
    language: string
}

type ImportPreviewResponse = {
    generatedCode: string
    stackState: string
}

type ImporterApi = {
    getPulumiVersion : unit -> Async<string>
    awsCallerIdentity : unit -> Async<Result<AwsCallerIdentity, string>>
    searchAws: string -> Async<Result<AwsSearchResponse, string>>
    azureAccount : unit -> Async<Result<AzureAccount, string>>
    getResourceGroups: unit -> Async<Result<string list, string>>
    getResourcesUnderResourceGroup: string -> Async<Result<AzureSearchResponse, string>>
    importPreview: ImportPreviewRequest -> Async<Result<ImportPreviewResponse, string>>
}