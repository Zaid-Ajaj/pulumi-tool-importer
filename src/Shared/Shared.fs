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
    accountAlias: string option
    arn: string
    userId: string
}

type AwsSearchRequest = {
    queryString: string
    tags: string
    region: string
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
    region: string
}

type AwsCloudFormationGeneratedTemplate = {
    templateId: string
    templateName: string
    resourceCount: int
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
    resourceDataJson: string
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
    region: string
}

type ImportPreviewResponse = {
    generatedCode: string
    stackState: string
    warnings: string list
    standardError: string option
    standardOutput: string
}
type AwsGeneratedTemplateRequest = {
    templateName: string
    region: string
}

type AwsGeneratedTemplateResponse = {
    templateBody: string
    resourceDataJson: string
    pulumiImportJson: string
    errors: string list
}

type GoogleProject = {
    projectId: string
    projectName: string
}

type GoogleResource = {
    resourceType: string
    name: string
    displayName: string
    location: string
    state: string
}

type SearchGoogleProjectRequest = {
    projectId: string
    query: string option
    maxResult: int
}

type ImporterApi = {
    getPulumiVersion : unit -> Async<string>
    awsCallerIdentity : unit -> Async<Result<AwsCallerIdentity, string>>
    searchAws: AwsSearchRequest -> Async<Result<AwsSearchResponse, string>>
    getAwsCloudFormationStacks: string -> Async<Result<AwsCloudFormationStack list, string>>
    getAwsCloudFormationResources: AwsCloudFormationStack -> Async<Result<AwsCloudFormationResourcesResponse, string>>
    getAwsCloudFormationGeneratedTemplates: string -> Async<Result<AwsCloudFormationGeneratedTemplate list, string>>
    getAwsCloudFormationGeneratedTemplate: AwsGeneratedTemplateRequest -> Async<Result<AwsGeneratedTemplateResponse, string>>
    azureAccount : unit -> Async<Result<AzureAccount, string>>
    getResourceGroups: unit -> Async<Result<string list, string>>
    getResourcesUnderResourceGroup: string -> Async<Result<AzureSearchResponse, string>>
    googleProjects: unit -> Async<Result<GoogleProject list, string>>
    googleResourcesByProject: SearchGoogleProjectRequest -> Async<Result<GoogleResource list, string>>
    importPreview: ImportPreviewRequest -> Async<Result<ImportPreviewResponse, string>>
}