module AwsCloudFormationTemplates

open AwsCloudFormationTypes
let private (=>) key value = key, value

let remapSpecifications = Map.ofList [
    "AWS::ApiGateway::Resource" => {
        pulumiType = "aws:apigateway/resource:Resource"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
    }

    "AWS::ApiGateway::Stage" => {
        pulumiType = "aws:apigateway/stage:Stage"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
    }

    "AWS::ApiGateway::Deployment" => {
        pulumiType = "aws:apigateway/deployment:Deployment"
        importIdentityParts = ["RestApiId"; "Id"]
        delimiter = "/"
    }

    "AWS::ApiGateway::UsagePlanKey" => {
        pulumiType = "aws:apigateway/usagePlanKey:UsagePlanKey"
        importIdentityParts = ["UsagePlanId"; "KeyId"]
        delimiter = "/"
    }

    "AWS::EC2::SubnetRouteTableAssociation" => {
        pulumiType = "aws:ec2/routeTableAssociation:RouteTableAssociation"
        importIdentityParts = ["SubnetId"; "RouteTableId"]
        delimiter = "/"
    }

    "AWS::Lambda::Permission" => {
        pulumiType = "aws:lambda/permission:Permission"
        importIdentityParts = ["FunctionName"; "Id"]
        delimiter = "/"
    }
]