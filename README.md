MovieTitler
-----------

This is the source code for the MovieTitler ActivityPub bot
(@bot@movietitler.azurewebsites.net)[https://movietitler.azurewebsites.net].

Every day, MovieTitler will generate a new movie title by combining the title
of one film and the subtitle of another (excluding real movie titles that it
knows about, and titles it's generated within the past 90 days). In general,
the bot includes the 50 top-grossing movies in the U.S. for each year from
1987 through the most recent full year, although for the most recent year, the
top 100 movies are included.

MovieTitler implements ActivityPub, HTML, and Markdown responses through
content negotiation. The RSS and Atom feeds are implemented on the same
endpoint as page 1 of the ActivityPub outbox, and are linked from the home
page.

## Implementation details

The MovieTitler code is adapted from [Crowmask](https://github.com/IsaacSchemm/Crowmask/)
but is largely simplified.

Layers:

* **MovieTitler.Interfaces** (VB.NET): contains interfaces used to pass config
  values between layers or to allow inner layers to call outer-layer code.
* **MovieTitler.Data** (C#): contains the data types and and data context, which
  map to documents in the Cosmos DB backend of EF Core.
* **MovieTitler.LowLevel** (F#): converts internal data objects to ActivityPub
  objects or Markdown / HTML pages and maps internal IDs to ActivityPub IDs.
* **MovieTitler.HighLevel** (C#):
    * **Signatures**: HTTP signature validation, adapted from
      [Letterbook](https://github.com/Letterbook/Letterbook).
    * **Remote**: Talks to other ActivityPub servers.
    * **FeedBuilder**: Implements RSS and Atom feeds.
    * **RemoteInboxLocator**: Collects inbox URLs for the admin actors,
      followers, and other known servers.
* **MovieTitler.Generation**: Contains the list of movie titles.
* **MovieTitler** (C#): The main Azure Functions project, responsible for
  handling HTTP requests and running timed functions.

Example `local.settings.json`:

    {
      "IsEncrypted": false,
      "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "ApplicationHost": "example.azurewebsites.net",
        "CosmosDBAccountEndpoint": "https://example.documents.azure.com:443/",
        "CosmosDBAccountKey": "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000==",
        "KeyVaultHost": "example.vault.azure.net"
      }
    }

For **Key Vault**, the app is set up to use Managed Identity - turn this on in
the Function App (Settings > Identity) then go to the key vault's access
control (IAM) tab to give a role assignment of Key Vault Crypto User to that
new managed identity.

For **Cosmos DB**, you will need to create the container in Data Explorer:

* Database ID: `MovieTitler`
* Container ID: `BotDbContext`
* Partition key: `__partitionKey`
