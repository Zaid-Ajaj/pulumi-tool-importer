module Aws

open Feliz
open Feliz.UseDeferred
open Feliz.SelectSearch

[<ReactComponent>]
let StartPage() =
    Html.div [
        MarkdownContent """
### Import from Amazon Web Services

The importer tool uses the [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-quickstart.html) to authenticate with AWS and access its services.

We assume that you already have configured your AWS CLI with the necessary permissions to access your resources.

If you haven't done so, please follow the [AWS CLI configuration guide](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-quickstart.html).

You can verify that you are logged in by running the following command:

```bash
aws configure export-credentials
```

You can also override which profile to use by setting the `AWS_PROFILE` environment variable.

If you are ready to go, choose one of the following options to start importing resources:
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
let Page(awsPage: string -> ReactElement) =
    let region, setRegion = React.useState "__default__"
    let callerIdentity = React.useDeferred(Server.api.awsCallerIdentity(), [| region |])
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
                    match callerIdentity.accountAlias with
                    | None ->
                        prop.text $"Logged in as {callerIdentity.userId} (Account ID: {callerIdentity.accountId})"
                    | Some alias ->
                        prop.text $"Logged in as {callerIdentity.userId} ({alias})"
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
let CloudFormationGeneratedTemplate(templateName: string, region: string) =
    let loadTemplate() = async {
        return! Server.api.getAwsCloudFormationGeneratedTemplate {
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
                Import.Preview(response.pulumiImportJson, region)

            | _ ->
                Html.none
        ]



[<ReactComponent>]
let CloudFormationGeneratedTemplates(region: string) =
    let selectedTemplateName, setTemplateName = React.useState<string option>(None)
    let templates = React.useDeferred(Server.api.getAwsCloudFormationGeneratedTemplates region, [| region |])
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
            | Some templateName -> CloudFormationGeneratedTemplate(templateName, region)
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
let CloudFormationResourcesByStack stack =
    let tab, setTab = React.useState "resources"
    let response = React.useDeferred(Server.api.getAwsCloudFormationResources stack, [| stack.stackId |])
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

                        Html.li [
                            prop.children [
                                Html.a [ Html.span "Resource Data" ]
                            ]
                            prop.onClick (fun _ -> setTab "data")
                            if tab = "data" then
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

                Import.Preview(response.pulumiImportJson, stack.region)

            | "import-json" ->
                ImportJsonDocs stack.region
                Html.pre [
                    prop.style [ style.maxHeight 400; style.overflow.auto ]
                    prop.children [
                        Html.code response.pulumiImportJson
                    ]
                ]

            | "data" ->
                Html.pre response.resourceDataJson

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
let CloudFormationStacks(region: string) =
    let selectedStackId, setStackId = React.useState<string option>(None)
    let stacks = React.useDeferred(Server.api.getAwsCloudFormationStacks region, [| region |])
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
                | Some stack -> CloudFormationResourcesByStack stack
        ]


[<ReactComponent>]
let ResourceExplorer(region: string) =
    let searchInputRef = React.useInputRef()
    let tagsInputRef = React.useInputRef()
    let currentTab, setCurrentTab = React.useState "resources"
    let searchResults, setSearchResults = React.useState(Deferred.HasNotStartedYet)
    let search = React.useDeferredCallback(Server.api.searchAws, setSearchResults)

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
                Import.Preview(response.pulumiImportJson, region)
            | _ ->
                Html.none
    ]
