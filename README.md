# Pulumi Importer (WIP)

A Pulumi tool for automatic resource discovery and resource import from cloud providers.

### Install

The importer as implemented a pulumi plugin of type `tool` which means it can be installed  using the pulumi CLI.

```
pulumi plugin install tool importer --server github://api.github.com/Zaid-Ajaj
```

After installing the plugin, you can run it using the following command:

```
pulumi plugin run importer
```

It will spin up a web server running at `http://localhost:5000` where you can navigate to so that you can  interact with the importer.

## Development

To run the project locally, you need to have the following installed:
 - Dotnet SDK v6.x
 - Nodejs v18.x or later
 - Pulumi CLI (preferably latest)

To run the project locally, you can run the following commands:
```bash
dotnet run
```