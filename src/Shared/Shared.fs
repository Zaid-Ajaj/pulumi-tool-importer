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

type AwsSearchRequest = {
    queryString: string
    tags: string
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
    warnings: string list
}

type AwsCloudFormationStack = {
    stackId: string
    stackName: string
    status: string
    statusReason: string
    description: string
    tags: Map<string, string>
}

type AwsCloudFormationResource = {
    logicalId: string
    resourceId: string
    resourceType: string
}

type AwsCloudFormationResourcesResponse = {
    resources: AwsCloudFormationResource list
    pulumiImportJson: string
    warnings: string list
    errors: string list
    templateBody: string
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
    warnings: string list
    standardError: string option
}

type ImporterApi = {
    getPulumiVersion : unit -> Async<string>
    awsCallerIdentity : unit -> Async<Result<AwsCallerIdentity, string>>
    searchAws: AwsSearchRequest -> Async<Result<AwsSearchResponse, string>>
    getAwsCloudFormationStacks: unit -> Async<Result<AwsCloudFormationStack list, string>>
    getAwsCloudFormationResources: AwsCloudFormationStack -> Async<Result<AwsCloudFormationResourcesResponse, string>>
    azureAccount : unit -> Async<Result<AzureAccount, string>>
    getResourceGroups: unit -> Async<Result<string list, string>>
    getResourcesUnderResourceGroup: string -> Async<Result<AzureSearchResponse, string>>
    importPreview: ImportPreviewRequest -> Async<Result<ImportPreviewResponse, string>>
}