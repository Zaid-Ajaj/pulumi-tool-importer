module Server

open System
open System.Collections.Generic
open System.IO
open Amazon.CloudFormation
open Amazon.CloudFormation.Model
open Amazon.CloudWatchEvents
open Amazon.EC2.Model
open Amazon.SQS
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Newtonsoft.Json
open Saturn
open CliWrap
open CliWrap.Buffered
open Newtonsoft.Json.Linq
open Shared
open System.Threading.Tasks
open Amazon
open Amazon.ResourceExplorer2
open Amazon.ResourceExplorer2.Model
open Amazon.Runtime
open Amazon.SecurityToken
open Amazon.SecurityToken.Model
open Amazon.ElasticLoadBalancingV2
open Amazon.ElasticLoadBalancingV2.Model
open Amazon.IdentityManagement
open Amazon.IdentityManagement.Model
open Amazon.Route53
open Amazon.Route53.Model
open Microsoft.Extensions.Logging
open Amazon.EC2
open AwsCloudFormationTypes

let unsetDefaultRegion = "__default__"

let awsEnvCredentials() =
    let credentials =
        Cli.Wrap("aws")
           .WithArguments("configure export-credentials")
           .WithValidation(CommandResultValidation.None)
           .ExecuteBufferedAsync()
           .GetAwaiter().GetResult()

    if credentials.ExitCode <> 0 then
        failwith $"Error while getting AWS credentials using 'aws configure export-credentials`: {credentials.StandardError}"
    else
        let output = JObject.Parse credentials.StandardOutput
        let accessKeyId = output["AccessKeyId"].ToObject<string>()
        let secretAccessKey = output["SecretAccessKey"].ToObject<string>()
        if not (output.ContainsKey "SessionToken") then
            BasicAWSCredentials(accessKeyId, secretAccessKey) :> AWSCredentials
        else
            let sessionToken = output["SessionToken"].ToObject<string>()
            SessionAWSCredentials(accessKeyId, secretAccessKey, sessionToken) :> AWSCredentials

let resourceExplorerClient(region: string) =
    if region = unsetDefaultRegion then
        new AmazonResourceExplorer2Client(awsEnvCredentials())
    else
        new AmazonResourceExplorer2Client(awsEnvCredentials(), RegionEndpoint.GetBySystemName region)

let ec2Client(region: string) =
    if region = unsetDefaultRegion then
        new AmazonEC2Client(awsEnvCredentials())
    else
        new AmazonEC2Client(awsEnvCredentials(), RegionEndpoint.GetBySystemName region)

let iamClient (region: string) =
    if region = unsetDefaultRegion then
        new AmazonIdentityManagementServiceClient(awsEnvCredentials())
    else
        new AmazonIdentityManagementServiceClient(awsEnvCredentials(), RegionEndpoint.GetBySystemName region)

let securityTokenServiceClient(region: string) =
    if region = unsetDefaultRegion then
        new AmazonSecurityTokenServiceClient(awsEnvCredentials())
    else
        new AmazonSecurityTokenServiceClient(awsEnvCredentials(), RegionEndpoint.GetBySystemName region)

let cloudFormationClient(region: string) =
    if region = unsetDefaultRegion then
        new AmazonCloudFormationClient(awsEnvCredentials())
    else
        new AmazonCloudFormationClient(awsEnvCredentials(), RegionEndpoint.GetBySystemName region)

// https://aws.amazon.com/sqs AWS Message Queue Service
let sqsClient(region: string) =
    if region = unsetDefaultRegion then
        new AmazonSQSClient(awsEnvCredentials())
    else
        new AmazonSQSClient(awsEnvCredentials(), RegionEndpoint.GetBySystemName region)

let cloudWatchEventsClient(region: string) =
    if region = unsetDefaultRegion then
        new AmazonCloudWatchEventsClient(awsEnvCredentials())
    else
        new AmazonCloudWatchEventsClient(awsEnvCredentials(), RegionEndpoint.GetBySystemName region)


let elasticLoadBalancingV2Client(region: string) =
    if region = unsetDefaultRegion then
        new AmazonElasticLoadBalancingV2Client(awsEnvCredentials())
    else
        new AmazonElasticLoadBalancingV2Client(awsEnvCredentials(), RegionEndpoint.GetBySystemName region)

let route53Client(region: string) =
    if region = unsetDefaultRegion then
        new AmazonRoute53Client(awsEnvCredentials())
    else
        new AmazonRoute53Client(awsEnvCredentials(), RegionEndpoint.GetBySystemName region)

let awsFirstAccountAlias() =
    try
        let credentials =
            Cli.Wrap("aws")
               .WithArguments("iam list-account-aliases --output json")
               .WithValidation(CommandResultValidation.None)
               .ExecuteBufferedAsync()
               .GetAwaiter().GetResult()

        if credentials.ExitCode <> 0 then
            None
        else
            let output = JObject.Parse credentials.StandardOutput
            let aliases = output["AccountAliases"].ToObject<string[]>()
            if aliases.Length > 0 then
                Some aliases[0]
            else
                None
    with
    | error ->
        printfn $"error occurred while running 'aws iam list-account-aliases --output json':\n%A{error}"
        None

let getCallerIdentity() = task {
    try
        let client = securityTokenServiceClient(unsetDefaultRegion)
        let! response = client.GetCallerIdentityAsync(GetCallerIdentityRequest())
        if response.HttpStatusCode <> System.Net.HttpStatusCode.OK then
            return Error $"Failed to get caller identity: {response.HttpStatusCode}"
        else
            return Ok {
                accountId = response.Account
                accountAlias = awsFirstAccountAlias()
                userId = response.UserId
                arn = response.Arn
            }
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let rec searchAws' (request: SearchRequest) (region:string) (nextToken: string option) = task {
   let resources = ResizeArray()
   let explorer = resourceExplorerClient region
   nextToken |> Option.iter (fun token -> request.NextToken <- token)
   let! response = explorer.SearchAsync(request)
   resources.AddRange response.Resources
   if not (isNull response.NextToken) then
       let! next = searchAws' request region (Some response.NextToken)
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

let awsIdFromArn (arn: string, resourceType: string) =
    let arn = Arn.Parse(arn)
    let id = arn.Resource
    match resourceType.Split ":" with
    | [| serviceName; resourceType |] ->
        if id.StartsWith resourceType then
            id.Substring(resourceType.Length + 1)
        else
            id
    | _ ->
        id

let awsResourceId (resource: Amazon.ResourceExplorer2.Model.Resource) : string =
    awsIdFromArn(resource.Arn, resource.ResourceType)

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

let awsTypeMapping = function
    | "rds", "subgrp" -> "rds", "subnetGroup"
    | "ec2", "volume" -> "ebs", "volume"
    | "ec2", "elastic-ip" -> "ec2", "eip"
    | "ec2", "dhcp-options" -> "ec2", "vpcDhcpOptions"
    | "ec2", "ipam" -> "ec2", "vpcIpam"
    | "ec2", "ipam-scope" -> "ec2", "vpcIpamScope"
    | "ec2", "image" -> "ec2", "ami"
    | "ec2", "snapshot" -> "ebs", "snapshot"
    | "ec2", "transit-gateway-route-table" -> "ec2transitgateway", "routeTable"
    | "ec2", "vpc-flow-log" -> "ec2", "flowLog"
    | "ec2", "natgateway" -> "ec2", "natGateway"
    | "ec2", "spot-instances-request" -> "ec2", "spotInstanceRequest"
    | "ec2", "transit-gateway" -> "ec2transitgateway", "transitGateway"
    | "logs", "log-group" -> "cloudwatch", "logGroup"
    | "acm-pca", "certificate-authority" -> "acmpca", "certificateAuthority"
    | "apigateway", "restapis" -> "apigateway", "restApi"
    | "athena", "datacatalog" -> "athena", "dataCatalog"
    | "codestar-connections", "connection" -> "codestarconnections", "connection"
    | "cognito-idp", "userpool" -> "cognito", "userPool"
    | "elasticache", "parametergroup" -> "elasticache", "parameterGroup"
    | "elasticache", "subnetgroup" -> "elasticache", "subnetGroup"
    | "elasticbeanstalk", "applicationversion" -> "elasticbeanstalk", "applicationVersion"
    | "events", "event-bus" -> "cloudwatch", "eventBus"
    | "events", "rule" -> "cloudwatch", "eventRule"
    | "iam", "oidc-provider" -> "iam", "openIdConnectProvider"
    | "iam", "mfa" -> "iam", "virtualMfaDevice"
    | "memorydb", "parametergroup" -> "memorydb", "parameterGroup"
    | "rds", "cluster-pg" -> "rds", "cluster"
    | "rds", "og" -> "rds", "optionGroup"
    | "rds", "pg" -> "rds", "parameterGroup"
    | "rds", "secgrp" -> "rds", "subnetGroup" //TODO: verify this
    | "redshift", "parametergroup" -> "redshift", "parameterGroup"
    | "redshift", "subnetgroup" -> "redshift", "subnetGroup"
    | "resource-explorer-2", "index" -> "resourceexplorer", "index"
    | "resource-groups", "group" -> "resourcegroups", "group"
    | "route53", "hostedzone" -> "route53", "zone"
    | "s3", "accesspoint" -> "s3", "accessPoint"
    | "s3", "storage-lens" -> "s3control", "storageLensConfiguration"
    | "ssm", "automation-execution" -> "ssm", "document"
    | "elasticloadbalancing", "listener-rule/app" -> "alb", "listenerRule"
    | "elasticfilesystem", "file-system" -> "efs", "fileSystem"
    | "elasticloadbalancing", "listener/app" -> "alb", "listener"
    | "elasticloadbalancing", "loadbalancer/app" -> "alb", "loadBalancer"
    | "elasticloadbalancing", "targetgroup" -> "alb", "targetGroup"
    | "aps", "workspace" -> "amp", "workspace"
    | "cloudformation", "stackset" -> "cloudformation", "stackSet"
    | "cloudwatch", "alarm" -> "cloudwatch", "metricAlarm"
    | service, resourceType -> service, resourceType

// TODO: find out which resource correspond to these types
let skipAwsResource = function
    | "ssm:automation-execution" -> true
    | "ssm:managed-instance" -> true
    | "forecast:dataset-group" -> true
    | _ -> false

type AwsImportDocs = {
    url: string
    moduleName: string
    resourceName: string
}

let awsImportDocs (pulumiType: string) =
    match pulumiType.Split ":" with
    | [| _; moduleName; _ |] ->
        match moduleName.Split "/" with
        | [| moduleName; resourceName |] ->
            let url = $"https://www.pulumi.com/registry/packages/aws/api-docs/{moduleName.ToLower()}/{resourceName.ToLower()}"
            Some {
                url = url
                moduleName = moduleName
                resourceName = resourceName
            }
        | _ -> None
    | _ -> None

let importWarningDocsMarkdown (pulumiType: string, resourceId: string) =
    let docsInfo =
        awsImportDocs pulumiType
        |> Option.map (fun docs -> $"See [docs]({docs.url}) for more information about **aws.{docs.moduleName}.{docs.resourceName}** resource.")
        |> Option.defaultValue ""

    let warning = $"""Resource '**{pulumiType}**' with used its ID '**{resourceId}**' as the import ID.
However, this resource uses an _import format_ which might need more information than just the ID of that resource.
Consider manually adjusting the import ID in the Pulumi Import JSON before running the actual import.

Examples of import formats for this resource:
```bash
{String.concat Environment.NewLine AwsSchemaTypes.resourcesWithOddImportFormat[pulumiType]}
```
"""

    warning + docsInfo

let searchAws (request: AwsSearchRequest) = task {
    try
        let queryString =
            if notEmpty request.tags then
                let tags =
                    request.tags.Split ";"
                    |> Seq.choose (fun tagPair ->
                        match tagPair.Split "=" with
                        | [| key; value |] when notEmpty key && notEmpty value -> Some(key.Trim(), value.Trim())
                        | _ -> None)
                    |> Seq.filter (fun (key,value) -> not (request.queryString.Contains $"tag.{key}={value}"))
                    |> Seq.filter (fun (key,value) -> not (request.queryString.Contains $"tag:{key}=\"{value}\""))
                    |> Seq.map (fun (key,value) -> $"tag.{key}={value}")
                    |> String.concat " "

                $"{tags} {request.queryString}"
            else
                request.queryString

        let! results = searchAws' (SearchRequest(QueryString=queryString, MaxResults=1000)) request.region None
        let resourceTypesFromSearchResult =
            results
            |> Seq.map (fun resource -> resource.ResourceType)
            |> Seq.distinct
            |> set

        let shouldQuerySecurityGroupRules =
            resourceTypesFromSearchResult.Contains "ec2:security-group-rule"
            || resourceTypesFromSearchResult.Contains "ec2:security-group"

        let! securityGroupRules = task {
            if shouldQuerySecurityGroupRules then
                let client = ec2Client request.region
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

        let! sqsQueueUrls = task {
            if resourceTypesFromSearchResult.Contains "sqs:queue" then
                let client = sqsClient request.region
                let! response = client.ListQueuesAsync(queueNamePrefix="")
                return response.QueueUrls
            else
                return ResizeArray()
        }

        let (|SqsQueue|_|) (resource: AwsResource) =
            sqsQueueUrls
            |> Seq.tryFind (fun url -> url.EndsWith resource.resourceId)

        let! cloudwatchEventRules = task {
            if resourceTypesFromSearchResult.Contains "events:rule" then
                let client = cloudWatchEventsClient request.region
                let! response = client.ListRulesAsync()
                return Map.ofList [ for rule in response.Rules -> rule.Arn, rule ]
            else
                return Map.empty
        }

        let (|CloudWatchEventRule|_|) (resource: AwsResource) =
            match cloudwatchEventRules.TryGetValue resource.arn with
            | true, rule -> Some rule
            | _ -> None

        // skip some of the resources that we don't want to import
        // or we don't know what resources map to them
        let filteredResults =
            results
            |> Seq.filter (fun resource -> not (skipAwsResource resource.ResourceType))

        let resources = [
            for resource in filteredResults do
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
        let addedResourceIds = ResizeArray()
        let warnings = ResizeArray()
        for resource in resources do
            let resourceJson = JObject()
            let pulumiType =
                match resource.resourceType.Split ":" with
                | [| serviceName'; resourceType' |] ->
                    let serviceName, resourceType = awsTypeMapping (serviceName', resourceType')
                    let pulumiType' =
                        match resource.resourceId with
                        | SecurityGroupRule rule  ->
                            if rule.IsEgress
                            then "aws:vpc/securityGroupEgressRule:SecurityGroupEgressRule"
                            else "aws:vpc/securityGroupIngressRule:SecurityGroupIngressRule"
                        | _ ->
                            $"aws:{serviceName}/{normalizeModuleName resourceType}:{normalizeTypeName resourceType}"

                    pulumiType'
                | _ ->
                    $"aws:{resource.resourceType}"

            resourceJson.Add("type", pulumiType)
            if not (AwsSchemaTypes.availableTypes.Contains pulumiType) then
                warnings.Add $"AWS resource '{resource.resourceType}' maps to a non-existing Pulumi type '{pulumiType}'"

            if AwsSchemaTypes.typeRequiresFullArnToImport.Contains pulumiType then
                resourceJson.Add("id", resource.arn)
            else
                match resource with
                | SqsQueue queueUrl ->
                    resourceJson.Add("id", queueUrl)
                | CloudWatchEventRule eventRule ->
                    resourceJson.Add("id", $"{eventRule.EventBusName}/{eventRule.Name}")
                | _ ->
                    resourceJson.Add("id", resource.resourceId)
                    addedResourceIds.Add(resource.resourceId)
                    if AwsSchemaTypes.resourcesWithOddImportFormat.ContainsKey pulumiType then
                        // Add a warning to show that this resource has an odd import format
                        // and the user might need to manually adjust the import ID in the import JSON
                        warnings.Add(importWarningDocsMarkdown(pulumiType, resource.resourceId))

            resourceJson.Add("name", resource.resourceId.Replace("-", "_"))
            resourcesJson.Add(resourceJson)

        // Add security group rules to import JSON if their parent security group is being imported
        for securityGroupRuleId, securityGroupRule in Map.toList securityGroupRules do
            let ruleNotAddedToImport = not (addedResourceIds.Contains securityGroupRuleId)
            let parentSecurityGroupAdded = addedResourceIds.Contains securityGroupRule.GroupId
            if ruleNotAddedToImport && parentSecurityGroupAdded then
                let resourceJson = JObject()
                if securityGroupRule.IsEgress then
                    resourceJson.Add("type", "aws:vpc/securityGroupEgressRule:SecurityGroupEgressRule")
                else
                    resourceJson.Add("type", "aws:vpc/securityGroupIngressRule:SecurityGroupIngressRule")
                resourceJson.Add("id", securityGroupRuleId)
                resourceJson.Add("name", securityGroupRuleId.Replace("-", "_"))
                resourcesJson.Add(resourceJson)

        pulumiImportJson.Add("resources", resourcesJson)
        let searchResponse : AwsSearchResponse = {
            resources = resources
            pulumiImportJson = pulumiImportJson.ToString(Formatting.Indented)
            warnings = warnings |> Seq.distinct |> Seq.sortBy id |> List.ofSeq
        }

        return Ok searchResponse
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let rec awsCloudFormationStacks (nextToken: string option) (region: string) = task {
    let stacks = ResizeArray()
    let client = cloudFormationClient region
    let request = ListStacksRequest()
    nextToken |> Option.iter (fun token -> request.NextToken <- token)
    let! response = client.ListStacksAsync(request)
    stacks.AddRange response.StackSummaries
    if not (isNull response.NextToken) then
        let! next = awsCloudFormationStacks (Some response.NextToken) region
        stacks.AddRange next
    return stacks
}

let getAwsCloudFormationStacks(region: string) = task {
    try
        let! stacks = awsCloudFormationStacks None region
        let sorted =
            stacks
            |> Seq.sortBy (fun stack -> stack.StackName)
            |> Seq.filter (fun stack -> not (stack.StackStatus.Value.Contains "DELETE"))

        return Ok [
            for stack in sorted do
                { stackId = stack.StackId
                  stackName = stack.StackName
                  region = region
                  status = stack.StackStatus.Value
                  statusReason =  stack.StackStatusReason
                  description = stack.TemplateDescription }
        ]
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let getAwsCloudFormationGeneratedTemplates(region: string) = task {
    try
        let client = cloudFormationClient region
        let request = ListGeneratedTemplatesRequest()
        let! response = client.ListGeneratedTemplatesAsync(request)
        let templates =
            response.Summaries
            |> List.ofSeq
            |> List.sortByDescending (fun template -> template.CreationTime)
            |> List.map (fun template -> {
                templateId = template.GeneratedTemplateId
                templateName = template.GeneratedTemplateName
                resourceCount = template.NumberOfResources
            })

        return Ok templates

    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let cloudFormationResourceToSkip = set [
    "AWS::CloudFormation::CustomResource"
    "AWS::CDK::Metadata"
    "AWS::CloudFormation::WaitConditionHandle"
    "AWS::CloudFormation::WaitCondition"
    // TODO: why skip the scaling policy?
    "AWS::AutoScaling::ScalingPolicy"
]

let getJObject (key: string) (json: JObject) =
    if json.ContainsKey key && json[key].Type = JTokenType.Object then
        json[key] :?> JObject
    else
        JObject()

let getAwsCloudFormationGeneratedTemplate (request: AwsGeneratedTemplateRequest) = task {
    try
        let client = cloudFormationClient request.region
        let! templateResponse = client.GetGeneratedTemplateAsync(GetGeneratedTemplateRequest(GeneratedTemplateName=request.templateName))
        let! templateDetails = client.DescribeGeneratedTemplateAsync(DescribeGeneratedTemplateRequest(GeneratedTemplateName=request.templateName))
        let templateBody = JObject.Parse templateResponse.TemplateBody

        let pulumiImportJson = JObject()
        let resourcesJson = JArray()
        let errors = ResizeArray()
        let resourcesFromTemplate = getJObject "Resources" templateBody
        let resourceDataByLogicalID = Dictionary<string, Dictionary<string, string>>()
        for resource in templateDetails.Resources do
            let resourceData = Dictionary<string, string>()
            // add resource identifiers from template resources
            for pair in resource.ResourceIdentifier do
                resourceData.Add(pair.Key, pair.Value)

            // add inputs from template resources
            let resourceJson = getJObject resource.LogicalResourceId resourcesFromTemplate
            let resourceProperties = getJObject "Properties" resourceJson
            for property in resourceProperties.Properties() do
                if property.Value.Type = JTokenType.String && not (resourceData.ContainsKey property.Name)
                    then resourceData.Add(property.Name, property.Value.ToObject<string>())

            resourceDataByLogicalID.Add(resource.LogicalResourceId, resourceData)

        for resource in templateDetails.Resources do
            let includeResource =
                not (cloudFormationResourceToSkip.Contains resource.ResourceType)
                && not (resource.ResourceType.StartsWith "Custom")

            if includeResource then
                let resourceJson = JObject()
                match AwsCloudFormationGeneratedTemplates.remapSpecifications.TryFind resource.ResourceType with
                | None ->
                    match AwsCloudFormation.mapToPulumi resource.ResourceType with
                    | Some pulumiType ->
                        resourceJson.Add("type", pulumiType)
                        if resource.ResourceIdentifier.Count > 0 then
                            let value = Seq.head resource.ResourceIdentifier.Values
                            resourceJson.Add("id", value)
                        else
                            resourceJson.Add("id", "")
                            errors.Add $"CloudFormation resource '{resource.ResourceType}' does not have an identifier"
                    | None ->
                        resourceJson.Add("type", resource.ResourceType)
                        errors.Add $"CloudFormation resource '{resource.ResourceType}' did not have a corresponding Pulumi type"
                        if resource.ResourceIdentifier.Count > 0 then
                            let value = Seq.head resource.ResourceIdentifier.Values
                            resourceJson.Add("id", value)
                        else
                            resourceJson.Add("id", "")
                            errors.Add $"CloudFormation resource '{resource.ResourceType}' does not have an identifier"

                | Some pulumiMapping ->
                    resourceJson.Add("type", pulumiMapping.pulumiType)
                    let importPartValues =
                        pulumiMapping.importIdentityParts
                        |> Seq.choose (fun part ->
                            match resourceDataByLogicalID[resource.LogicalResourceId].TryGetValue(part) with
                            | true, value -> Some value
                            | _ -> None)
                        |> String.concat pulumiMapping.delimiter

                    resourceJson.Add("id", importPartValues)

                resourceJson.Add("name", resource.LogicalResourceId.Replace("-", "_"))
                resourcesJson.Add(resourceJson)

        pulumiImportJson.Add("resources", resourcesJson)

        return Ok {
            templateBody = templateBody.ToString(Formatting.Indented)
            resourceDataJson = (JObject.FromObject resourceDataByLogicalID).ToString(Formatting.Indented)
            pulumiImportJson = pulumiImportJson.ToString(Formatting.Indented)
            errors = List.ofSeq errors
        }
    with
    | error ->
        let errorType = error.GetType().Name
        return Error $"{errorType}: {error.Message}"
}

let isValidJson (json: string) =
    try
        let _ = JObject.Parse json
        true
    with
    | _ -> false

let [<Literal>] IdProperty = "Id"
let [<Literal>] ArnProperty = "Arn"

let getImportIdentityParts
    (resourceType: string)
    : Option<seq<string>> =      
    match AwsCloudFormationTemplates.remapSpecifications.TryFind resourceType with
    | Some(spec) -> Some spec.importIdentityParts
    | _ -> None

let addImportIdentityParts
    (resourceId: string)
    (resourceType: string)
    (properties: JObject)
    (data: Dictionary<string, Dictionary<string, string>>) =
    let importIdentityParts = getImportIdentityParts resourceType
    match importIdentityParts with
    | Some(parts) ->
        let definedImportIdentityParts = parts |> Seq.filter (fun part -> properties.ContainsKey part)
        for part in definedImportIdentityParts do
            if not (data.ContainsKey resourceId) then data.Add(resourceId, Dictionary<string, string>())
            let prop = properties[part]
            if not (data[resourceId].ContainsKey part) then data[resourceId].Add(part, prop.ToString())
    | _ -> ()

// returns resource dependency data and cfn template as JObject
let templateBodyData (cloudformationTemplate: GetTemplateResponse) (resources: seq<AwsCloudFormationResource>) =
    let data = Dictionary<string, Dictionary<string, string>>()
    // convert cloudformation template body to JObject
    let bodyJson =
        if isValidJson cloudformationTemplate.TemplateBody
        then JObject.Parse cloudformationTemplate.TemplateBody
        else Yaml.convertToJson cloudformationTemplate.TemplateBody

    // initialize return data with map of logical id to map of "Id" to resourceId
    for resource in resources do
        let properties = Dictionary<string, string>()
        properties.Add(IdProperty, resource.resourceId)
        data.Add(resource.logicalId, properties)

    // get Resources block from cfn template as JObject
    let resourcesFromTemplate = getJObject "Resources" bodyJson
    // loop over resources within the Resources template block 
    // TODO: since this loops over the template, instead of the filtered resource list,
    // will this function add entries for skipped resources to the return data?
    for property in resourcesFromTemplate.Properties() do
        // property.Name is cfn logical id
        let resourceId = property.Name
        let resource = getJObject resourceId resourcesFromTemplate
        let properties = getJObject "Properties" resource

        for property in properties.Properties() do
            // if the property is a reference property...
            if property.Name.EndsWith IdProperty || property.Name.EndsWith ArnProperty || property.Name.EndsWith "Name" then
                let referenceProperty = getJObject property.Name properties
                // if the reference is to another resource in the template...
                if referenceProperty.ContainsKey "Ref" then
                    let referencedResourceLogicalId = referenceProperty["Ref"].ToObject<string>()
                    if data.ContainsKey referencedResourceLogicalId then
                        if data[referencedResourceLogicalId].ContainsKey IdProperty then
                            let id = data[referencedResourceLogicalId][IdProperty]
                            if not (data.ContainsKey resourceId) then data.Add(resourceId, Dictionary<string, string>())
                            // add map of ref property name to id of referenced resource to return data map under 
                            // logical id referring resource
                            data[resourceId].Add(property.Name, id)

                // if the reference is to an attribute of another resource in the template...
                if referenceProperty.ContainsKey "Fn::GetAtt" && referenceProperty["Fn::GetAtt"].Type = JTokenType.Array then
                    let getAtt = referenceProperty["Fn::GetAtt"].ToObject<JArray>()
                    if getAtt.Count = 2 && getAtt[0].Type = JTokenType.String then
                        let referencedResourceLogicalId = getAtt[0].ToObject<string>()
                        if data.ContainsKey referencedResourceLogicalId then
                            if data[referencedResourceLogicalId].ContainsKey IdProperty then
                                let id = data[referencedResourceLogicalId][IdProperty]
                                if not (data.ContainsKey resourceId) then data.Add(resourceId, Dictionary<string, string>())
                                // add map of ref property name to id of referenced resource to return data map under 
                                // logical id referring resource
                                data[resourceId].Add(property.Name, id)
        // add importIdentityParts that have not already been added as reference properties
        addImportIdentityParts (resourceId.ToString()) ((resource["Type"]).ToString()) properties data
    data, bodyJson

let routeTableAssociationType = "AWS::EC2::SubnetRouteTableAssociation"
let cloudFormationLoadBalancerType = "AWS::ElasticLoadBalancingV2::LoadBalancer"

let getRouteTables (resourceTypes: Set<string>) (region: string) = task {
    if resourceTypes.Contains routeTableAssociationType then
        let client = ec2Client region
        let! response = client.DescribeRouteTablesAsync(DescribeRouteTablesRequest())
        return response.RouteTables
    else
        return ResizeArray()
}

let getElasticIps (resourceTypes: Set<string>) (region: string) = task {
    if resourceTypes.Contains "AWS::EC2::EIP" then
        let client = ec2Client region
        let request = DescribeAddressesRequest()
        let! response = client.DescribeAddressesAsync(request)
        return Map.ofList [ for address in response.Addresses -> address.PublicIp, address ]
    else
        return Map.empty
}

let getIamPolicies (resourceTypes: Set<string>) (region: string) = task {
    if resourceTypes.Contains "AWS::IAM::Policy" then
        let client = iamClient region
        let! response = client.ListPoliciesAsync(ListPoliciesRequest(MaxItems=1000))
        return response.Policies
    else
        return ResizeArray []
}

let getLoadBalancers (resourceTypes: Set<string>) (region: string) = task {
    if resourceTypes.Contains cloudFormationLoadBalancerType then
        let client = elasticLoadBalancingV2Client region
        let! response = client.DescribeLoadBalancersAsync(DescribeLoadBalancersRequest())
        return Map.ofList [ for lb in response.LoadBalancers -> lb.LoadBalancerArn, lb ]
    else
        return Map.empty
}

let getInternetGateways (vpcId: string) (region: string)= task {
    let client = ec2Client region
    let request = DescribeInternetGatewaysRequest()
    request.Filters <- ResizeArray [
        Amazon.EC2.Model.Filter(Name="attachment.vpc-id", Values=ResizeArray[ vpcId ])
    ]
    let! response = client.DescribeInternetGatewaysAsync(request)
    return response.InternetGateways
}

let getImportIdsForVPCGatewayAttachments 
    (cloudformationResources: seq<AwsCloudFormationResource>) 
    (region: string ) = task {
    let data = Dictionary<string, string>()
    let vpcGatewayAttachments = 
            cloudformationResources
            |> Seq.filter (fun resource -> resource.resourceType = "AWS::EC2::VPCGatewayAttachment")
    for resource in vpcGatewayAttachments do
        match resource.resourceId.Split('|') with
        | [| _; vpcId |] ->
            let! gateways = getInternetGateways vpcId region
            match gateways |> Seq.tryHead with
            | Some gateway ->
                data.Add(resource.logicalId, $"{gateway.InternetGatewayId}:{vpcId}")
            | None ->
                data.Add(resource.logicalId, resource.resourceId)
        | _ ->
            data.Add(resource.logicalId, resource.resourceId)
    return data
}

let getRemappedImportProps 
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    : Option<RemappedSpecResult> =      
    
    AwsCloudFormationTemplates.remapSpecifications
    |> Seq.tryFind (fun pair -> pair.Key = resource.resourceType)
    |> Option.map (fun pair -> pair.Value)
    |> Option.filter (fun spec -> spec.validatorFunc resource resourceData spec)
    |> Option.map (fun spec -> spec.remapFunc resource resourceData spec)

let getPulumiImportJson 
    (cloudformationResources: seq<AwsCloudFormationResource>) 
    (loadBalancers: Map<string,LoadBalancer>)
    (elasticIps: Map<string,Address>)
    (routeTables: List<RouteTable>)
    (iamPolicies: List<ManagedPolicy>)
    (gatewayAttachmentImportIds: Dictionary<string,string>)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    : JObject * ResizeArray<string> =
    // initialize return data
    let pulumiImportJson = JObject()
    let resourcesJson = JArray()
    let errors = ResizeArray()
    // loop over filtered resources to construct pulumiImportJson
    for resource in cloudformationResources do
        let resourceJson = JObject()

        match getRemappedImportProps resource resourceData with
        | Some (remappedSpecResult) -> 
            resourceJson.Add("type", remappedSpecResult.resourceType)
            resourceJson.Add("name", remappedSpecResult.logicalId)
            resourceJson.Add("id", remappedSpecResult.importId)
        | _ ->
            // set the resource type
            if resource.resourceType = cloudFormationLoadBalancerType then
                // special handling for load balancer type
                match loadBalancers.TryGetValue resource.resourceId with
                | true, balancer when balancer.Type = LoadBalancerTypeEnum.Application ->
                    resourceJson.Add("type", "aws:alb/loadBalancer:LoadBalancer")
                | _ ->
                    resourceJson.Add("type", "aws:elb/loadBalancer:LoadBalancer")
            else
                // set type as pulumi type from AwsCloudFormation.mapToPulumi (autogenerated from ?)
                match AwsCloudFormation.mapToPulumi resource.resourceType with
                | Some pulumiType ->
                    resourceJson.Add("type", pulumiType)
                | None ->
                    resourceJson.Add("type", resource.resourceType)
                    errors.Add $"CloudFormation resource '{resource.resourceType}' did not have a corresponding Pulumi type"
            
            // set the import id
            // special handling for certain resource types...
            if resource.resourceType = "AWS::ApiGateway::Method" then
                let methodId = resource.resourceId.Replace("|", "/")
                resourceJson.Add("id", methodId)
            elif resource.resourceType = "AWS::EC2::Route" then
                let routeId = resource.resourceId.Replace("|", "_")
                resourceJson.Add("id", routeId)
            elif resource.resourceType = "AWS::ApiGateway::UsagePlanKey" then
                let usagePlanKeyId = resource.resourceId.Replace(":", "/")
                resourceJson.Add("id", usagePlanKeyId)
            elif resource.resourceType = "AWS::EC2::EIP" then
                match elasticIps.TryGetValue resource.resourceId with
                | true, eip ->
                    resourceJson.Add("id", eip.AllocationId)
                | _ ->
                    resourceJson.Add("id", resource.resourceId)
            elif resource.resourceType = "AWS::IAM::Policy" then
                let id = resource.resourceId
                let foundPolicy =
                    iamPolicies
                    |> Seq.tryFind (fun policy -> policy.PolicyId = id || policy.PolicyName = id)
                match foundPolicy with
                | Some policy ->
                    resourceJson.Add("id", policy.Arn)
                | _ ->
                    resourceJson.Add("id", resource.resourceId)
            elif resource.resourceType = "AWS::EC2::VPCGatewayAttachment" then
                match gatewayAttachmentImportIds.TryGetValue resource.logicalId with
                | true, importId ->
                    resourceJson.Add("id", importId)
                |_ ->
                    resourceJson.Add("id", resource.resourceId)
            elif resource.resourceType = routeTableAssociationType then
                let routeTableAssociationId = resource.resourceId
                let association =
                    routeTables
                    |> Seq.collect (fun table -> table.Associations)
                    |> Seq.tryFind (fun association -> association.RouteTableAssociationId = routeTableAssociationId)

                match association with
                | Some association ->
                    let importId = $"{association.SubnetId}/{association.RouteTableId}"
                    resourceJson.Add("id", importId)
                | _ ->
                    resourceJson.Add("id", resource.resourceId)
            else
                // base case: set the import id to the resourceId
                resourceJson.Add("id", resource.resourceId)
            
            // set resource name as cfn resource logical id with underscores replaced with dashes
            resourceJson.Add("name", resource.logicalId.Replace("-", "_"))

        resourcesJson.Add(resourceJson)

    pulumiImportJson.Add("resources", resourcesJson)
    (pulumiImportJson, errors)

let getAwsCloudFormationResources (stack: AwsCloudFormationStack) = task {
    try
        // instantiate cloudformation client
        let client = cloudFormationClient stack.region
        // get cloudformation stack template from aws api
        let! stackTemplate = client.GetTemplateAsync(GetTemplateRequest(StackName=stack.stackId))
        // get list of cloudformation stack resources from aws api
        let! response = client.ListStackResourcesAsync(ListStackResourcesRequest(StackName=stack.stackId))

        let cloudformationResources : seq<AwsCloudFormationResource> =
            // start with list of resources
            response.StackResourceSummaries
            // filter out resources that should be skipped (bc only exist in cloudformation)
            |> Seq.filter (fun resource -> not (cloudFormationResourceToSkip.Contains resource.ResourceType))
            // filter out custom resources
            |> Seq.filter (fun resource -> not (resource.ResourceType.StartsWith "Custom::"))
            // return logicalId, resourceId, resourceType
            |> Seq.map (fun resource ->
                { logicalId = resource.LogicalResourceId
                  resourceId = resource.PhysicalResourceId
                  resourceType = resource.ResourceType })

        // get resource dependency map and cfn template as JObject
        let resourceData, bodyJson = templateBodyData stackTemplate cloudformationResources

        // get set of resource types
        let resourceTypes = Set [ for resource in cloudformationResources -> resource.resourceType ]
        
        // fetch additional info from aws api about certain types
        // get map of load balancer arn to load balancer details
        let! loadBalancers = getLoadBalancers resourceTypes stack.region

        // get ResizeArray of route tables
        let! routeTables = getRouteTables resourceTypes stack.region

        // get map of public IPs to EIP details
        let! elasticIps = getElasticIps resourceTypes stack.region

        // get ResizeArray of IAM Policies
        let! iamPolicies = getIamPolicies resourceTypes stack.region

        // get map of logical ids of vpc gateway attachments to import ids
        let! gatewayAttachmentImportIds = getImportIdsForVPCGatewayAttachments cloudformationResources stack.region

        let (pulumiImportJson, errors) = getPulumiImportJson 
                                            cloudformationResources
                                            loadBalancers
                                            elasticIps
                                            routeTables
                                            iamPolicies
                                            gatewayAttachmentImportIds
                                            resourceData
        return Ok {
            resources = List.ofSeq cloudformationResources
            pulumiImportJson = pulumiImportJson.ToString(Formatting.Indented)
            warnings = []
            errors = errors |> Seq.distinct |> Seq.toList
            templateBody = bodyJson.ToString(Formatting.Indented)
            resourceDataJson = (JObject.FromObject resourceData).ToString(Formatting.Indented)
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

let tempDirectory (f: string -> 't) =
    let tempDir = Path.GetTempPath()
    let dir = Path.Combine(tempDir, $"pulumi-test-{Guid.NewGuid()}")
    try
        let info = Directory.CreateDirectory dir
        f info.FullName
    finally
        Directory.Delete(dir, true)

let invalidTypesInImportJson (json: string) =
    let parsedJson = JObject.Parse json
    if parsedJson.ContainsKey "resources" && parsedJson.["resources"].Type = JTokenType.Array then
        let resources = parsedJson["resources"] :?> JArray
        resources
        |> Seq.filter (fun resource -> resource.Type = JTokenType.Object)
        |> Seq.map (fun resource -> resource :?> JObject)
        |> Seq.choose (fun resource ->
            let resourceType = resource["type"].ToObject<string>()
            if resourceType.StartsWith "AWS::" || resourceType.Split(":").Length <> 3
            then Some resourceType
            else None)
        |> Seq.distinct
    else
        Seq.empty

let importPreview (request: ImportPreviewRequest) = task {
    try
        if not (isValidJson request.pulumiImportJson) then
            return Error "Invalid JSON provided for Pulumi Import JSON"
        else
        let invalidTypes = invalidTypesInImportJson request.pulumiImportJson
        if not (Seq.isEmpty invalidTypes) then
            let types =
                invalidTypes
                |> Seq.map (fun token -> $"'{token}'")
                |> String.concat ", "

            return Error $"Invalid Pulumi resource types found in the Import JSON: {types}"
        else
        let! pulumiCli = pulumiCliBinary()
        let response = tempDirectory <| fun tempDir ->
            let exec (args:string) =
                Cli.Wrap(pulumiCli)
                   .WithWorkingDirectory(tempDir)
                   .WithArguments(args)
                   .WithEnvironmentVariables(fun config ->
                       if request.region <> "" && request.region <> unsetDefaultRegion then
                            config
                                .Set("PULUMI_CONFIG_PASSPHRASE", "whatever")
                                .Set("AWS_REGION", request.region)
                                .Build()
                            |> ignore
                       else
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

            Ok {
                generatedCode = generatedCode
                stackState = stackState
                warnings = warnings
                standardOutput = pulumiImportOutput.StandardOutput
                standardError =
                    if String.IsNullOrWhiteSpace generatedCode && not (String.IsNullOrWhiteSpace pulumiImportOutput.StandardError)
                    then Some pulumiImportOutput.StandardError
                    else None
            }

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
    getResourceGroups = Azure.getResourceGroups >> Async.AwaitTask
    azureAccount = Azure.account >> Async.AwaitTask
    getResourcesUnderResourceGroup = Azure.getResourcesUnderResourceGroup >> Async.AwaitTask
    importPreview = importPreview >> Async.AwaitTask
    getAwsCloudFormationStacks = getAwsCloudFormationStacks >> Async.AwaitTask
    getAwsCloudFormationResources = getAwsCloudFormationResources >> Async.AwaitTask
    getAwsCloudFormationGeneratedTemplates = getAwsCloudFormationGeneratedTemplates >> Async.AwaitTask
    getAwsCloudFormationGeneratedTemplate = getAwsCloudFormationGeneratedTemplate >> Async.AwaitTask
    googleProjects = Google.projects >> Async.AwaitTask
    googleResourcesByProject = Google.resourcesByProject >> Async.AwaitTask
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