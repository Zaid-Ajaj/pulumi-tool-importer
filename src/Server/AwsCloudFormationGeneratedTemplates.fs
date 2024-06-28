module AwsCloudFormationGeneratedTemplates

let private (=>) key value = key, value

type RemapSpecification = {
    pulumiType: string
    delimiter: string
    importIdentityParts: string list
}

let remapSpecifications = Map.ofList [
    "AWS::EC2::SubnetRouteTableAssociation" => {
        pulumiType = "aws:ec2/routeTableAssociation:RouteTableAssociation"
        importIdentityParts = ["Id"; "SubnetId"; "RouteTableId"]
        delimiter = "/"
    }

    "AWS::EC2::VPCDHCPOptionsAssociation" => {
        pulumiType = "aws:ec2/vpcDhcpOptionsAssociation:VpcDhcpOptionsAssociation"
        importIdentityParts = ["VpcId"]
        delimiter = "/"
    }

    "AWS::EC2::DHCPOptions" => {
        pulumiType = "aws:ec2/vpcDhcpOptions:VpcDhcpOptions"
        importIdentityParts = ["DhcpOptionsId"]
        delimiter = "/"
    }

    "AWS::EC2::DHCPOptions" => {
        pulumiType = "aws:ec2/vpcDhcpOptions:VpcDhcpOptions"
        importIdentityParts = ["DhcpOptionsId"]
        delimiter = "/"
    }

    "AWS::EC2::SubnetNetworkAclAssociation" => {
         pulumiType = "aws:ec2/networkAclAssociation:NetworkAclAssociation"
         importIdentityParts = ["AssociationId"]
         delimiter = "/"
    }

    "AWS::IAM::ManagedPolicy" => {
         pulumiType = "aws:iam/policy:Policy"
         importIdentityParts = ["PolicyArn"]
         delimiter = "/"
    }

    "AWS::Events::EventBus" => {
         pulumiType = "aws:cloudwatch/eventBus:EventBus"
         importIdentityParts = ["Name"]
         delimiter = "/"
    }

    "AWS::EC2::EIPAssociation" => {
         pulumiType = "aws:ec2/eipAssociation:EipAssociation"
         importIdentityParts = ["Id"]
         delimiter = "/"
    }

    "AWS::EC2::VPNConnectionRoute" => {
        pulumiType = "aws:ec2/vpnConnectionRoute:VpnConnectionRoute"
        importIdentityParts = ["DestinationCidrBlock"; "VpnConnectionId"]
        delimiter = ":"
    }

    "AWS::CloudWatch::Alarm" => {
        pulumiType = "aws:cloudwatch/metricAlarm:MetricAlarm"
        importIdentityParts = ["AlarmName"]
        delimiter = "/"
    }

    "AWS::Events::Rule" => {
        pulumiType = "aws:cloudwatch/eventRule:EventRule"
        importIdentityParts = ["Arn"]
        delimiter = "/"
    }

    "AWS::Backup::BackupSelection" => {
        pulumiType = "aws:backup/selection:Selection"
        importIdentityParts = ["Id"]
        delimiter = "/"
    }

    "AWS::EC2::VPNGateway" => {
        pulumiType = "aws:ec2/vpnGateway:VpnGateway"
        importIdentityParts = ["VPNGatewayId"]
        delimiter = "/"
    }

    "AWS::EC2::VPNConnection" => {
        pulumiType = "aws:ec2/vpnGateway:VpnGateway"
        importIdentityParts = ["VpnConnectionId"]
        delimiter = "/"
    }

    "AWS::Logs::LogGroup" => {
        pulumiType = "aws:cloudwatch/logGroup:LogGroup"
        importIdentityParts = ["LogGroupName"]
        delimiter = "/"
    }

    "AWS::EC2::Route" => {
        pulumiType = "aws:ec2/route:Route"
        importIdentityParts = ["RouteTableId"; "CidrBlock"]
        delimiter = "_"
    }

    "AWS::IAM::SAMLProvider" => {
        pulumiType = "aws:iam/samlProvider:SamlProvider"
        importIdentityParts = ["Arn"]
        delimiter = ":"
    }

    "AWS::ECS::Service" => {
        pulumiType = "aws:ecs/service:Service"
        importIdentityParts = ["Cluster"; "ServiceArn"] //TODO: verify this
        delimiter = ":"
    }

    "AWS::ECS::TaskDefinition" => {
        pulumiType = "aws:lb/targetGroup:TargetGroup"
        importIdentityParts = ["TargetGroupArn"]
        delimiter = "/"
    }

    "AWS::EC2::VolumeAttachment" => {
        pulumiType = "aws:ec2/volumeAttachment:VolumeAttachment"
        importIdentityParts = ["Device"; "VolumeId"; "InstanceId"]
        delimiter = "_"
    }
]