module Google

open Google.Apis.Auth.OAuth2
open Google.Apis.CloudResourceManager.v1
open Google.Apis.Services
open Google.Cloud.Asset.V1

open Shared

let cloudManagerService() =
    let credentials = GoogleCredential.GetApplicationDefault()
    let initializer = BaseClientService.Initializer(HttpClientInitializer=credentials)
    new CloudResourceManagerService(initializer)

let projects() = task {
    try
        let service = cloudManagerService()
        let! response = service.Projects.List().ExecuteAsync()

        return Ok [
            for project in response.Projects do {
                projectId = project.ProjectId
                projectName = project.Name
            }
        ]
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let resourcesByProject(searchRequest: SearchGoogleProjectRequest) = task {
    try
        let builder = AssetServiceClientBuilder(QuotaProject = searchRequest.projectId)
        let assetService = builder.Build()
        let request = SearchAllResourcesRequest(Scope = $"projects/{searchRequest.projectId}")

        searchRequest.query
        |> Option.iter (fun query -> request.Query <- query)

        let searchResponse = assetService.SearchAllResources(request)
        let filteredDuplicates =
            searchResponse.ReadPage searchRequest.maxResult
            |> Seq.distinctBy (fun resource -> resource.Name)

        let resources = [
            for resource in filteredDuplicates do {
                resourceType = resource.AssetType
                name = resource.Name
                displayName = resource.DisplayName
                location = resource.Location
                state = resource.State
            }
        ]

        return Ok resources
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}