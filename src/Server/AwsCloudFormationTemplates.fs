module AwsCloudFormationTemplates

open System
open System.Reflection

open Amazon.EC2.Model
open Newtonsoft.Json.Linq

open AwsCloudFormationTypes
open System.Collections.Generic
open Shared

let private (=>) key value = key, value

let getPulumiType (cfnType: string) =
    match AwsCloudFormation.mapToPulumi cfnType with
    | Some(pulumiType) -> pulumiType
    | None -> ""

let hasImportIdentityParts
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (spec: CustomRemapSpecification) 
    : bool =
    spec.importIdentityParts
    |> Seq.forall (fun part -> resourceData[resource.logicalId].ContainsKey part)

let validateFromImportIdentityParts
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (spec: CustomRemapSpecification) 
    : bool =
    resourceData.ContainsKey resource.logicalId
    && hasImportIdentityParts resource resourceData spec

// for resources whose import id can be constructed from resource references in the template
let remapFromImportIdentityParts
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult = 
    let data = resourceData[resource.logicalId]
    let importId =
        spec.importIdentityParts
        |> Seq.map (fun partKey -> data[partKey])
        |> String.concat spec.delimiter
    let resourceType = spec.pulumiType
    let logicalId = resource.logicalId.Replace("-", "_")
    {
        resourceType = resourceType
        logicalId = logicalId
        importId = importId
    }

let remapFromImportIdentityPartsDNSRecord
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult = 
    let data = resourceData[resource.logicalId]
    let importId =
        spec.importIdentityParts
        |> Seq.map (fun partKey -> data[partKey])
        |> String.concat spec.delimiter
        |> (fun id -> 
            if data.ContainsKey("SetIdentifier") then 
                String.concat spec.delimiter [id; data["SetIdentifier"]]
            else
                id)
    let resourceType = spec.pulumiType
    let logicalId = resource.logicalId.Replace("-", "_")
    {
        resourceType = resourceType
        logicalId = logicalId
        importId = importId
    }

// for resources whose physical id is the arn
let remapFromIdAsArn
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult = 
    let data = resourceData[resource.logicalId]
    let importIdParts = data["Id"].Split("/")[1..]
    let importId = String.Join(spec.delimiter, importIdParts)
    let resourceType = spec.pulumiType
    let logicalId = resource.logicalId.Replace("-", "_")
    {
        resourceType = resourceType
        logicalId = logicalId
        importId = importId
    }

let validateAppScalingPolicy
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (spec: CustomRemapSpecification) 
    : bool =
    (resourceData.ContainsKey resource.logicalId)
    && (hasImportIdentityParts resource resourceData spec)
    && ((((resourceData[resource.logicalId])["ScalingTargetId"]).Split("|")).Length = 3)

let remapAppScalingPolicy
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult = 
    let data = resourceData[resource.logicalId]
    let scalingTargetIdParts = data["ScalingTargetId"].Split("|")
    let importId = 
        if scalingTargetIdParts.Length = 3 then
            [scalingTargetIdParts[2]; scalingTargetIdParts[0]; scalingTargetIdParts[1]; data["PolicyName"]]
            |> String.concat spec.delimiter
        else ""
                
    let resourceType = spec.pulumiType
    let logicalId = resource.logicalId.Replace("-", "_")
    {
        resourceType = resourceType
        logicalId = logicalId
        importId = importId
    }

let remapAppScalableTarget
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult = 
    let data = resourceData[resource.logicalId]
    let physicalIdParts = data["Id"].Split("|")
    let importId = 
        if physicalIdParts.Length = 3 then
            [physicalIdParts[2]; physicalIdParts[0]; physicalIdParts[1]]
            |> String.concat spec.delimiter
        else ""
                
    let resourceType = spec.pulumiType
    let logicalId = resource.logicalId.Replace("-", "_")
    {
        resourceType = resourceType
        logicalId = logicalId
        importId = importId
    }

let validateSecurityGroupRule
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (spec: CustomRemapSpecification) 
    : bool =
    ((resourceData[resource.logicalId]).ContainsKey "IpProtocol") &&
    ((resourceData[resource.logicalId]).ContainsKey "GroupId")

let filterSecurityGroupRules
    (data: Dictionary<string,string>)
    (rules: Map<string,JObject>)
    : Map<string,JObject> =
    rules
    |> Map.filter (fun id props -> 
        props.Property("GroupId").Value.ToString() = data["GroupId"])
    |> Map.filter (fun id props ->
        data
        |> Seq.forall (fun entry ->
            if entry.Key = "Id" || entry.Key = "resourceType" then true
            elif props.ContainsKey entry.Key then
                let prop = props.Property(entry.Key)
                prop.Value.ToString() = data[prop.Name]
            else false
        ))

let remapSecurityGroupIngress
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult =
    let data = resourceData[resource.logicalId]
    let filteredRules = filterSecurityGroupRules data resourceContext.securityGroupIngressRules
    let importId =
        if not ((Seq.length filteredRules) = 1) then 
            ""
        else (Seq.exactlyOne filteredRules).Key
    
    let resourceType = spec.pulumiType
    let logicalId = resource.logicalId.Replace("-", "_")
    {
        resourceType = resourceType
        logicalId = logicalId
        importId = importId
    }

let remapSecurityGroupEgress
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult =
    let data = resourceData[resource.logicalId]
    let filteredRules = filterSecurityGroupRules data resourceContext.securityGroupEgressRules
    let importId =
        if not ((Seq.length filteredRules) = 1) then 
            ""
        else (Seq.exactlyOne filteredRules).Key
    
    let resourceType = spec.pulumiType
    let logicalId = resource.logicalId.Replace("-", "_")
    {
        resourceType = resourceType
        logicalId = logicalId
        importId = importId
    }

let remapLayerVersionPermission
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult =
    let data = resourceData[resource.logicalId]
    let layerVersionArnParts = data["LayerVersionArn"].Split(":")
    let layerVersionArnPartsLen = Seq.length layerVersionArnParts
    let layerVersionIndex = layerVersionArnPartsLen - 1
    let layerVersion = layerVersionArnParts[layerVersionIndex]
    let layerArnParts = Seq.truncate layerVersionIndex layerVersionArnParts
    let layerArn = String.concat ":" layerArnParts
    let importId = String.concat spec.delimiter [layerArn; layerVersion] 
    let resourceType = spec.pulumiType
    let logicalId = resource.logicalId.Replace("-", "_")
    {
        resourceType = resourceType
        logicalId = logicalId
        importId = importId
    }

let remapSpecifications = Map.ofList [
    "AWS::ApiGateway::Resource" => {
        pulumiType = "aws:apigateway/resource:Resource"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::ApiGateway::Stage" => {
        pulumiType = "aws:apigateway/stage:Stage"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::ApiGateway::Deployment" => {
        pulumiType = getPulumiType "AWS::ApiGateway::Deployment"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::ApiGateway::UsagePlanKey" => {
        pulumiType = getPulumiType "AWS::ApiGateway::UsagePlanKey"
        importIdentityParts = ["UsagePlanId"; "KeyId"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::ApplicationAutoScaling::ScalingPolicy" => {
        pulumiType = getPulumiType "AWS::ApplicationAutoScaling::ScalingPolicy"
        // as per pulumi registry, import id should be made up of service-namespace , resource-id, scalable-dimension 
        // and policy-name separated by /
        // ServiceNamespace, ResourceId and ScalableDimension can be parsed out of ScalingTargetId
        importIdentityParts = ["ScalingTargetId"; "PolicyName"]
        delimiter = "/"
        remapFunc = remapAppScalingPolicy
        validatorFunc = validateAppScalingPolicy
    }

    "AWS::ApplicationAutoScaling::ScalableTarget" => {
        // as per pulumi registry, import Application AutoScaling Target using the service-namespace , 
        // resource-id and scalable-dimension separated by /
        // all three can be parsed from the physical id
        pulumiType = getPulumiType "AWS::ApplicationAutoScaling::ScalableTarget"
        importIdentityParts = ["Id"]
        delimiter = "/"
        remapFunc = remapAppScalableTarget
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::Cognito::UserPoolClient" => {
        pulumiType = getPulumiType "AWS::Cognito::UserPoolClient"
        importIdentityParts = ["UserPoolId"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::EC2::SubnetRouteTableAssociation" => {
        pulumiType = getPulumiType "AWS::EC2::SubnetRouteTableAssociation"
        importIdentityParts = ["SubnetId"; "RouteTableId"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::EC2::SecurityGroupIngress" => {
        pulumiType = getPulumiType "AWS::EC2::SecurityGroupIngress"
        importIdentityParts = [
            "GroupId";
            "CidrIp";
            "Description";
            "FromPort";
            "ToPort"
            "IpProtocol";
            "SourceSecurityGroupOwnerId";
        ]
        delimiter = ""
        remapFunc = remapSecurityGroupIngress
        validatorFunc = validateSecurityGroupRule
    }

    "AWS::EC2::SecurityGroupEgress" => {
        pulumiType = getPulumiType "AWS::EC2::SecurityGroupEgress"
        importIdentityParts = [
            "GroupId";
            "CidrIp";
            "Description";
            "DestinationPrefixListId";
            "DestinationSecurityGroupId";
            "FromPort";
            "ToPort";
            "IpProtocol"
        ]
        delimiter = ""
        remapFunc = remapSecurityGroupEgress
        validatorFunc = validateSecurityGroupRule
    }

    "AWS::ECS::Service" => {
        pulumiType = getPulumiType "AWS::ECS::Service"
        importIdentityParts = ["Id"]
        delimiter = "/"
        remapFunc = remapFromIdAsArn
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::ElasticLoadBalancingV2::ListenerCertificate" => {
        pulumiType = getPulumiType "AWS::ElasticLoadBalancingV2::ListenerCertificate"
        importIdentityParts = ["ListenerArn"; "Certificates"]
        delimiter = "_"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::Lambda::Permission" => {
        pulumiType = getPulumiType "AWS::Lambda::Permission"
        importIdentityParts = ["FunctionName"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::Lambda::LayerVersionPermission" => {
        pulumiType = getPulumiType "AWS::Lambda::LayerVersionPermission"
        importIdentityParts = ["LayerVersionArn"]
        delimiter = ","
        remapFunc = remapLayerVersionPermission
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::Lambda::EventInvokeConfig" => {
        pulumiType = getPulumiType "AWS::Lambda::EventInvokeConfig"
        importIdentityParts = ["FunctionName"; "Qualifier"]
        delimiter = ":"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::Route53::RecordSet" => {
        pulumiType = getPulumiType "AWS::Route53::RecordSet"
        importIdentityParts = ["HostedZoneId"; "Name"; "Type"]
        delimiter = "_"
        remapFunc = remapFromImportIdentityPartsDNSRecord
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::S3::BucketPolicy" => {
        pulumiType = getPulumiType "AWS::S3::BucketPolicy"
        importIdentityParts = ["Bucket"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }

    "AWS::SQS::QueuePolicy" => {
        pulumiType = getPulumiType "AWS::SQS::QueuePolicy"
        importIdentityParts = ["Queues"]
        delimiter = ""
        remapFunc = remapFromImportIdentityParts
        validatorFunc = validateFromImportIdentityParts
    }
]