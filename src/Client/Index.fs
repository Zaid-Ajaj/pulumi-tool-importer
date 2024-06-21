module Index

open Feliz
open Feliz.UseDeferred
open Feliz.Router
open Feliz.SelectSearch
open Feliz.Markdown

open Shared
open System
open Fable.Remoting.Client
open Fable.Core

let serverApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<ImporterApi>

[<ReactComponent>]
let Row(cells: ReactElement list) =
    Html.tr [
        for (i, cell) in List.indexed cells do
        Html.td [
            prop.key i
            prop.children cell
        ]
    ]

[<ReactComponent>]
let Table (rows: ReactElement list) =
    Html.table [
        prop.className "table"
        prop.children [
            Html.tbody [
                prop.children rows
            ]
        ]
    ]

[<ReactComponent>]
let Subtitle(text: string) =
    Html.p [
        prop.className "subtitle"
        prop.text text
    ]

[<ReactComponent>]
let Div(className: string, children: ReactElement list) =
    Html.div [
        prop.className className
        prop.children children
    ]

[<ReactComponent(import="default", from="react-highlight")>]
let Highlight(className: string, children: ReactElement array) : ReactElement = jsNative

[<ReactComponent>]
let MarkdownContent(sourceMarkdown: string) =
    Div("content", [
        Markdown.markdown [
            markdown.children sourceMarkdown
            markdown.components [
                markdown.components.pre (fun props -> React.fragment props.children)
                markdown.components.code (fun props ->
                    if props.isInline
                    then Html.code props.children
                    else Highlight(props.className, props.children)
                )
            ]
        ]
    ])

[<ReactComponent>]
let Tab(title: string, value: string, selectedTab, onClick: string -> unit) =
    Html.li [
        prop.className (if selectedTab = value then "is-active" else "")
        prop.children [
            Html.a [
                prop.onClick (fun _ -> onClick value)
                prop.text title
            ]
        ]
    ]

let Tabs(children: ReactElement list) =
    Div("tabs", [
        Html.ul children
    ])

[<ReactComponent>]
let PulumiTitleWithVersion() =
    let response = React.useDeferred(serverApi.getPulumiVersion(), [|  |])
    match response with
    | Deferred.Resolved version ->
        React.fragment [
            Html.div "Pulumi Importer"
            Html.div [
                prop.style [ style.fontSize 13; style.marginTop 10; style.marginLeft 10 ]
                prop.text $" using Pulumi {version}"
            ]
        ]

    | _ ->
         Html.div "Pulumi Importer"

[<ReactComponent>]
let ImportPreview(importJson: string) =
    let pulumiImportJson, setPulumiImportJson = React.useState importJson
    let tab, setTab = React.useState "code"
    let language, setLanguage = React.useState "typescript"
    let preview, setPreview = React.useState(Deferred.HasNotStartedYet)
    let importPreview = React.useDeferredCallback(serverApi.importPreview, setPreview)
    React.fragment [
        Html.div [
            prop.style [
                style.width 200
                style.display.inlineBlock
                style.position.relative
            ]
            prop.children [
                SelectSearch.selectSearch [
                    selectSearch.options [
                        { value = "typescript"; name = "TypeScript"; disabled = false }
                        { value = "python"; name = "Python"; disabled = false }
                        { value = "go"; name = "Go"; disabled = false }
                        { value = "csharp"; name = "C#"; disabled = false }
                        { value = "yaml"; name = "YAML"; disabled = false }
                        { value = "java"; name = "Java"; disabled = false }
                    ]

                    selectSearch.value language
                    selectSearch.search true
                    selectSearch.placeholder "Select a language to preview the import"
                    selectSearch.onChange setLanguage
                ]
            ]
        ]

        Html.div [
            prop.style [
                style.width 200
                style.display.inlineBlock
                style.position.relative
                style.marginLeft 20
            ]
            prop.children [
                Html.button [
                    prop.text "Start import preview"
                    prop.className "button is-primary"
                    prop.style [ style.height 36; style.top -5 ]
                    prop.disabled (Deferred.inProgress preview)
                    prop.onClick (fun _ -> importPreview {
                        language = language
                        pulumiImportJson = pulumiImportJson
                    })
                ]
            ]
        ]

        Html.br [ ]

        Html.details [
            prop.style [
                style.marginTop 10
                style.marginBottom 10
            ]

            prop.children [
                Html.summary "Edit Import JSON:"
                Html.div [
                    prop.className "control"
                    prop.children [
                         Html.textarea [
                            prop.style [ style.height 350 ]
                            prop.className "input"
                            prop.value pulumiImportJson
                            prop.onChange setPulumiImportJson
                        ]
                    ]
                ]
            ]
        ]

        match preview with
        | Deferred.HasNotStartedYet ->
            Html.none
        | Deferred.InProgress ->
            Html.p $"Generating import preview in {language}"
            Html.progress [
                prop.className "progress is-small is-primary"
                prop.max 100
            ]

        | Deferred.Failed error ->
            Html.p [
                prop.style [ style.color.red ]
                prop.text error.Message
            ]

        | Deferred.Resolved (Error errorMessage) ->
            let parts = errorMessage.Split "\n"
            React.fragment [
                for part in parts do
                Html.p [
                    prop.style [ style.color.red ]
                    prop.text part
                ]
            ]

        | Deferred.Resolved (Ok response) ->
            Tabs [
                Tab("Code", "code", tab, setTab)
                Tab("Stack state", "stack-state", tab, setTab)
                if not (List.isEmpty response.warnings) then
                    Tab($"Warnings ({List.length response.warnings})", "warnings", tab, setTab)
            ]

            match tab with
            | "code" ->
                Highlight(language, [| Html.code response.generatedCode |])
            | "stack-state" ->
                Html.pre response.stackState
            | "warnings" ->
                Html.ul [
                    for warning in response.warnings do
                    Html.li warning
                ]
            | _ ->
                Html.none
    ]

[<ReactComponent>]
let ImportJsonDocs() = MarkdownContent """
The following JSON content can be used by pulumi to batch import the resources into a pulumi project.

Put the contents inside a file called `import.json` and run `pulumi import -f import.json`

See [pulumi import](https://www.pulumi.com/docs/cli/commands/pulumi_import/) to learn more about importing resources.
"""

[<ReactComponent>]
let AwsResourceExplorer() =
    let searchInputRef = React.useInputRef()
    let tagsInputRef = React.useInputRef()
    let currentTab, setCurrentTab = React.useState "resources"
    let searchResults, setSearchResults = React.useState(Deferred.HasNotStartedYet)
    let search = React.useDeferredCallback(serverApi.searchAws, setSearchResults)

    React.fragment [
        MarkdownContent """
Search resources to import using the [query syntax](https://docs.aws.amazon.com/resource-explorer/latest/userguide/using-search-query-syntax.html)
for AWS resource explorer
"""

        Html.div [
            prop.classes [ "field"; "is-grouped" ]
            prop.children [
                Html.p [
                    prop.classes [ "control"; "is-expanded" ]
                    prop.children [
                        Html.input [
                            prop.className "input"
                            prop.type' "text"
                            prop.placeholder "Query resources"
                            prop.ref searchInputRef
                            prop.onKeyUp(key.enter, fun ev ->
                                searchInputRef.current
                                |> Option.iter (fun searchInput ->
                                    tagsInputRef.current
                                    |> Option.iter (fun tagsInput ->
                                        search {
                                            queryString = searchInput.value
                                            tags = tagsInput.value
                                        })
                                )
                            )
                        ]
                    ]
                ]

                Html.p [
                    prop.className "control"
                    prop.children [
                        Html.button [
                            prop.classes [ "button"; "is-primary" ]
                            prop.text "Search"
                            prop.onClick (fun _ ->
                                searchInputRef.current
                                |> Option.iter (fun searchInput ->
                                    tagsInputRef.current
                                    |> Option.iter (fun tagsInput ->
                                        search {
                                            queryString = searchInput.value
                                            tags = tagsInput.value
                                        })
                                )
                            )
                        ]
                    ]
                ]
            ]
        ]

        Html.details [
            prop.style [ style.marginBottom 20 ]
            prop.children [
                Html.summary "Filter by tags"
                Html.div [
                    prop.className "control"
                    prop.style [ style.marginBottom 20 ]
                    prop.children [
                        Html.input [
                            prop.className "input"
                            prop.type' "text"
                            prop.placeholder "For example key1=value1;key2=value2"
                            prop.ref tagsInputRef
                            prop.onKeyUp(key.enter, fun ev ->
                                searchInputRef.current
                                |> Option.iter (fun searchInput ->
                                    tagsInputRef.current
                                    |> Option.iter (fun tagsInput ->
                                        search {
                                            queryString = searchInput.value
                                            tags = tagsInput.value
                                        })
                                )
                            )
                        ]
                    ]
                ]
            ]
        ]

        match searchResults with
        | Deferred.HasNotStartedYet -> Html.none
        | Deferred.InProgress ->
            Html.progress [
                prop.className "progress is-small is-primary"
                prop.max 100
            ]

        | Deferred.Failed error ->
            Html.p [
                prop.style [ style.color.red ]
                prop.text error.Message
            ]

        | Deferred.Resolved (Error errorMessage) ->
            Html.p [
                prop.style [ style.color.red ]
                prop.text errorMessage
            ]

        | Deferred.Resolved (Ok response) ->

            Html.div [
                prop.className "tabs is-toggle"
                prop.children [
                    Html.ul [
                        Html.li [
                            prop.children [
                                Html.a [ Html.span $"AWS resources ({List.length response.resources})" ]
                            ]
                            prop.onClick (fun _ -> setCurrentTab "resources")
                            if currentTab = "resources" then
                                prop.className "is-active"
                        ]

                        Html.li [
                            prop.children [
                                Html.a [ Html.span "Pulumi Import JSON" ]
                            ]
                            prop.onClick (fun _ -> setCurrentTab "import-json")
                            if currentTab = "import-json" then
                                prop.className "is-active"
                        ]

                        if not (List.isEmpty response.warnings) then
                            Html.li [
                                prop.children [
                                    Html.a [ Html.span $"Warnings ({List.length response.warnings})" ]
                                ]
                                prop.onClick (fun _ -> setCurrentTab "warnings")
                                if currentTab = "warnings" then
                                    prop.className "is-active"
                            ]

                        Html.li [
                            prop.children [
                                Html.a [ Html.span "Import Preview" ]
                            ]
                            prop.onClick (fun _ -> setCurrentTab "preview")
                            if currentTab = "preview" then
                                prop.className "is-active"
                        ]
                    ]
                ]
            ]

            match currentTab with
            | "import-json" ->
                ImportJsonDocs()
                Html.pre [
                    prop.style [ style.maxHeight 400; style.overflow.auto ]
                    prop.children [
                        Html.code response.pulumiImportJson
                    ]
                ]

            | "warnings" ->
                Html.ul [ for warning in response.warnings -> Html.li warning ]

            | "resources" ->
                Html.table [
                    prop.className "table is-fullwidth"
                    prop.style [
                        style.maxHeight 400
                        style.display.inlineBlock
                        style.overflow.auto
                    ]

                    prop.children [
                        Html.thead [
                            Html.tr [
                                Html.th "Resource ID"
                                Html.th "Type"
                                Html.th "Region"
                                Html.th "Service"
                                Html.th "Tags"
                                Html.th "Owning account ID"
                            ]
                        ]

                        Html.tbody [
                            for resource in response.resources do
                            Html.tr [
                                Html.td [
                                    prop.text resource.resourceId
                                    prop.style [
                                        style.maxWidth 400
                                        style.overflow.hidden
                                    ]
                                ]

                                Html.td resource.resourceType
                                Html.td resource.region
                                Html.td resource.service
                                Html.td [
                                    prop.style [
                                        style.maxWidth 400
                                        style.overflow.hidden
                                    ]

                                    prop.children [
                                        Html.ul [
                                            prop.style [ style.listStyleType.none ]
                                            prop.children [
                                                for (key, value) in Map.toList resource.tags do
                                                Html.li [
                                                    Html.span [
                                                        prop.style [ style.fontWeight.bolder ]
                                                        prop.text key
                                                    ]

                                                    Html.i [
                                                        prop.style [ style.marginLeft 5; style.marginRight 5  ]
                                                        prop.className "fas fa-arrow-right"
                                                    ]

                                                    Html.span [
                                                        prop.style [ style.fontStyle.italic ]
                                                        prop.text value
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                                Html.td resource.owningAccountId
                            ]
                        ]
                    ]
                ]
            | "preview" ->
                ImportPreview(response.pulumiImportJson)
            | _ ->
                Html.none
    ]


[<ReactComponent>]
let AwsImporter() =
    let callerIdentity = React.useDeferred(serverApi.awsCallerIdentity(), [|  |])
    React.fragment [
        match callerIdentity with
        | Deferred.HasNotStartedYet -> Html.none
        | Deferred.InProgress ->
            Html.progress [
                prop.className "progress is-small is-primary"
                prop.max 100
            ]

        | Deferred.Failed error ->
            Html.p [
                prop.style [ style.color.red ]
                prop.text error.Message
            ]

        | Deferred.Resolved (Error errorMessage) ->
            React.fragment [
                Html.p "Error occured while fetching caller identity for the current user:"
                Html.p [
                    prop.style [ style.color.red ]
                    prop.text errorMessage
                ]

                if errorMessage.Contains "The environment variables AWS_ACCESS_KEY_ID" then
                    Html.p "If you have already logged in via the AWS CLI, you can export your credentials as environment variables using the following command:"
                    Html.pre "eval \"$(aws configure export-credentials --format env)\""
                    Html.p "Restart the importer after running the command."

                if errorMessage.Contains "The security token included in the request is expired" then
                    Html.p "Your credentials seem to be expired. Please refresh your credentials and restart the importer."
                    Html.p "You can reset the credentials by running the following command:"
                    Html.pre [
                        Html.text "unset AWS_ACCESS_KEY_ID\n"
                        Html.text "unset AWS_SECRET_ACCESS_KEY\n"
                        Html.text "unset AWS_SESSION_TOKEN"
                    ]
                    Html.p "Then, login again using the AWS CLI and export the credentials as follows"
                    Html.pre "eval \"$(aws configure export-credentials --format env)\""
                    Html.p "Restart the importer after running the command."
            ]

        | Deferred.Resolved (Ok callerIdentity) ->
            Html.div [
                Html.i [
                    prop.className "fas fa-user"
                    prop.style [ style.marginRight 10 ]
                ]

                Html.span [
                    prop.style [ style.fontSize 18 ]
                    prop.text $"Logged in as {callerIdentity.userId}"
                ]

                Html.br [ ]
                Html.br [ ]

                AwsResourceExplorer()
            ]
    ]

[<ReactComponent>]
let AwsStartPage() =
    Html.div [
        MarkdownContent """
### Import from Amazon Web Services

The importer tool uses AWS credentials from environment variables to authenticate with AWS.

The easiest way to expose the credentials is to use the [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-quickstart.html)
to [login](https://docs.aws.amazon.com/signin/latest/userguide/command-line-sign-in.html).
Afterwards you can use the following command to export the credentials as environment variables:
```
eval "$(aws configure export-credentials --format env)"
```

Then you can start the importer tool to import resources from your AWS account.

If you are already logged in and have the credentials available, click 'Continue' to start importing resources.
"""
        Html.br [  ]
        Html.button [
            prop.className "button is-primary"
            prop.text "Continue"
            prop.onClick (fun _ -> Router.navigate("aws"))
        ]
    ]

[<ReactComponent>]
let AzureResourcesWithinResourceGroup(resourceGroup: string) =
    let currentTab, setCurrentTab = React.useState "resources"
    let response = React.useDeferred(serverApi.getResourcesUnderResourceGroup resourceGroup, [| resourceGroup |])
    match response with
    | Deferred.HasNotStartedYet -> Html.none
    | Deferred.InProgress ->
        Html.progress [
            prop.className "progress is-small is-primary"
            prop.max 100
        ]

    | Deferred.Failed error ->
        Html.p [
            prop.style [ style.color.red ]
            prop.text error.Message
        ]

    | Deferred.Resolved (Error errorMessage) ->
        Html.p [
            prop.style [ style.color.red ]
            prop.text errorMessage
        ]

    | Deferred.Resolved (Ok response) ->
        React.fragment [
            // tabs
            Html.div [
                prop.className "tabs is-toggle"
                prop.children [
                    Html.ul [
                        Html.li [
                            prop.children [
                                Html.a [ Html.span $"Azure resources ({List.length response.azureResources})" ]
                            ]
                            prop.onClick (fun _ -> setCurrentTab "resources")
                            if currentTab = "resources" then
                                prop.className "is-active"
                        ]

                        Html.li [
                            prop.children [
                                Html.a [ Html.span "Pulumi Import JSON" ]
                            ]
                            prop.onClick (fun _ -> setCurrentTab "import-json")
                            if currentTab = "import-json" then
                                prop.className "is-active"
                        ]

                        Html.li [
                            prop.children [
                                Html.a [ Html.span "Preview" ]
                            ]
                            prop.onClick (fun _ -> setCurrentTab "preview")
                            if currentTab = "preview" then
                                prop.className "is-active"
                        ]
                    ]
                ]
            ]

            // content
            match currentTab with
            | "import-json" ->
                ImportJsonDocs()
                Html.pre [
                    prop.style [ style.maxHeight 400; style.overflow.auto ]
                    prop.children [
                        Html.code [
                            prop.style [ style.color.black ]
                            prop.text response.pulumiImportJson
                        ]
                    ]
                ]

            | "resources" ->
                Html.table [
                    prop.className "table is-fullwidth"
                    prop.style [
                        style.maxHeight 400
                        style.display.inlineBlock
                        style.overflow.auto
                    ]

                    prop.children [
                        Html.thead [
                            Html.tr [
                                Html.th "Resource Type"
                                Html.th "ID"
                                Html.th "Name"
                            ]
                        ]

                        Html.tbody [
                            for resource in response.azureResources do
                            Html.tr [
                                Html.td [
                                    prop.text resource.resourceType
                                    prop.style [
                                        style.maxWidth 400
                                        style.overflow.hidden
                                    ]
                                ]

                                Html.td [
                                    prop.text resource.resourceId
                                    prop.style [
                                        style.maxWidth 500
                                        style.overflow.hidden
                                    ]
                                ]

                                Html.td resource.name
                            ]
                        ]
                    ]
                ]

            | "preview" ->
                ImportPreview(response.pulumiImportJson)
            | _ ->
                Html.none
        ]

[<ReactComponent>]
let AzureAccount() =
    let account = React.useDeferred(serverApi.azureAccount(), [|  |])
    match account with
    | Deferred.HasNotStartedYet -> Html.none
    | Deferred.InProgress ->
        Html.progress [
            prop.className "progress is-small is-primary"
            prop.max 100
        ]

    | Deferred.Failed error ->
        Html.p [
            prop.style [ style.color.red ]
            prop.text error.Message
        ]

    | Deferred.Resolved (Error errorMessage) ->
        Html.p [
            prop.style [ style.color.red ]
            prop.text errorMessage
        ]

    | Deferred.Resolved (Ok account) ->
        Html.div [
            prop.style [ style.marginBottom 10 ]
            prop.children [
                Html.i [
                    prop.className "fas fa-user"
                    prop.style [ style.marginRight 10 ]
                ]

                Html.span [
                    prop.style [ style.fontSize 18 ]
                    prop.text $"Logged in as {account.userName} (Subscription: {account.subscriptionName})"
                ]
            ]
        ]

[<ReactComponent>]
let AzureImporter() =
    let selectedResourceGroup, setResourceGroup = React.useState<string option>(None)
    let resourceGroups = React.useDeferred(serverApi.getResourceGroups(), [|  |])
    match resourceGroups with
    | Deferred.HasNotStartedYet -> Html.none
    | Deferred.InProgress ->
        Html.progress [
            prop.className "progress is-small is-primary"
            prop.max 100
        ]

    | Deferred.Failed error ->
        Html.p [
            prop.style [ style.color.red ]
            prop.text error.Message
        ]

    | Deferred.Resolved (Error errorMessage) ->
        Html.p [
            prop.style [ style.color.red ]
            prop.text errorMessage
        ]

    | Deferred.Resolved (Ok resourceGroups) ->
        React.fragment [
            AzureAccount()
            SelectSearch.selectSearch [
                selectSearch.options [
                    for rg in resourceGroups -> {
                        value = rg
                        name = rg
                        disabled = false
                    }
                ]

                selectSearch.value (defaultArg selectedResourceGroup "")
                selectSearch.search true
                selectSearch.placeholder "Select a resource group to filter resources"
                selectSearch.onChange (Some >> setResourceGroup)
            ]

            Html.br [ ]

            match selectedResourceGroup with
            | None -> Html.none
            | Some resourceGroup -> AzureResourcesWithinResourceGroup resourceGroup
        ]


[<ReactComponent>]
let AzureStartPage() =
    Html.div [
        MarkdownContent """
### Import from Microsoft Azure

The importer tool requires you to authenticate with Azure using the CLI to import resources from your Azure account.

Use the [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/) to login as follows:
```
az login
```
Then you can start the importer tool to import resources from your Azure account.

If you are already logged in, click 'Continue' to start importing resources.
"""

        Html.br [  ]
        Html.button [
            prop.className "button is-primary"
            prop.text "Continue"
            prop.onClick (fun _ -> Router.navigate("azure"))
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
                        AwsImporter()
                    | [ "aws-start" ] ->
                        AwsStartPage()
                    | [ "azure-start" ] ->
                        AzureStartPage()
                    | [ "azure" ] ->
                        AzureImporter()
                    | [ "import-preview" ] ->
                        MarkdownContent """
### Pulumi Import Preview
Use the import preview to experiment with [Pulumi Import](https://www.pulumi.com/docs/cli/commands/pulumi_import/).
Edit the JSON content and preview the import results in different languages alongside the imported stack state.
                        """
                        ImportPreview("{ \"resources\": [] }")
                    | _ ->
                        Html.p [
                            prop.text "Select a cloud provider to import resources from:"
                            prop.className "subtitle"
                        ]

                        AwsTile()
                        AzureTile()

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