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

// for resources whose import id can be constructed from resource references in the template
let remapFromImportIdentityParts = 
    (fun 
        (resource: AwsCloudFormationResource)
        (resourceData: Dictionary<string, Dictionary<string,string>>)
        (spec: CustomRemapSpecification) -> 
        let data = resourceData[resource.logicalId]
        let importId =
            spec.importIdentityParts
            |> Seq.map (fun partKey -> data[partKey])
            |> String.concat spec.delimiter
        let resourceType = spec.pulumiType
        let name = resource.logicalId.Replace("-", "_")
        resourceType, name, importId)

let remapFromImportIdentityPartsListenerCertificate =
    (fun 
        (resource: AwsCloudFormationResource)
        (resourceData: Dictionary<string, Dictionary<string,string>>)
        (spec: CustomRemapSpecification) -> 
        let data = resourceData[resource.logicalId]
        let listenerArn = data["ListenerArn"]
        let certificates = JArray.Parse data["Certificates"]
        let certificateObj = certificates[0]
        let certificateArn = certificateObj["CertificateArn"].ToObject<string>()
        let importId = String.concat spec.delimiter [listenerArn; certificateArn]
        let resourceType = spec.pulumiType
        let name = resource.logicalId.Replace("-", "_")
        resourceType, name, importId)

// for resources whose physical id is the arn
let remapFromIdAsArn =
    (fun
        (resource: AwsCloudFormationResource)
        (resourceData: Dictionary<string, Dictionary<string,string>>)
        (spec: CustomRemapSpecification) -> 
        let data = resourceData[resource.logicalId]
        let importIdParts = data["Id"].Split("/")[1..]
        let importId = String.Join("/", importIdParts)
        let resourceType = spec.pulumiType
        let name = resource.logicalId.Replace("-", "_")
        resourceType, name, importId)

let remapSpecifications = Map.ofList [
    "AWS::ApiGateway::Resource" => {
        pulumiType = "aws:apigateway/resource:Resource"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
    }

    "AWS::ApiGateway::Stage" => {
        pulumiType = "aws:apigateway/stage:Stage"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
    }

    "AWS::ApiGateway::Deployment" => {
        pulumiType = "aws:apigateway/deployment:Deployment"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
    }

    "AWS::ApiGateway::UsagePlanKey" => {
        pulumiType = "aws:apigateway/usagePlanKey:UsagePlanKey"
        importIdentityParts = ["UsagePlanId"; "KeyId"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
    }

    "AWS::EC2::SubnetRouteTableAssociation" => {
        pulumiType = "aws:ec2/routeTableAssociation:RouteTableAssociation"
        importIdentityParts = ["SubnetId"; "RouteTableId"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
    }

    "AWS::ECS::Service" => {
        pulumiType = "aws:ecs/service:Service"
        importIdentityParts = []
        delimiter = "/"
        remapFunc = remapFromIdAsArn
    }

    "AWS::ElasticLoadBalancingV2::ListenerCertificate" => {
        pulumiType = getPulumiType "AWS::ElasticLoadBalancingV2::ListenerCertificate"
        importIdentityParts = ["ListenerArn"; "Certificates"]
        delimiter = "_"
        remapFunc = remapFromImportIdentityPartsListenerCertificate
    }

    "AWS::Lambda::Permission" => {
        pulumiType = "aws:lambda/permission:Permission"
        importIdentityParts = ["FunctionName"; "Id"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
    }

    "AWS::S3::BucketPolicy" => {
        pulumiType = "aws:s3/bucketPolicy:BucketPolicy"
        importIdentityParts = ["Bucket"]
        delimiter = "/"
        remapFunc = remapFromImportIdentityParts
    }
]