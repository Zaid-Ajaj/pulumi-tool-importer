[<RequireQualifiedAccess>]
module Azure

open Feliz
open Feliz.Router
open Feliz.UseDeferred
open Feliz.SelectSearch

[<ReactComponent>]
let StartPage() =
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
let Account() =
    let account = React.useDeferred(Server.api.azureAccount(), [|  |])
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
let AzureResourcesWithinResourceGroup(resourceGroup: string) =
    let currentTab, setCurrentTab = React.useState "resources"
    let response = React.useDeferred(Server.api.getResourcesUnderResourceGroup resourceGroup, [| resourceGroup |])
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
                Import.Preview(response.pulumiImportJson, "")
            | _ ->
                Html.none
        ]

[<ReactComponent>]
let ResourceGroups() =
    let selectedResourceGroup, setResourceGroup = React.useState<string option>(None)
    let resourceGroups = React.useDeferred(Server.api.getResourceGroups(), [|  |])
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
            Account()
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
