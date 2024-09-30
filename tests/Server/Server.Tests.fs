module Server.Tests

open System.Collections.Generic

open Expecto

open Shared
open Server

let syntax = testList "syntax" [ 
    test "A simple test" {
        let subject = "Hello World"
        Expect.equal subject "Hello World" "The strings should be equal"
    }
    test "using Maps" {
        let myMap = Map [("blah","boink"); ("foo","bar")]
        let myNestedMap = Map [("nested", myMap)]
        Expect.equal (myMap.TryGetValue "blah") (true, "boink") "That's not how maps work"
        Expect.equal (myMap.TryGetValue "forp") (false, null) "That's not how maps work"
        Expect.equal (myMap |> Map.tryFind "foo") (Some "bar") "That's not how maps work"
        Expect.equal myNestedMap.["nested"].["foo"] "bar" "That's not how maps work"
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
        Expect.equal (getRemappedImportProps resource resourceData) None ""
    }
    test "resourceType in remapSpec with missing resourceData returns None" {
        let resource = {
                logicalId = "foo"
                resourceId = "bar"
                resourceType = "AWS::ApiGateway::Deployment"
        }
        let resourceData = Dictionary<string, Dictionary<string, string>>()
        Expect.equal (getRemappedImportProps resource resourceData) None ""
    }
    test "resourceType in remapSpec with complete resourceData returns Some(resourceType, name, importId)" {
        let resource = {
            logicalId = "foo-foo"
            resourceId = "bar"
            resourceType = "AWS::ApiGateway::Deployment"
        }
        let resourceData = Dictionary<string, Dictionary<string,string>>()
        resourceData.Add("foo-foo", Dictionary<string,string>())
        resourceData["foo-foo"].Add("RestApiId", "fonce")
        resourceData["foo-foo"].Add("Id", "foo-foo")
        Expect.equal (getRemappedImportProps resource resourceData) (Some (
            "aws:apigateway/deployment:Deployment",
            "foo_foo",
            "fonce/foo-foo"
        )) ""
    }
    test "ECS Service remaps using remapFromIdAsArn" {
        let logicalId = "myService"
        let resource = {
            logicalId = logicalId
            resourceId = "arn:aws:ecs:us-west-2:051081605780:service/dev2-environment-ECSCluster-2JDFODYBOS1/dev2-dropbeacon-ECSServiceV2-zFfDnUyTGIgg"
            resourceType = "AWS::ECS::Service"
        }
        let resourceData = Dictionary<string, Dictionary<string,string>>()
        resourceData.Add(logicalId, Dictionary<string,string>())
        resourceData[logicalId].Add("Id", "arn:aws:ecs:us-west-2:051081605780:service/dev2-environment-ECSCluster-2JDFODYBOS1/dev2-dropbeacon-ECSServiceV2-zFfDnUyTGIgg")
        Expect.equal (getRemappedImportProps resource resourceData) (Some (
            "aws:ecs/service:Service",
            "myService",
            "dev2-environment-ECSCluster-2JDFODYBOS1/dev2-dropbeacon-ECSServiceV2-zFfDnUyTGIgg"
        )) ""

    }
]

let all =
    testList "All"
        [
            Shared.Tests.shared
            syntax
            getRemappedImportProps
        ]

[<EntryPoint>]
let main _ = runTestsWithCLIArgs [] [||] all