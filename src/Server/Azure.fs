module Azure

open CliWrap
open CliWrap.Buffered
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Threading.Tasks
open Azure.Identity
open Azure.ResourceManager

open Shared
let armClient() = ArmClient(AzureCliCredential())

let account() = task {
    try
        let! output = Cli.Wrap("az").WithArguments("account show").ExecuteBufferedAsync()
        if output.ExitCode = 0 then
            let json = JObject.Parse(output.StandardOutput)
            return Ok {
                subscriptionId = json.["id"].ToObject<string>()
                subscriptionName =  json.["name"].ToObject<string>()
                userName =
                    if json.ContainsKey "user" then
                        json.["user"].["name"].ToObject<string>()
                    else
                        ""
            }
        else
            return Error $"Error while called 'az account show': {output.StandardError}"
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let defaultSubscription() = task {
    match! account() with
    | Ok account ->
        let defaultSubscription =
            armClient().GetSubscriptions()
                |> Seq.tryFind (fun sub -> sub.Id.SubscriptionId = account.subscriptionId)
                |> Option.defaultValue (armClient().GetDefaultSubscription())
        return Ok defaultSubscription
    | Error error ->
        return Error $"Error occurred while getting the default subscription: {error}"
}

let getResourceGroups() = task {
    try
        let! subscription = defaultSubscription()
        match subscription with
        | Ok subscriptionResource ->
             let groups = subscriptionResource.GetResourceGroups()
             return Ok ([ for group in groups -> group.Id.Name ] |> List.sortBy (fun name -> name.ToLower()))
        | Error error ->
            return Error $"Could not find resource groups: {error}"
    with
    | ex ->
        let errorType = ex.GetType().Name
        return Error $"{errorType}: {ex.Message}"
}

let getResourcesUnderResourceGroup (resourceGroupName: string) = task {
    try
        let! subscription = defaultSubscription()
        match subscription with
        | Ok subscriptionResource ->
            let resourceGroup = subscriptionResource.GetResourceGroup(resourceGroupName)
            let resources: AzureResource list = [
                for page in resourceGroup.Value.GetGenericResources().AsPages() do
                for resource in page.Values do
                    yield {
                        resourceId = resource.Data.Id.ToString()
                        resourceType = resource.Id.ResourceType.ToString()
                        name = resource.Id.Name
                    }
            ]

            let pulumiImportJson = JObject()
            let resourcesJson = JArray()
            let resourceGroupJson = JObject()
            resourceGroupJson.Add("type", "azure-native:resources:ResourceGroup")
            resourceGroupJson.Add("name", resourceGroupName)
            resourceGroupJson.Add("id", resourceGroup.Value.Id.ToString())
            resourcesJson.Add(resourceGroupJson)

            for resource in resources do
                let azureNativeType = AzureResourceTokens.fromAzureSpecToPulumi resource.resourceType
                let resourceJson = JObject()
                resourceJson.Add("type", azureNativeType)
                resourceJson.Add("name", resource.name)
                resourceJson.Add("id", resource.resourceId)
                resourcesJson.Add(resourceJson)

            pulumiImportJson.Add("resources", resourcesJson)

            return Ok {
                azureResources = resources
                pulumiImportJson = pulumiImportJson.ToString(Formatting.Indented)
            }
        | Error error ->
            return Error $"Could not find resources in {resourceGroupName}: {error}"
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}
