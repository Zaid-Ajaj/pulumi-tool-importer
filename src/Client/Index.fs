module Index

open Feliz
open Feliz.UseDeferred
open Feliz.Router
open Shared

[<ReactComponent>]
let PulumiTitleWithVersion() =
    let response = React.useDeferred(Server.api.getPulumiVersion(), [|  |])
    match response with
    | Deferred.Resolved version ->
        React.fragment [
            Html.div "Pulumi Importer"
            Html.div [
                prop.style [ style.fontSize 13; style.marginTop 10; style.marginLeft 10 ]
                prop.text $" using Pulumi {version} | Importer v0.25.0"
            ]
        ]

    | _ ->
         React.fragment [
            Html.div "Pulumi Importer"
            Html.div [
                prop.style [ style.fontSize 13; style.marginTop 10; style.marginLeft 10 ]
                prop.text " v0.25.0"
            ]
        ]

[<ReactComponent>]
let ProviderTile(name: string, link: string) = Html.div [
    prop.text name
    prop.onClick (fun _ -> Router.navigate(link))
    prop.style [
        style.margin 10
        style.fontSize 20
        style.height 60
        style.width 200
        style.cursor.pointer
        style.textAlign.center
        style.display.inlineBlock
        style.position.relative
        style.overflow.hidden
        style.paddingTop 10
        style.border(2, borderStyle.solid, color.black)
        style.boxShadow(0, 0, 2, 0, color.black)
    ]
]

[<ReactComponent>]
let AwsTile() = Html.div [
    prop.onClick (fun _ -> Router.navigate "aws-start")
    prop.children [
        Html.img [
            prop.src "https://www.pulumi.com/logos/pkg/aws.svg"
            prop.style [
                style.marginTop 15
                style.height 50
                style.width 100
            ]
        ]
    ]

    prop.style [
        style.margin 10
        style.fontSize 20
        style.height 100
        style.width 200
        style.cursor.pointer
        style.textAlign.center
        style.display.inlineBlock
        style.position.relative
        style.overflow.hidden
        style.paddingTop 10
        style.border(2, borderStyle.solid, color.black)
        style.boxShadow(0, 0, 2, 0, color.black)
    ]
]

[<ReactComponent>]
let AzureTile() = Html.div [
    prop.onClick (fun _ -> Router.navigate "azure-start")
    prop.children [
        Html.img [
            prop.src "https://www.pulumi.com/logos/pkg/azure-native.svg"
            prop.style [
                style.marginTop 15
                style.height 50
                style.width 100
            ]
        ]
    ]

    prop.style [
        style.margin 10
        style.fontSize 20
        style.height 100
        style.width 200
        style.cursor.pointer
        style.textAlign.center
        style.display.inlineBlock
        style.position.relative
        style.overflow.hidden
        style.paddingTop 10
        style.border(2, borderStyle.solid, color.black)
        style.boxShadow(0, 0, 2, 0, color.black)
    ]
]

[<ReactComponent>]
let GoogleTile() = Html.div [
    prop.onClick (fun _ -> Router.navigate "google-start")
    prop.children [
        Html.img [
            prop.src "https://www.pulumi.com/logos/pkg/gcp.svg"
            prop.style [
                style.height 80
                style.width 130
            ]
        ]
    ]

    prop.style [
        style.margin 10
        style.fontSize 20
        style.height 100
        style.width 200
        style.cursor.pointer
        style.textAlign.center
        style.display.inlineBlock
        style.position.relative
        style.overflow.hidden
        style.paddingTop 10
        style.border(2, borderStyle.solid, color.black)
        style.boxShadow(0, 0, 2, 0, color.black)
    ]
]


[<ReactComponent>]
let ImportPreviewTile() = Html.div [
    prop.onClick (fun _ -> Router.navigate "import-preview")
    prop.text "Import Preview"
    prop.style [
        style.margin 10
        style.fontSize 20
        style.height 60
        style.width 200
        style.cursor.pointer
        style.textAlign.center
        style.display.inlineBlock
        style.position.relative
        style.overflow.hidden
        style.paddingTop 10
        style.border(2, borderStyle.solid, color.black)
        style.boxShadow(0, 0, 2, 0, color.black)
    ]
]

[<ReactComponent>]
let View() =
    let (currentUrl, setCurrentUrl) = React.useState(Router.currentUrl())
    Html.div [
        prop.style [ style.margin 20 ]
        prop.children [

            Html.span [
                prop.style [ style.fontSize 24; style.display.flex; style.justifyContent.left; style.alignContent.center ]
                prop.children [
                    Html.img [
                        prop.src "https://www.pulumi.com/logos/brand/avatar-on-white.png"
                        prop.style [ style.height 50; style.marginRight 20 ]
                    ]

                    PulumiTitleWithVersion()
                ]
            ]

            Html.hr [ ]

            React.router [
                 router.onUrlChanged setCurrentUrl
                 router.children [
                    match currentUrl with
                    | [ "aws" ] ->
                        Aws.Page(fun region -> Aws.ResourceExplorer region)
                    | [ "aws-cloudformation-stacks" ] ->
                        Aws.Page(fun region -> Aws.CloudFormationStacks region)
                    | [ "aws-cloudformation-generated-templates" ] ->
                        Aws.Page(fun region -> Aws.CloudFormationGeneratedTemplates region)
                    | [ "aws-start" ] ->
                        Aws.StartPage()
                    | [ "azure-start" ] ->
                        Azure.StartPage()
                    | [ "azure" ] ->
                        Azure.ResourceGroups()
                    | [ "google-start" ] ->
                        Google.StartPage()
                    | [ "google-asset-inventory" ] ->
                        Google.AssetInventory()
                    | [ "import-preview" ] ->
                        MarkdownContent """
### Pulumi Import Preview
Use the import preview to experiment with [Pulumi Import](https://www.pulumi.com/docs/cli/commands/pulumi_import/).
Edit the JSON content and preview the import results in different languages alongside the imported stack state.
                        """
                        Import.Preview("{ \"resources\": [] }", "")
                    | _ ->
                        Html.p [
                            prop.text "Select a cloud provider to import resources from:"
                            prop.className "subtitle"
                        ]

                        AwsTile()
                        AzureTile()
                        GoogleTile()

                        Html.p [
                           prop.text "Experiment with Pulumi Import and preview results:"
                           prop.className "subtitle"
                           prop.style [ style.marginTop 10 ]
                        ]

                        ImportPreviewTile()
                 ]
            ]
        ]
    ]