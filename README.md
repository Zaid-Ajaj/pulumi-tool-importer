# Pulumi Importer

A Pulumi tool plugin for automatic resource discovery and resource import from cloud providers. Currently supports AWS and Azure. 

It lets you search for your resources, automatically generate a Pulumi Import JSON file from them and preview a import operations see to see the generated import code without leaving the UI.

### Install via Pulumi 

The importer is implemented as a pulumi plugin of type `tool` which means it can be installed  using the Pulumi CLI (requires CLI v3.121.0 or later)

```
pulumi plugin install tool importer --server github://api.github.com/Zaid-Ajaj
```

After installing the plugin, you can run it using the following command:

```
pulumi plugin run importer
```
It will spin up a web server running at `http://localhost:5000` where you can navigate to interact with the importer.

### Unintall via Pulumi

To uninstall the plugin, you can run the following command:

```
pulumi plugin rm tool importer
```

### Update to latest version

You can uninstall and reinstall the plugin to get the latest version of the importer.

### Manual Installation

You can install the manually by going to the [releases page](https://github.com/Zaid-Ajaj/pulumi-tool-importer/releases) and downloading the latest release for your platform. 

The download is a tar archive that contains the executable `pulumi-tool-importer` alongside its assets which you can run directly.

It will spin up a web server running at `http://localhost:5000` where you can navigate to so that you can interact with the importer.


## Development

To run the project locally, you need to have the following installed:
 - Dotnet SDK v6.x
 - Nodejs v20.x or later
 - Pulumi CLI (preferably latest)

To run the project locally, you can run the following commands:
```bash
dotnet run
```
This will start two processes in parallel:
 - the backend server running at `http://localhost:5000`
 - the frontend app running at `http://localhost:8080` 

Navigate to the frontend app at `http://localhost:8080` to interact with the importer. 