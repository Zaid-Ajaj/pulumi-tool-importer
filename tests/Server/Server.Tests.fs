module Server.Tests

open System.Collections.Generic
open System.IO
open System.Linq

open Newtonsoft.Json.Linq
open Expecto
open Amazon.CloudFormation.Model

open AwsCloudFormationTypes
open Shared
open Server

let dictIsEqual (dic1: Dictionary<string,string>) (dic2: Dictionary<string,string>) =
    let dic1Ordered = Dictionary<string,string>(dic1.OrderBy(fun entry -> entry.Key))
    let dic2Ordered = Dictionary<string,string>(dic2.OrderBy(fun entry -> entry.Key))
    let keysMatch = 
        dic1Ordered
        |> Seq.forall (fun entry -> 
            dic2Ordered.ContainsKey entry.Key
            && entry.Value = dic2Ordered[entry.Key])
    (dic1Ordered.Count = dic2Ordered.Count) && keysMatch

let resourceDataIsEqual 
    (dic1: Dictionary<string,Dictionary<string,string>>)
    (dic2: Dictionary<string,Dictionary<string,string>>) =
    let dic1Ordered = Dictionary<string,Dictionary<string,string>>(dic1.OrderBy(fun entry -> entry.Key))
    let dic2Ordered = Dictionary<string,Dictionary<string,string>>(dic2.OrderBy(fun entry -> entry.Key))
    let keysMatch = 
        dic1Ordered
        |> Seq.forall (fun entry -> 
            dic2Ordered.ContainsKey entry.Key
            && (dictIsEqual entry.Value dic2Ordered[entry.Key]))
    (dic1Ordered.Count = dic2Ordered.Count) && keysMatch

let getRemappedImportProps = testList "getRemappedImportProps" [
    test "resourceType not in remapSpec returns error" {
        let resource = {
            logicalId = "foo"
            resourceId = "bar"
            resourceType = "boink"
        }
        let resourceData = Dictionary<string, Dictionary<string, string>>()
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual (Error "Found no mapping specification for resource type boink") ""
    }

    test "resourceType in remapSpec with missing resourceData returns error" {
        let resource = {
            logicalId = "foo"
            resourceId = "bar"
            resourceType = "AWS::ApiGateway::Deployment"
        }
        let resourceData = Dictionary<string, Dictionary<string, string>>()
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual (Error "Resource 'foo' of type AWS::ApiGateway::Deployment has no resource data resolved") ""
    }

    test "ApiGateway Deployment remaps with remapFromImportIdentityParts)" {
        let resource = {
            logicalId = "foo-foo"
            resourceId = "bar"
            resourceType = "AWS::ApiGateway::Deployment"
        }
        let resourceData = Dictionary<string, Dictionary<string,string>>()
        resourceData.Add("foo-foo", Dictionary<string,string>())
        resourceData["foo-foo"].Add("RestApiId", "fonce")
        resourceData["foo-foo"].Add("Id", "foo-foo")
        let expectedResult : RemappedSpecResult = {
            resourceType = "aws:apigateway/deployment:Deployment"
            logicalId = "foo_foo"
            importId = "fonce/foo-foo"
        }
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual (Ok(expectedResult)) ""
    }
    test "ECS Service remaps with remapFromIdAsArn" {
        let logicalId = "myService"
        let resource = {
            logicalId = logicalId
            resourceId = "arn:aws:ecs:us-west-2:051081605780:service/dev2-environment-ECSCluster-2JDFODYBOS1/dev2-dropbeacon-ECSServiceV2-zFfDnUyTGIgg"
            resourceType = "AWS::ECS::Service"
        }
        let resourceData = Dictionary<string, Dictionary<string,string>>()
        resourceData.Add(logicalId, Dictionary<string,string>())
        resourceData[logicalId].Add("Id", "arn:aws:ecs:us-west-2:051081605780:service/dev2-environment-ECSCluster-2JDFODYBOS1/dev2-dropbeacon-ECSServiceV2-zFfDnUyTGIgg")
        let expectedResult : RemappedSpecResult = {
            resourceType = "aws:ecs/service:Service"
            logicalId = "myService"
            importId = "dev2-environment-ECSCluster-2JDFODYBOS1/dev2-dropbeacon-ECSServiceV2-zFfDnUyTGIgg"
        } 
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual (Ok(expectedResult)) ""
    }
    test "DNS record remaps with remapFromImportIdentityPartsDNSRecord" {
        let logicalId = "myDNSRecord"
        let resource = {
            logicalId = logicalId
            resourceId = "foo"
            resourceType = "AWS::Route53::RecordSet"
        }
        let resourceData = Dictionary<string, Dictionary<string,string>>()
        resourceData.Add(logicalId, Dictionary<string,string>())
        resourceData[logicalId].Add("Id", "foo")
        resourceData[logicalId].Add("HostedZoneId", "hostedZoneId")
        resourceData[logicalId].Add("Name", "name")
        resourceData[logicalId].Add("Type", "type")
        let expectedResult : RemappedSpecResult = {
            resourceType = "aws:route53/record:Record"
            logicalId = "myDNSRecord"
            importId = "hostedZoneId_name_type"
        } 
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual (Ok(expectedResult)) ""

        // also remaps correctly if SetIdentifier is present in data
        resourceData[logicalId].Add("SetIdentifier", "setIdentifier")
        let expectedResultWithSetId : RemappedSpecResult = {
            resourceType = "aws:route53/record:Record"
            logicalId = "myDNSRecord"
            importId = "hostedZoneId_name_type_setIdentifier"
        }
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual (Ok(expectedResultWithSetId)) ""
    }
    test "Listener Certificate remaps with remapFromImportIdentityPartsListenerCertificate" {
        let logicalId = "myListenerCertificate"
        let resource = {
            logicalId = logicalId
            resourceId = "foo"
            resourceType = "AWS::ElasticLoadBalancingV2::ListenerCertificate"
        }
        let resourceData = Dictionary<string, Dictionary<string,string>>()
        resourceData.Add(logicalId, Dictionary<string,string>())
        resourceData[logicalId].Add("Id", "foo")
        resourceData[logicalId].Add("Certificates", "certificateArn")
        resourceData[logicalId].Add("ListenerArn", "listenerArn")
        let expectedResult : RemappedSpecResult = {
            resourceType = "aws:lb/listenerCertificate:ListenerCertificate"
            logicalId = "myListenerCertificate"
            importId = "listenerArn_certificateArn"
        } 
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual (Ok(expectedResult)) ""
    }
]

let resolveTokenValue = testList "resolveTokenValue" [
    test "resolves JTokenType.String" {
        let data = Dictionary<string, Dictionary<string,string>>()
        let stackExports = Map.ofList []
        let token = JToken.FromObject("testString")
        Expect.equal (resolveTokenValue data stackExports token) "testString" ""
    }
    test "resolves Join of string and ref" {
        let data = Dictionary<string, Dictionary<string,string>>()
        data.Add("AWS::Region", Dictionary<string,string>())
        data["AWS::Region"].Add("Id","us-west-2")
        let stackExports = Map.ofList []
        let jsonString = """{
          "Fn::Join": [
            "/",
            [
              "dropbeacon.dev2.healthsparq.com.",
              {
                "Ref": "AWS::Region"
              }
            ]
          ]
        }"""
        let token = JObject.Parse(jsonString)
        Expect.equal (resolveTokenValue data stackExports token) "dropbeacon.dev2.healthsparq.com./us-west-2" ""
    }
    test "resolves Join of string, import, and getatt" {
        let data = Dictionary<string, Dictionary<string,string>>()
        data.Add("resourceLogicalId", Dictionary<string,string>())
        data["resourceLogicalId"].Add("Name","resourcePhysicalId")
        let stackExports = Map.ofList ["importKey","importValue"]
        let jsonString = """{
          "Fn::Join": [
            "/",
            [
              "service",
              {
                "Fn::ImportValue": "importKey"
              },
              {
                "Fn::GetAtt": [
                    "resourceLogicalId",
                    "Name"
                ]
              }
            ]
          ]
        }"""
        let token = JObject.Parse(jsonString)
        let resolved = resolveTokenValue data stackExports token
        Expect.equal resolved "service/importValue/resourcePhysicalId" ""
    }
]

let templateBodyData = testList "templateBodyData" [
    test "AWS::ElasticLoadBalancingV2::ListenerCertificate with Fn::ImportValue" {
        let template = """
        {
            "Resources": {
                "ListenerCertificate": {
                    "Properties": {
                    "ListenerArn": "listenerArn",
                    "Certificates": [
                        {
                            "CertificateArn": {
                                "Fn::ImportValue": "imported-cert-arn-name"
                            }
                        }
                    ]
                },
                "Type": "AWS::ElasticLoadBalancingV2::ListenerCertificate"
                }
            }
        }
        """
        let templateResponse = GetTemplateResponse()
        templateResponse.TemplateBody <- template

        let stackExports = Map.ofList ["imported-cert-arn-name", "imported-cert-arn-val"]
        let resources = [{
            resourceId = "resourceId"
            logicalId = "ListenerCertificate"
            resourceType = "AWS::ElasticLoadBalancingV2::ListenerCertificate"
        }]
        let region = "us-east-1"

        let resourceData, _ = templateBodyData templateResponse resources stackExports region

        let expectedData = Dictionary<string,Dictionary<string,string>>(
            Map.ofList [
                "ListenerCertificate", Dictionary<string,string>(Map.ofList [
                    "ListenerArn", "listenerArn";
                    "Certificates", "imported-cert-arn-val";
                    "Id", "resourceId";
                    "resourceType", "AWS::ElasticLoadBalancingV2::ListenerCertificate";
                ]);
                "AWS::Region", Dictionary<string,string>(Map.ofList [
                    "Id", "us-east-1"
                ])
            ]
        )

        Expect.isTrue (resourceDataIsEqual expectedData resourceData) "resourceData does not match expectedData"
    }
]

let getPulumiImportJson = testList "getPulumiImportJson" [
    test "AWS::ElasticLoadBalancingV2::ListenerCertificate with Fn::ImportValue" {
        let resourceData = Dictionary<string,Dictionary<string,string>>(
            Map.ofList [
                "ListenerCertificate", Dictionary<string,string>(Map.ofList [
                    "ListenerArn", "listenerArn";
                    "Certificates", "imported-cert-arn-val";
                    "Id", "resourceId";
                    "resourceType", "AWS::ElasticLoadBalancingV2::ListenerCertificate";
                ])
            ]
        )

        let resources = [{
            resourceId = "resourceId"
            logicalId = "ListenerCertificate"
            resourceType = "AWS::ElasticLoadBalancingV2::ListenerCertificate"
        }]

        let context = AwsResourceContext.Empty

        let importJson, errors = getPulumiImportJson resources context resourceData
        let expectedImportJson = JObject.Parse("""
        {
            "resources": [
                {
                    "id": "listenerArn_imported-cert-arn-val",
                    "type": "aws:lb/listenerCertificate:ListenerCertificate",
                    "name": "ListenerCertificate"
                }
            ]
        }
        """)
        Expect.isTrue (JToken.DeepEquals(importJson, expectedImportJson)) "importJson and expectedImportJson don't match"
    }

]

let all =
    testList "All"
        [
            Shared.Tests.shared
            getRemappedImportProps
            resolveTokenValue
            templateBodyData
            getPulumiImportJson
        ]

[<EntryPoint>]
let main _ = runTestsWithCLIArgs [] [||] all