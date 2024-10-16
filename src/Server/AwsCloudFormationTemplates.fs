module AwsCloudFormationTemplates

open System
open Newtonsoft.Json.Linq

open AwsCloudFormationTypes
open System.Collections.Generic
open Shared

let private (=>) key value = key, value

let tryFindSecurityGroupRule
    (data: Dictionary<string,string>)
    (rules: Map<string,JObject>) =
    rules
    |> Map.filter (fun id props -> 
        props.Property("GroupId").Value.ToString() = data["GroupId"])
    |> Map.tryFindKey (fun id props ->
        data
        |> Seq.forall (fun entry ->
            if entry.Key = "Id" || entry.Key = "resourceType" then 
                true
            elif props.ContainsKey entry.Key then
                let prop = props.Property(entry.Key)
                prop.Value.ToString() = data[prop.Name]
            else 
                false
        ))

let requireParts (keys: string list) (data: Dictionary<string, string>) =
    if keys |> List.forall (fun key -> data.ContainsKey key) then 
        Ok data
    else 
        let missingKeys = 
            keys 
            |> List.filter (fun key -> not (data.ContainsKey key))
            |> String.concat ", "
        Error $"Missing required parts [{missingKeys}]"

let defaultImportIdentity (spec: ImportIdentityParts) : ImportIdentityBuilder = 
    fun resource resourceData context -> result {
        let! data = requireParts spec.importIdentityParts resourceData
        let parts = spec.importIdentityParts |> List.map (fun key -> data[key])
        return String.concat spec.delimiter parts
    }

let requirePart (key: string) (data: Dictionary<string, string>) =
    if data.ContainsKey key 
    then Ok data[key]
    else Error $"Missing required part '{key}'"

let optionalPart (key: string) (data: Dictionary<string, string>) =
    if data.ContainsKey key 
    then Some data[key]
    else None

let importIdentityBuilders : Map<string, ImportIdentityBuilder> = Map.ofList [
    
    "AWS::ApiGateway::Resource" => defaultImportIdentity {
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
    }

    "AWS::ApiGateway::Deployment" => defaultImportIdentity {
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
    }

    "AWS::ApiGateway::UsagePlanKey" => defaultImportIdentity {
        importIdentityParts = ["UsagePlanId"; "KeyId"]
        delimiter = "/"
    }
    
    "AWS::ApplicationAutoScaling::ScalingPolicy" => fun resource data context ->
        result {
            let! scalingTargetId = requirePart "ScalingTargetId" data
            let! policyName = requirePart "PolicyName" data
            let! importId = 
                match scalingTargetId.Split("|") with
                | scalingTargetParts when scalingTargetParts.Length = 3 -> 
                    scalingTargetParts
                    |> Array.rev
                    |> Array.append [| policyName |]
                    |> String.concat "/" 
                    |> Ok
                | _ -> 
                    Error $"Invalid ScalingTargetId: {scalingTargetId}"

            return importId
        }

    "AWS::ApplicationAutoScaling::ScalableTarget" => fun resource data context ->
        result {
            let! physicalId = requirePart "Id" data
            let! importId = 
                match physicalId.Split("|") with
                | parts when parts.Length = 3 -> 
                    parts
                    |> Array.rev
                    |> String.concat "/"
                    |> Ok
                | _ -> 
                    Error $"Invalid Id: {physicalId}"

            return importId
        }

    "AWS::Cognito::UserPoolClient" => defaultImportIdentity {
        importIdentityParts = ["UserPoolId"; "Id"]
        delimiter = "/"
    }

    "AWS::EC2::SubnetRouteTableAssociation" => defaultImportIdentity {
        importIdentityParts = ["SubnetId"; "RouteTableId"]
        delimiter = "/"
    }

    "AWS::EC2::SecurityGroupIngress" => fun resource data context ->
        result {
            let! _ = requirePart "IpProtocol" data
            let! _ = requirePart "GroupId" data
            let! importId = 
                match tryFindSecurityGroupRule data context.securityGroupIngressRules with
                | Some importId -> Ok importId
                | None -> Error $"No matching security group rule found for {resource.resourceType} named {resource.logicalId}"
            return importId
        }

    "AWS::EC2::SecurityGroupEgress" => fun resource data context ->
        result {
            let! _ = requirePart "IpProtocol" data
            let! _ = requirePart "GroupId" data
            let! importId = 
                match tryFindSecurityGroupRule data context.securityGroupEgressRules with
                | Some importId -> Ok importId
                | None -> Error $"No matching security group rule found for {resource.resourceType} named {resource.logicalId}"
            return importId
        }

    "AWS::ECS::Service" => fun resource data context ->
        result {
            let! physicalId = requirePart "Id" data
            let! importId = 
                match physicalId.Split("/") with
                | parts when parts.Length > 1 -> 
                    parts
                    |> Array.skip 1
                    |> String.concat "/"
                    |> Ok
                | _ -> 
                    Error $"Invalid Id: {physicalId}"

            return importId
        }

    "AWS::ElasticLoadBalancingV2::ListenerCertificate" => defaultImportIdentity {
        importIdentityParts = ["ListenerArn"; "Certificates"]
        delimiter = "_"
    }

    "AWS::Lambda::Permission" => defaultImportIdentity {
        importIdentityParts = ["FunctionName"; "Id"]
        delimiter = "/"
    }

    "AWS::Lambda::LayerVersionPermission" => fun resource data context ->
        result {
            let! layerVersionArn = requirePart "LayerVersionArn" data
            let! importId = 
                // rewrite "part1:part2:partN:version" 
                // into "part1:part2:partN,version"
                match layerVersionArn.Split(":") with
                | layerVersionArnParts when layerVersionArnParts.Length > 1 -> 
                    let layerVersion = layerVersionArnParts[layerVersionArnParts.Length - 1]
                    let layerArn = 
                        layerVersionArnParts
                        |> Array.except [| layerVersion |]
                        |> String.concat ":"
        
                    Ok $"{layerArn},{layerVersion}"
                | _ -> 
                    Error $"Invalid LayerVersionArn: {layerVersionArn}"

            return importId
        }

    "AWS::Lambda::EventInvokeConfig" => defaultImportIdentity {
        importIdentityParts = ["FunctionName"; "Qualifier"]
        delimiter = ":"
    }

    "AWS::Route53::RecordSet" => fun resource data context ->
        result {
            let! hostedZoneId = requirePart "HostedZoneId" data
            let! name = requirePart "Name" data
            let! recordType = requirePart "Type" data
            let importId = 
                match optionalPart "SetIdentifier" data with
                | Some setId -> $"{hostedZoneId}_{name}_{recordType}_{setId}"
                | None -> $"{hostedZoneId}_{name}_{recordType}"

            return importId
        }

    "AWS::S3::BucketPolicy" => defaultImportIdentity {
        importIdentityParts = ["Bucket"]
        delimiter = "/"
    }

    "AWS::SQS::QueuePolicy" => defaultImportIdentity {
        importIdentityParts = ["Queues"]
        delimiter = ""
    }

    "AWS::Transfer::Server" => fun resource data context ->
        result {
            let! serverPhysicalId = requirePart "Id" data
            let! importId = 
                match serverPhysicalId.Split("/") with
                | parts when parts.Length > 1 -> Ok parts[1]
                | _ -> Error $"Invalid Id: {serverPhysicalId}"

            return importId
        }

    "AWS::Transfer::User" => defaultImportIdentity {
        importIdentityParts = ["ServerId"; "UserName"]
        delimiter = "/"
    }
]