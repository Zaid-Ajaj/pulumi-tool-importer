module AwsCloudFormationTypes

open Amazon.EC2.Model
open Amazon.ElasticLoadBalancingV2.Model
open Amazon.IdentityManagement.Model

open System.Collections.Generic
open Shared


type RemappedSpecResult = {
    resourceType: string
    logicalId: string
    importId: string
}

type AwsResourceContext = {
    loadBalancers: Map<string,LoadBalancer>
    elasticIps: Map<string,Address>
    routeTables: List<RouteTable>
    iamPolicies: List<ManagedPolicy>
    gatewayAttachmentImportIds: Dictionary<string,string>
    securityGroupRuleIds: Dictionary<string, seq<SecurityGroupRule>>
}
  with 
    static member Empty = {
        loadBalancers = Map.empty
        elasticIps = Map.empty
        routeTables = ResizeArray []
        iamPolicies = ResizeArray []
        gatewayAttachmentImportIds = Dictionary()
        securityGroupRuleIds = Dictionary()
    }

type CustomRemapSpecification = {
    pulumiType: string
    delimiter: string
    importIdentityParts: string list
    remapFunc: AwsCloudFormationResource -> Dictionary<string, Dictionary<string,string>> -> AwsResourceContext -> CustomRemapSpecification -> RemappedSpecResult
    validatorFunc: AwsCloudFormationResource -> Dictionary<string, Dictionary<string,string>> -> CustomRemapSpecification -> bool
}

type RemapSpecification = {
    pulumiType: string
    delimiter: string
    importIdentityParts: string list
}



