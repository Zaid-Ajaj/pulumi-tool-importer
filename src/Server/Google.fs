module Google

open Google.Apis.Auth.OAuth2
open Google.Apis.CloudResourceManager.v1
open Google.Apis.Services
open Google.Cloud.Asset.V1
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Shared

let private (=>) key value = key, value

let googleTypeMap = Map.ofArray [|
    "cloudfunctions.googleapis.com/Function" => "gcp:cloudfunctions/function:Function"
    "cloudfunctions.googleapis.com/CloudFunction" => "gcp:cloudfunctions/function:Function"
|]

let (|GoogleType|_|) (googleType: string) =
    match googleType.Split '/' with
    | [| domain; resourceType |] ->
        Some (domain, resourceType)
    | _ ->
        None

let (|GoogleService|_|) (domain: string) =
    match domain.Split '.' with
    | [| service; _; _ |] -> Some service
    | _ -> None

let capitalize (input: string) =
    match input with
    | null -> ""
    | "" -> ""
    | _ -> input[0].ToString().ToUpper() + input.Substring(1)

let camelCase (input: string) =
    match input with
    | null -> ""
    | "" -> ""
    | _ -> input[0].ToString().ToLower() + input.Substring(1)

let (|SkippedGoogleType|_|) (googleType: string) =
    match googleType with
    | "sqladmin.googleapis.com/Backup" -> Some ()
    | "sqladmin.googleapis.com/BackupRun" -> Some ()
    | "cloudkms.googleapis.com/CryptoKeyVersion" -> Some ()
    | "dns.googleapis.com/ManagedZone" -> Some ()
    | _ -> None

let pulumiType (googleType: string) =
    match Map.tryFind googleType googleTypeMap with
    | Some foundType -> Some foundType
    | None ->
        match googleType with
        | SkippedGoogleType -> None
        | GoogleType (GoogleService service, resourceType) ->
            Some $"gcp:{service}/{camelCase resourceType}:{capitalize resourceType}"
        | _ ->
            None

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

        let pulumiImportJson = JObject()
        let resourcesJson = JArray()
        let errors = ResizeArray()

        let resources = [
            for resource in filteredDuplicates do {
                resourceType = resource.AssetType
                name = resource.Name.TrimStart '/'
                displayName = resource.DisplayName
                location = resource.Location
                state = resource.State
            }
        ]

        for resource in resources do
            let resourceJson = JObject()
            if resource.displayName <> "" then
                match pulumiType resource.resourceType with
                | Some foundType ->
                    resourceJson.Add("type", foundType)
                    resourceJson.Add("name", resource.displayName.Replace("-", "_"))
                    resourceJson.Add("id", resource.displayName)
                    resourcesJson.Add resourceJson
                | _ ->
                    ignore()


        pulumiImportJson.Add("resources", resourcesJson)

        return Ok {
            resources = resources
            pulumiImportJson = pulumiImportJson.ToString(Formatting.Indented)
        }
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}