module AwsCloudFormationTemplates

open System

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

let remapFromImportIdentityPartsValidator
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

let remapFromImportIdentityPartsValidatorAppScalingPolicy
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (spec: CustomRemapSpecification) 
    : bool =
    resourceData.ContainsKey resource.logicalId
    && hasImportIdentityParts resource resourceData spec
    && ((resourceData[resource.logicalId])["ScalingTargetId"]).Length = 3

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

let remapIngressRule
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult = 
    let data = resourceData[resource.logicalId]
    let groupId = data[((spec.importIdentityParts)[0])]
    let importId = 
        if resourceContext.securityGroupRuleIds.ContainsKey groupId then
            let rules = ((resourceContext.securityGroupRuleIds)[groupId])
            let ingressRules = rules |> Seq.filter (fun rule -> not rule.IsEgress)
            if not ((Seq.length ingressRules) = 1) then ""
            else (Seq.exactlyOne ingressRules).SecurityGroupRuleId
        else ""
  
    let resourceType = spec.pulumiType
    let logicalId = resource.logicalId.Replace("-", "_")
    {
        resourceType = resourceType
        logicalId = logicalId
        importId = importId
    }

let remapEgressRule
    (resource: AwsCloudFormationResource)
    (resourceData: Dictionary<string, Dictionary<string,string>>)
    (resourceContext: AwsResourceContext)
    (spec: CustomRemapSpecification) 
    : RemappedSpecResult = 
    let data = resourceData[resource.logicalId]
    let groupId = data[((spec.importIdentityParts)[0])]
    let importId = 
        if resourceContext.securityGroupRuleIds.ContainsKey groupId then
            let rules = ((resourceContext.securityGroupRuleIds)[groupId])
            let egressRules = rules |> Seq.filter (fun rule -> rule.IsEgress)
            if not ((Seq.length egressRules) = 1) then ""
            else (Seq.exactlyOne egressRules).SecurityGroupRuleId
        else ""
  
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
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::ApiGateway::Stage" => {
        pulumiType = "aws:apigateway/stage:Stage"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::ApiGateway::Deployment" => {
        pulumiType = getPulumiType "AWS::ApiGateway::Deployment"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::ApiGateway::UsagePlanKey" => {
        pulumiType = getPulumiType "AWS::ApiGateway::UsagePlanKey"
        importIdentityParts = ["UsagePlanId"; "KeyId"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::ApplicationAutoScaling::ScalingPolicy" => {
        pulumiType = getPulumiType "AWS::ApplicationAutoScaling::ScalingPolicy"
        // as per pulumi registry, import id should be made up of service-namespace , resource-id, scalable-dimension 
        // and policy-name separated by /
        // ServiceNamespace, ResourceId and ScalableDimension can be parsed out of ScalingTargetId
        importIdentityParts = ["ScalingTargetId"; "PolicyName"]
        delimiter = "/"
        remapFunc = remapAppScalingPolicy
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::ApplicationAutoScaling::ScalableTarget" => {
        // as per pulumi registry, import Application AutoScaling Target using the service-namespace , 
        // resource-id and scalable-dimension separated by /
        // all three can be parsed from the physical id
        pulumiType = getPulumiType "AWS::ApplicationAutoScaling::ScalableTarget"
        importIdentityParts = ["Id"]
        delimiter = "/"
        remapFunc = remapAppScalableTarget
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::EC2::SubnetRouteTableAssociation" => {
        pulumiType = getPulumiType "AWS::EC2::SubnetRouteTableAssociation"
        importIdentityParts = ["SubnetId"; "RouteTableId"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::EC2::SecurityGroupIngress" => {
        pulumiType = getPulumiType "AWS::EC2::SecurityGroupIngress"
        importIdentityParts = ["GroupId"]
        delimiter = ""
        remapFunc = remapIngressRule
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::EC2::SecurityGroupEgress" => {
        pulumiType = getPulumiType "AWS::EC2::SecurityGroupEgress"
        importIdentityParts = ["GroupId"]
        delimiter = ""
        remapFunc = remapEgressRule
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::ECS::Service" => {
        pulumiType = getPulumiType "AWS::ECS::Service"
        importIdentityParts = ["Id"]
        delimiter = "/"
        remapFunc = remapFromIdAsArn
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::ElasticLoadBalancingV2::ListenerCertificate" => {
        pulumiType = getPulumiType "AWS::ElasticLoadBalancingV2::ListenerCertificate"
        importIdentityParts = ["ListenerArn"; "Certificates"]
        delimiter = "_"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::Lambda::Permission" => {
        pulumiType = getPulumiType "AWS::Lambda::Permission"
        importIdentityParts = ["FunctionName"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::Lambda::EventInvokeConfig" => {
        pulumiType = getPulumiType "AWS::Lambda::EventInvokeConfig"
        importIdentityParts = ["FunctionName"; "Qualifier"]
        delimiter = ":"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::Route53::RecordSet" => {
        pulumiType = getPulumiType "AWS::Route53::RecordSet"
        importIdentityParts = ["HostedZoneId"; "Name"; "Type"]
        delimiter = "_"
        remapFunc = remapFromImportIdentityPartsDNSRecord
        validatorFunc = remapFromImportIdentityPartsValidator
    }

    "AWS::S3::BucketPolicy" => {
        pulumiType = getPulumiType "AWS::S3::BucketPolicy"
        importIdentityParts = ["Bucket"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
        validatorFunc = remapFromImportIdentityPartsValidator
    }
]