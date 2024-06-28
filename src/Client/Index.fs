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

[<Emit "navigator.clipboard.writeText($0)">]
let copyToClipboard (text: string) : unit = jsNative

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
let AwsRegionImportDocs(region:string) = MarkdownContent $"""
Note: we run import previews with environment variable AWS_REGION={region}
Remember to setup your Pulumi stack with the correct AWS region using `pulumi config set aws:region {region}`
when you actually run the import command.
"""

[<ReactComponent>]
let ImportPreview(importJson: string, region: string) =
    let importJsonInputRef = React.useInputRef()
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
                    prop.onClick (fun _ ->
                        importJsonInputRef.current
                        |> Option.iter (fun importJsonInput ->
                            importPreview {
                                language = language
                                pulumiImportJson = importJsonInput.value
                                region = region
                            }))
                ]
            ]
        ]

        Html.br [ ]

        if region <> "__default__" && region <> ""
            then AwsRegionImportDocs region

        Html.details [
            prop.style [
                style.marginTop 10
                style.marginBottom 10
            ]

            prop.children [
                Html.summary "Edit Pulumi Import JSON:"
                Html.div [
                    prop.className "control"
                    prop.children [
                         Html.textarea [
                            prop.style [ style.height 350 ]
                            prop.className "input"
                            prop.ref importJsonInputRef
                            prop.defaultValue importJson
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
                match response.standardError with
                | Some error -> Tab("Import Errors", "error", tab, setTab)
                | None -> ()
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
            | "error" ->
                Html.p [
                    prop.style [ style.color.red ]
                    prop.text (Option.defaultValue "" response.standardError)
                ]
            | _ ->
                Html.none
    ]

[<ReactComponent>]
let ImportJsonDocs(region: string) =
    let command =
        if region <> "__default__" && region <> ""
        then $"\nRemember to setup your Pulumi stack with the AWS region using `pulumi config set aws:region {region}` before running the import\n"
        else ""

    MarkdownContent $"""
The following JSON content can be used by pulumi to batch import the resources into a pulumi project.

Put the contents inside a file called `import.json` and run `pulumi import -f import.json`
{command}
See [pulumi import](https://www.pulumi.com/docs/cli/commands/pulumi_import/) to learn more about importing resources.
"""

[<ReactComponent>]
let AwsResourceExplorer(region: string) =
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
                                            region = region
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
                                            region = region
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
                                            region = region
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
                ImportJsonDocs region
                Html.pre [
                    prop.style [ style.maxHeight 400; style.overflow.auto ]
                    prop.children [
                        Html.code response.pulumiImportJson
                    ]
                ]

            | "warnings" ->
                response.warnings
                |> String.concat "\n\n---\n\n"
                |> MarkdownContent

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
                                Html.th "ARN"
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
                                        style.maxWidth 300
                                        style.overflow.hidden
                                    ]
                                ]

                                Html.td [
                                    Html.button [
                                        prop.text "Copy ARN"
                                        prop.className "button is-small is-primary"
                                        prop.style [ style.marginLeft 10 ]
                                        prop.onClick (fun _ -> copyToClipboard resource.arn)
                                    ]
                                ]

                                Html.td resource.resourceType
                                Html.td resource.region
                                Html.td resource.service
                                Html.td [
                                    prop.style [
                                        style.fontSize 12
                                        style.maxWidth 200
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
                ImportPreview(response.pulumiImportJson, region)
            | _ ->
                Html.none
    ]


[<ReactComponent>]
let AwsImporter(awsPage: string -> ReactElement) =
    let region, setRegion = React.useState "__default__"
    let callerIdentity = React.useDeferred(serverApi.awsCallerIdentity(), [| region |])
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
            ]

        | Deferred.Resolved (Ok callerIdentity) ->
            Html.div [
                Html.i [
                    prop.className "fas fa-user"
                    prop.style [ style.marginRight 10 ]
                ]

                Html.span [
                    prop.style [ style.fontSize 18; style.marginRight 10 ]
                    prop.text $"Logged in as {callerIdentity.userId}"
                ]

                Html.div [
                    prop.style [ style.width 350; style.position.relative; style.display.inlineBlock ]
                    prop.children [
                        SelectSearch.selectSearch [
                            selectSearch.options [
                                { value = "__default__"; name = "Select a region"; disabled = false }
                                { value = "us-east-2"; name = "US East Ohio / us-east-2"; disabled = false }
                                { value = "us-east-1"; name = "US East (N. Virginia) / us-east-1"; disabled = false }
                                { value = "us-west-1"; name = "US West (N. California) / us-west-1"; disabled = false }
                                { value = "us-west-2"; name = "US West (Oregon) / us-west-2"; disabled = false }
                                { value = "af-south-1"; name = "Africa (Cape Town) / af-south-1"; disabled = false }
                                { value = "ap-east-1"; name = "Asia Pacific (Hong Kong) / ap-east-1"; disabled = false }
                                { value = "ap-south-1"; name = "Asia Pacific (Mumbai) / ap-south-1"; disabled = false }
                                { value = "ap-northeast-2"; name = "Asia Pacific (Seoul) / ap-northeast-2"; disabled = false }
                                { value = "ap-southeast-1"; name = "Asia Pacific (Singapore) / ap-southeast-1"; disabled = false }
                                { value = "ap-southeast-2"; name = "Asia Pacific (Sydney) / ap-southeast-2"; disabled = false }
                                { value = "ap-northeast-1"; name = "Asia Pacific (Tokyo) / ap-northeast-1"; disabled = false }
                                { value = "ca-central-1"; name = "Canada (Central) / ca-central-1"; disabled = false }
                                { value = "eu-central-1"; name = "Europe (Frankfurt) / eu-central-1"; disabled = false }
                                { value = "eu-west-1"; name = "Europe (Ireland) / eu-west-1"; disabled = false }
                                { value = "eu-west-2"; name = "Europe (London) / eu-west-2"; disabled = false }
                                { value = "eu-south-1"; name = "Europe (Milan) / eu-south-1"; disabled = false }
                                { value = "eu-west-3"; name = "Europe (Paris) / eu-west-3"; disabled = false }
                            ]

                            selectSearch.value region
                            selectSearch.search true
                            selectSearch.placeholder "Select a region"
                            selectSearch.onChange setRegion
                        ]
                    ]
                ]

                Html.br [ ]
                Html.br [ ]

                awsPage region
            ]
    ]

[<ReactComponent>]
let AwsStartPage() =
    Html.div [
        MarkdownContent """
### Import from Amazon Web Services

The importer tool requires you to authenticate with AWS using the [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-quickstart.html).

Start by [logging in](https://docs.aws.amazon.com/signin/latest/userguide/command-line-sign-in.html) as follows:

```
aws sso login
```

If you are already logged in, choose one of the following options to import resources:
"""
        Html.br [  ]
        Html.p [
            prop.style [ style.fontSize 18 ]
            prop.children [
                Html.a [
                    prop.href "/#aws"
                    prop.style [ style.marginRight 5 ]
                    prop.children [
                        Html.i [
                            prop.className "fa fa-chevron-right"
                            prop.style [ style.marginRight 5  ]
                        ]
                        Html.text "AWS Resource Explorer"
                    ]
                ]

                Html.span "Query your resources and import them using the AWS Resource Explorer."
            ]
        ]

        Html.p [
            prop.style [ style.fontSize 18 ]
            prop.children [
                Html.a [
                    prop.href "/#aws-cloudformation-stacks"
                    prop.style [ style.marginRight 5 ]
                    prop.children [
                        Html.i [
                            prop.className "fa fa-chevron-right"
                            prop.style [ style.marginRight 5  ]
                        ]
                        Html.text "Cloud Formation Stacks"
                    ]
                ]

                Html.span "Lookup and import resources from your Cloud Formation stacks."
            ]
        ]

        Html.p [
            prop.style [ style.fontSize 18 ]
            prop.children [
                Html.a [
                    prop.href "/#aws-cloudformation-generated-templates"
                    prop.style [ style.marginRight 5 ]
                    prop.children [
                        Html.i [
                            prop.className "fa fa-chevron-right"
                            prop.style [ style.marginRight 5  ]
                        ]
                        Html.text "Cloud Formation Generated Templates"
                    ]
                ]

                Html.span "Convert your Cloud Formation generated templates into Pulumi import definitions."
            ]
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
                ImportJsonDocs ""
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
                ImportPreview(response.pulumiImportJson, "")
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
let CloudFormationErrorMessageOnPreview() = Html.p [
    prop.className "subtitle"
    prop.style [
        style.marginTop 10
        style.marginBottom 20
        style.fontSize 16
    ]
    prop.children [
        Html.i [
            prop.className "fas fa-exclamation-triangle"
            prop.style [ style.color.red; style.marginRight 10 ]
        ]

        Html.span "There are errors in the generated import JSON. "
        Html.span "You might need to edit the import file manually them before proceeding. "
        Html.span "Alternatively, you can file an issue on our "
        Html.a [
            prop.href "https://github.com/Zaid-Ajaj/pulumi-tool-importer"
            prop.text "GitHub Repository"
        ]
        Html.span " so we can fix the problem"
    ]
]


[<ReactComponent>]
let AwsCloudFormationResourcesByStack stack =
    let tab, setTab = React.useState "resources"
    let response = React.useDeferred(serverApi.getAwsCloudFormationResources stack, [| stack.stackId |])
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
                                Html.a [ Html.span $"Resources ({List.length response.resources})" ]
                            ]
                            prop.onClick (fun _ -> setTab "resources")
                            if tab = "resources" then
                                prop.className "is-active"
                        ]

                        Html.li [
                            prop.children [
                                Html.a [ Html.span "Pulumi Import JSON" ]
                            ]
                            prop.onClick (fun _ -> setTab "import-json")
                            if tab = "import-json" then
                                prop.className "is-active"
                        ]

                        Html.li [
                            prop.children [
                                Html.a [ Html.span "Stack Template" ]
                            ]
                            prop.onClick (fun _ -> setTab "template")
                            if tab = "template" then
                                prop.className "is-active"
                        ]

                        if not (List.isEmpty response.warnings) then
                            Html.li [
                                prop.children [
                                    Html.a [ Html.span $"Warnings ({List.length response.warnings})" ]
                                ]
                                prop.onClick (fun _ -> setTab "warnings")
                                if tab = "warnings" then
                                    prop.className "is-active"
                            ]

                        if not (List.isEmpty response.errors) then
                            Html.li [
                                prop.children [
                                    Html.a [ Html.span $"Errors ({List.length response.errors})" ]
                                ]
                                prop.onClick (fun _ -> setTab "errors")
                                if tab = "errors" then
                                    prop.className "is-active"
                            ]

                        Html.li [
                            prop.children [
                                Html.a [ Html.span "Import Preview" ]
                            ]
                            prop.onClick (fun _ -> setTab "preview")
                            if tab = "preview" then
                                prop.className "is-active"
                        ]
                    ]
                ]
            ]

            match tab with
            | "preview" ->
                if not (List.isEmpty response.errors) then
                    CloudFormationErrorMessageOnPreview()

                ImportPreview(response.pulumiImportJson, stack.region)

            | "import-json" ->
                ImportJsonDocs stack.region
                Html.pre [
                    prop.style [ style.maxHeight 400; style.overflow.auto ]
                    prop.children [
                        Html.code response.pulumiImportJson
                    ]
                ]

            | "template" ->
                Html.pre response.templateBody

            | "warnings" ->
                response.warnings
                |> String.concat "\n\n---\n\n"
                |> MarkdownContent

            | "errors" ->
                response.errors
                |> String.concat "\n\n---\n\n"
                |> MarkdownContent

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
                                Html.th "Logical ID"
                                Html.th "Physical ID"
                                Html.th "Type"
                            ]
                        ]

                        Html.tbody [
                            for resource in response.resources do
                            Html.tr [
                                Html.td [
                                    prop.text resource.logicalId
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

                                Html.td resource.resourceType
                            ]
                        ]
                    ]
                ]

            | _ ->
                Html.none
        ]

[<ReactComponent>]
let AwsCloudFormationStacks(region: string) =
    let selectedStackId, setStackId = React.useState<string option>(None)
    let stacks = React.useDeferred(serverApi.getAwsCloudFormationStacks region, [| region |])
    match stacks with
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

    | Deferred.Resolved (Ok stacks) ->
        let stacksByCompletedFirst =
            stacks
            |> List.sortBy (fun stack ->
                match stack.status with
                | "CREATE_COMPLETE" -> 0
                | _ -> 1)

        React.fragment [
            SelectSearch.selectSearch [
                selectSearch.options [
                    for stack in stacksByCompletedFirst -> {
                        value = stack.stackId
                        name = $"{stack.stackName} ({stack.status})"
                        disabled = false
                    }
                ]

                selectSearch.value (defaultArg selectedStackId "")
                selectSearch.search true
                selectSearch.placeholder "Select a stack to import resources from"
                selectSearch.onChange (Some >> setStackId)
            ]

            Html.br [ ]

            match selectedStackId with
            | None -> Html.none
            | Some stackId ->
                match stacks |> List.tryFind (fun stack -> stack.stackId = stackId) with
                | None -> Html.none
                | Some stack -> AwsCloudFormationResourcesByStack stack
        ]

[<ReactComponent>]
let AwsCloudFormationGeneratedTemplate(templateName: string, region: string) =
    let loadTemplate() = async {
        return! serverApi.getAwsCloudFormationGeneratedTemplate {
            templateName = templateName
            region = region
        }
    }

    let currentTab, setCurrentTab = React.useState "template"
    let template = React.useDeferred(loadTemplate(), [| region; templateName |])
    match template with
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
                                Html.a [ Html.span "Template" ]
                            ]
                            prop.onClick (fun _ -> setCurrentTab "template")
                            if currentTab = "template" then
                                prop.className "is-active"
                        ]

                        Html.li [
                            prop.children [
                                Html.a [ Html.span "Resource Data" ]
                            ]
                            prop.onClick (fun _ -> setCurrentTab "data")
                            if currentTab = "data" then
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
            | "template" ->
                Html.pre response.templateBody

            | "data" ->
                Html.pre response.resourceDataJson

            | "import-json" ->
                ImportJsonDocs region
                Html.pre [
                    prop.style [ style.maxHeight 400; style.overflow.auto ]
                    prop.children [
                        Html.code response.pulumiImportJson
                    ]
                ]

            | "preview" ->
                ImportPreview(response.pulumiImportJson, region)

            | _ ->
                Html.none
        ]



[<ReactComponent>]
let AwsCloudFormationGeneratedTemplates(region: string) =
    let selectedTemplateName, setTemplateName = React.useState<string option>(None)
    let templates = React.useDeferred(serverApi.getAwsCloudFormationGeneratedTemplates region, [| region |])
    match templates with
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

    | Deferred.Resolved (Ok templates) ->
        React.fragment [
            SelectSearch.selectSearch [
                selectSearch.options [
                    for template in templates -> {
                        value = template.templateName
                        name = $"{template.templateName} ({template.resourceCount} resources)"
                        disabled = false
                    }
                ]

                selectSearch.value (defaultArg selectedTemplateName "")
                selectSearch.search true
                selectSearch.placeholder "Select a generated template to import resources from"
                selectSearch.onChange (Some >> setTemplateName)
            ]

            Html.br [ ]

            if List.isEmpty templates then
                Html.p "No generated templates found, did you create the templates in a different region?"

            match selectedTemplateName with
            | None -> Html.none
            | Some templateName -> AwsCloudFormationGeneratedTemplate(templateName, region)
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
                        AwsImporter(fun region -> AwsResourceExplorer region)
                    | [ "aws-cloudformation-stacks" ] ->
                        AwsImporter(fun region -> AwsCloudFormationStacks region)
                    | [ "aws-cloudformation-generated-templates" ] ->
                        AwsImporter(fun region -> AwsCloudFormationGeneratedTemplates region)
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
                        ImportPreview("{ \"resources\": [] }", "")
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