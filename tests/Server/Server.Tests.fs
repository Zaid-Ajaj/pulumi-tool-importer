module Server.Tests

open System.Collections.Generic
open Newtonsoft.Json.Linq
open Expecto
open Amazon.CloudFormation.Model

open AwsCloudFormationTypes
open Shared
open Server


let getImportIdentityParts = testList "getImportIdentityParts" [
    test "resourceType not in remapSpec returns None" {
        Expect.equal (getImportIdentityParts "foo") None ""
    }
    test "resourceType in remapSpec returns Some(parts)" {
        Expect.equal (getImportIdentityParts "AWS::S3::BucketPolicy") (Some ["Bucket"]) ""
    }
]

let getRemappedImportProps = testList "getRemappedImportProps" [
    test "resourceType not in remapSpec returns None" {
        let resource = {
                logicalId = "foo"
                resourceId = "bar"
                resourceType = "boink"
        }
        let resourceData = Dictionary<string, Dictionary<string, string>>()
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual None ""
    }
    test "resourceType in remapSpec with missing resourceData returns None" {
        let resource = {
                logicalId = "foo"
                resourceId = "bar"
                resourceType = "AWS::ApiGateway::Deployment"
        }
        let resourceData = Dictionary<string, Dictionary<string, string>>()
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual None ""
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
        Expect.equal actual (Some(expectedResult)) ""
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
        Expect.equal actual (Some(expectedResult)) ""
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
        Expect.equal actual (Some(expectedResult)) ""

        // also remaps correctly if SetIdentifier is present in data
        resourceData[logicalId].Add("SetIdentifier", "setIdentifier")
        let expectedResultWithSetId : RemappedSpecResult = {
            resourceType = "aws:route53/record:Record"
            logicalId = "myDNSRecord"
            importId = "hostedZoneId_name_type_setIdentifier"
        }
        let actual = getRemappedImportProps resource resourceData AwsResourceContext.Empty
        Expect.equal actual (Some(expectedResultWithSetId)) ""
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
        Expect.equal actual (Some(expectedResult)) ""
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
        Expect.isTrue (resourceData.ContainsKey "ListenerCertificate") ""
        let certProps = resourceData["ListenerCertificate"] 
        Expect.isTrue (certProps.ContainsKey "ListenerArn") ""
        Expect.equal certProps["ListenerArn"] "listenerArn" ""
        Expect.isTrue (certProps.ContainsKey "Certificates") ""
        Expect.equal certProps["Certificates"] "imported-cert-arn-val" ""
        Expect.isTrue (certProps.ContainsKey "Id") ""
        Expect.equal certProps["Id"] "resourceId" ""
        Expect.isTrue (certProps.ContainsKey "resourceType") ""
        Expect.equal certProps["resourceType"] "AWS::ElasticLoadBalancingV2::ListenerCertificate" ""
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
        
        // let resourceData = Dictionary<string, Dictionary<string,string>>()
        // resourceData.Add("ListenerCertificate", Dictionary<string,string>())
        // resourceData["ListenerCertificate"].Add("ListenerArn", "listenerArn")
        // resourceData["ListenerCertificate"].Add("Certificates", "imported-cert-arn-val")
        // resourceData["ListenerCertificate"].Add("Id", "resourceId")
        // resourceData["ListenerCertificate"].Add("resourceType", "AWS::ElasticLoadBalancingV2::ListenerCertificate")

        let resources = [{
            resourceId = "resourceId"
            logicalId = "ListenerCertificate"
            resourceType = "AWS::ElasticLoadBalancingV2::ListenerCertificate"
        }]

        let context = AwsResourceContext.Empty

        let importJson, errors = getPulumiImportJson resources context resourceData
        Expect.isTrue (importJson.ContainsKey "resources") ""
        Expect.equal importJson["resources"].Type JTokenType.Array ""
        let resourcesArr = importJson["resources"].ToObject<JArray>()
        Expect.equal resourcesArr.Count 1 ""
        Expect.equal resourcesArr[0].Type JTokenType.Object ""
        let resourceImport = resourcesArr[0].ToObject<JObject>()
        Expect.isTrue (resourceImport.ContainsKey "type") ""
        Expect.equal ((resourceImport["type"]).ToString()) "aws:lb/listenerCertificate:ListenerCertificate" ""
        Expect.isTrue (resourceImport.ContainsKey "name") ""
        Expect.equal ((resourceImport["name"]).ToString()) "ListenerCertificate" ""
        Expect.isTrue (resourceImport.ContainsKey "id") ""
        Expect.equal ((resourceImport["id"]).ToString()) "listenerArn_imported-cert-arn-val" ""

        let testDict = Dictionary<string, string>(Map.ofList ["boink","blah"])
        printfn "%A" testDict
    }

]

let all =
    testList "All"
        [
            Shared.Tests.shared
            getRemappedImportProps
            getImportIdentityParts
            resolveTokenValue
            templateBodyData
            getPulumiImportJson
        ]

[<EntryPoint>]
let main _ = runTestsWithCLIArgs [] [||] all