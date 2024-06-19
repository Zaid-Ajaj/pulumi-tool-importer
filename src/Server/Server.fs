module Server

open System
open System.Diagnostics
open System.IO
open Amazon.EC2.Model
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Newtonsoft.Json
open Saturn
open CliWrap
open CliWrap.Buffered
open Newtonsoft.Json.Linq
open Shared
open System.Threading.Tasks
open Octokit
open Amazon
open Amazon.ResourceExplorer2
open Amazon.ResourceExplorer2.Model
open Amazon.Runtime
open Amazon.SecurityToken
open Amazon.SecurityToken.Model
open Azure.Identity
open Azure.ResourceManager
open Microsoft.Extensions.Logging
open Amazon.EC2

let github = new GitHubClient(ProductHeaderValue "PulumiBot")

let githubClient() =
    let githubToken = Environment.GetEnvironmentVariable "GITHUB_TOKEN"
    if String.IsNullOrWhiteSpace(githubToken) then
        github
    else
        github.Credentials <- Octokit.Credentials(githubToken)
        github

let resourceExplorerClient() =
    let credentials = EnvironmentVariablesAWSCredentials()
    new AmazonResourceExplorer2Client(credentials)

let ec2Client() =
    let credentials = EnvironmentVariablesAWSCredentials()
    new AmazonEC2Client(credentials)

let securityTokenServiceClient() =
    let credentials = EnvironmentVariablesAWSCredentials()
    new AmazonSecurityTokenServiceClient(credentials)

let armClient() = ArmClient(AzureCliCredential())

let getResourceGroups() = task {
    try
        let subscription = armClient().GetDefaultSubscription()
        let groups = subscription.GetResourceGroups()
        return Ok [ for group in groups -> group.Id.Name ]
    with
    | ex ->
        let errorType = ex.GetType().Name
        return Error $"{errorType}: {ex.Message}"
}

let getCallerIdentity() = task {
    try
        let client = securityTokenServiceClient()
        let! response = client.GetCallerIdentityAsync(GetCallerIdentityRequest())

        if response.HttpStatusCode <> System.Net.HttpStatusCode.OK then
            return Error $"Failed to get caller identity: {response.HttpStatusCode}"
        else
            return Ok {
                accountId = response.Account
                userId = response.UserId
                arn = response.Arn
            }
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let rec searchAws' (request: SearchRequest) (nextToken: string option) = task {
   let resources = ResizeArray()
   let explorer = resourceExplorerClient()
   nextToken |> Option.iter (fun token -> request.NextToken <- token)
   let! response = explorer.SearchAsync(request)
   resources.AddRange response.Resources
   if not (isNull response.NextToken) then
       let! next = searchAws' request (Some response.NextToken)
       resources.AddRange next
   return resources
}

let capitalize (input: string) =
    match input with
    | null -> ""
    | "" -> ""
    | _ -> input.[0].ToString().ToUpper() + input.Substring(1)

let normalizeTypeName (input: string) =
    [ for part in input.Split "-" -> capitalize part ]
    |> String.concat ""

let normalizeModuleName (input: string) =
    let parts = input.Split "-"
    if parts.Length = 1 then
        input
    else
    [ for (i, part) in Array.indexed parts ->
        if i = 0
        then part
        else capitalize part ]
    |> String.concat ""

let awsResourceId (resource: Amazon.ResourceExplorer2.Model.Resource) : string =
    let arn = Arn.Parse resource.Arn
    let id = arn.Resource
    match resource.ResourceType.Split ":" with
    | [| serviceName; resourceType |] ->
        if id.StartsWith resourceType then
            id.Substring(resourceType.Length + 1)
        else
            id
    | _ ->
        id

let awsResourceTags (resource: Amazon.ResourceExplorer2.Model.Resource) =
    resource.Properties
    |> Seq.tryFind (fun property -> property.Name = "tags" || property.Name = "Tags")
    |> Option.bind (fun tags ->
        if tags.Data.IsList() then
            tags.Data.AsList()
            |> Seq.filter (fun dict -> dict.IsDictionary())
            |> Seq.collect(fun tags ->
                let dict = tags.AsDictionary()
                let isKeyValuePair =
                    dict.ContainsKey "Key"
                    && dict.ContainsKey "Value"
                    && dict["Key"].IsString()
                    && dict["Value"].IsString()
                if isKeyValuePair then
                    [ dict["Key"].AsString(), dict["Value"].AsString() ]
                else
                    [  ])
            |> Some
        else None)
    |> Option.defaultValue [  ]
    |> Map.ofSeq

let notEmpty (input: string) = not (String.IsNullOrWhiteSpace input)

let searchAws (request: AwsSearchRequest) = task {
    try
        let! results = searchAws' (SearchRequest(QueryString=request.queryString, MaxResults=1000)) None
        let resourceTypesFromSearchResult =
            results
            |> Seq.map (fun resource -> resource.ResourceType)
            |> Seq.distinct
            |> set

        let! securityGroupRules = task {
            if resourceTypesFromSearchResult.Contains "ec2:security-group-rule" then
                let client = ec2Client()
                let request = DescribeSecurityGroupRulesRequest()
                let! response = client.DescribeSecurityGroupRulesAsync(request)
                return Map.ofList [ for rule in response.SecurityGroupRules -> rule.SecurityGroupRuleId, rule ]
            else
                return Map.empty
        }

        let (|SecurityGroupRule|_|) (securityGroupRuleId: string) =
            match securityGroupRules.TryGetValue securityGroupRuleId with
            | true, rule -> Some rule
            | _ -> None

        let resources = [
            for resource in results do
                let resourceId = awsResourceId resource
                let tags = awsResourceTags resource
                if request.tags <> "" then
                    let tagPairs = request.tags.Split ";"
                    let anyTagMatch = tagPairs |> Seq.exists (fun pair ->
                        match pair.Split "=" with
                        | [| tagKey; tagValue |] ->
                            let key = tagKey.Trim()
                            let value = tagValue.Trim()
                            tags.ContainsKey key && tags[key].Trim() = value
                        | _ ->
                            false)

                    if anyTagMatch then yield {
                        resourceType = resource.ResourceType
                        resourceId = resourceId
                        region = resource.Region
                        service = resource.Service
                        arn = resource.Arn
                        owningAccountId = resource.OwningAccountId
                        tags = tags
                    }
                else
                    yield {
                        resourceType = resource.ResourceType
                        resourceId = resourceId
                        region = resource.Region
                        service = resource.Service
                        arn = resource.Arn
                        owningAccountId = resource.OwningAccountId
                        tags = tags
                    }
        ]

        let pulumiImportJson = JObject()
        let resourcesJson = JArray()
        let ancestorTypes = JObject()
        for resource in resources do
            let resourceJson = JObject()
            match resource.resourceType.Split ":" with
            | [| serviceName'; resourceType' |] ->
                let serviceName, resourceType =
                    match serviceName', resourceType' with
                    | "rds", "subgrp" -> "rds", "subnetGroup"
                    | "ec2", "volume" -> "ebs", "volume"
                    | "ec2", "elastic-ip" -> "ec2", "eip"
                    | "logs", "log-group" -> "cloudwatch", "logGroup"
                    | _ -> serviceName', resourceType'

                let pulumiType = $"aws:{serviceName}/{normalizeModuleName resourceType}:{normalizeTypeName resourceType}"
                resourceJson.Add("type", pulumiType)
                if not (ancestorTypes.ContainsKey pulumiType) then
                    match Map.tryFind pulumiType AwsAncestorTypes.ancestorsByType with
                    | Some ancestors ->
                        ancestorTypes.Add(pulumiType, JArray ancestors)
                    | None ->
                        ()
            | _ ->
                resourceJson.Add("type", $"aws:{resource.resourceType}")

            match resource.resourceId with
            | SecurityGroupRule rule ->
                // format: <SECURITY-GROUP-ID>_<TYPE>_<PROTOCOL>_<FROM-PORT>_<TO-PORT>_SOURCE
                let ruleType = if rule.IsEgress then "egress" else "ingress"
                // TODO: fix me
                let source =
                    if notEmpty rule.CidrIpv4 then rule.CidrIpv4
                    elif notEmpty rule.CidrIpv6 then rule.CidrIpv6
                    else "self"

                resourceJson.Add("id", String.concat "_" [
                    rule.GroupId
                    ruleType
                    rule.IpProtocol
                    $"{rule.FromPort}"
                    $"{rule.ToPort}"
                    source
                ])
            | _ ->
                resourceJson.Add("id", resource.resourceId)

            resourceJson.Add("name", resource.resourceId.Replace("-", "_"))
            resourcesJson.Add(resourceJson)

        pulumiImportJson.Add("resources", resourcesJson)
        if ancestorTypes.Count > 0 then
            pulumiImportJson.Add("ancestorTypes", ancestorTypes)

        return Ok {
            resources = resources
            pulumiImportJson = pulumiImportJson.ToString(Formatting.Indented)
        }
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let pulumiCliBinary() : Task<string> = task {
    try
        // try to get the version of pulumi installed on the system
        let! pulumiVersionResult =
            Cli.Wrap("pulumi")
                .WithArguments("version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync()

        let version = pulumiVersionResult.StandardOutput.Trim()
        let versionRegex = Text.RegularExpressions.Regex("v[0-9]+\\.[0-9]+\\.[0-9]+")
        if versionRegex.IsMatch version then
            return "pulumi"
        else
            return! failwith "Pulumi not installed"
    with
    | error ->
        // when pulumi is not installed, try to get the version of of the dev build
        // installed on the system using `make install` in the pulumi repo
        let homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        let pulumiPath = System.IO.Path.Combine(homeDir, ".pulumi-dev", "bin", "pulumi")
        if System.IO.File.Exists pulumiPath then
            return pulumiPath
        elif System.IO.File.Exists $"{pulumiPath}.exe" then
            return $"{pulumiPath}.exe"
        else
            return "pulumi"
}

let getPulumiVersion() = task {
    let! binary = pulumiCliBinary()
    let! output = Cli.Wrap(binary).WithArguments("version").ExecuteBufferedAsync()
    return output.StandardOutput
}

let azureAccount() = task {
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



let getResourcesUnderResourceGroup (resourceGroupName: string) = task {
    try
        let subscription = armClient().GetDefaultSubscription()
        let resourceGroup = subscription.GetResourceGroup(resourceGroupName)
        if not resourceGroup.HasValue then
            return Error "Could not find the resource group"
        else
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

            let ancestorTypes = JObject()
            for resource in resources do
                let azureNativeType = AzureResourceTokens.fromAzureSpecToPulumi resource.resourceType
                let resourceJson = JObject()
                resourceJson.Add("type", azureNativeType)
                resourceJson.Add("name", resource.name)
                resourceJson.Add("id", resource.resourceId)
                resourcesJson.Add(resourceJson)

                if not (ancestorTypes.ContainsKey azureNativeType) then
                    ancestorTypes.Add(azureNativeType, JArray [| "azure-native:resources:ResourceGroup" |])

            pulumiImportJson.Add("resources", resourcesJson)
            pulumiImportJson.Add("ancestorTypes", ancestorTypes)

            return Ok {
                azureResources = resources
                pulumiImportJson = pulumiImportJson.ToString(Formatting.Indented)
            }

    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let tempDirectory (f: string -> 't) =
    let tempDir = Path.GetTempPath()
    let dir = Path.Combine(tempDir, $"pulumi-test-{Guid.NewGuid()}")
    try
        let info = Directory.CreateDirectory dir
        f info.FullName
    finally
        Directory.Delete(dir, true)

let importPreview (request: ImportPreviewRequest) = task {
    try
        let! pulumiCli = pulumiCliBinary()
        let response = tempDirectory <| fun tempDir ->
            let exec (args:string) =
                Cli.Wrap(pulumiCli)
                   .WithWorkingDirectory(tempDir)
                   .WithArguments(args)
                   .WithEnvironmentVariables(fun config ->
                       config.Set("PULUMI_CONFIG_PASSPHRASE", "whatever").Build()
                       |> ignore)
                   .WithValidation(CommandResultValidation.None)
                   .ExecuteBufferedAsync()
                   .GetAwaiter()
                   .GetResult()

            let newCommandArgs = $"new {request.language} --yes --generate-only"
            let pulumiNewOutput = exec newCommandArgs

            if pulumiNewOutput.ExitCode <> 0 then
                Error $"Error occurred while running 'pulumi {newCommandArgs}' command: {pulumiNewOutput.StandardError}"
            else
            Path.Combine(tempDir, "state") |> Directory.CreateDirectory |> ignore
            let pulumiLoginOutput = exec $"login file://./state"
            if pulumiLoginOutput.ExitCode <> 0 then
                Error $"Error occurred while running 'pulumi login file://./state' command: {pulumiLoginOutput.StandardError}"
            else

            let initStack = exec $"stack init dev"
            if initStack.ExitCode <> 0 then
                Error $"Error occurred while running 'pulumi stack init dev' command: {initStack.StandardError}"
            else
            let importFilePath = Path.Combine(tempDir, "import.json")
            File.WriteAllText(importFilePath, request.pulumiImportJson)

            let generatedCodePath = Path.Combine(tempDir, "generated.txt")
            let pulumiImportOutput = exec $"import --file {importFilePath} --yes --out {generatedCodePath}"
            let hasErrors = pulumiImportOutput.StandardOutput.Contains "error:"
            if pulumiImportOutput.ExitCode <> 0 && hasErrors then
                Error $"Error occurred while running 'pulumi import --file <tempDir>/import.json --yes --out <tempDir>/generated.txt' command: {pulumiImportOutput.StandardOutput}"
            else
            let generatedCode = File.ReadAllText(generatedCodePath)
            let stackOutputPath = Path.Combine(tempDir, "stack.json")
            let exportStackOutput = exec $"stack export --file {stackOutputPath}"
            if exportStackOutput.ExitCode <> 0 then
                Error $"Error occurred while running 'pulumi stack export --file {stackOutputPath}' command: {exportStackOutput.StandardError}"
            else
            let stackState = File.ReadAllText(stackOutputPath)
            let warnings =
                pulumiImportOutput.StandardOutput.Split "\n"
                |> Array.filter (fun line -> line.Contains "warning:")
                |> Array.toList
            Ok { generatedCode = generatedCode; stackState = stackState; warnings = warnings }

        return response
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let importerApi = {
    getPulumiVersion = getPulumiVersion >> Async.AwaitTask
    awsCallerIdentity = getCallerIdentity >> Async.AwaitTask
    searchAws = searchAws >> Async.AwaitTask
    getResourceGroups = getResourceGroups >> Async.AwaitTask
    azureAccount = azureAccount >> Async.AwaitTask
    getResourcesUnderResourceGroup = getResourcesUnderResourceGroup >> Async.AwaitTask
    importPreview = importPreview >> Async.AwaitTask
}

let pulumiSchemaDocs = Remoting.documentation "Pulumi Importer" [ ]

let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.withErrorHandler (fun error routeInfo ->
        printfn "%A" error
        Ignore
    )
    |> Remoting.fromValue importerApi
    |> Remoting.withDocs "/api/docs" pulumiSchemaDocs
    |> Remoting.buildHttpHandler

let app = application {
    logging (fun config -> config.ClearProviders() |> ignore)
    use_router webApp
    memory_cache
    use_static AppContext.BaseDirectory
    use_gzip
}

[<EntryPoint>]
let main _ =
    printfn "Pulumi Importer started, navigate to http://localhost:5000"
    run app
    0