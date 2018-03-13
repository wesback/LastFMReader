# LastFMReader
Small demo app that uses the LastFM API to get your track history

Please make sure your read the LastFM TOS if you want to use this application
https://www.last.fm/api/tos

# Get started
Create an Azure Storage Account
By default a container lastfmdata is expected

Change the appsettings.json file
Replace <LASTFMKEY> with your LastFM API key (you can get yours at https://www.last.fm/api)
Replace <STORAGEACCOUNT> with the storage account you created
Replace <STORAGEKEY> with the key
Replace or add <USER> entries with the usernames you want to extract information for

# Docker
Install the Azure CLI: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest or go to https://shell.azure.com/


I have foreseen a Docker container at https://hub.docker.com/r/wesback/lastfmreader/ 

If you want to run this on an Azure Container Instance use the following commands
```bash
az login
az group create -l westeurope -n RGLastFMReader

az container create -g RGLastFMReader --name lastfmreader --image wesback/lastfmreader --cpu 1 --memory 1 --restart-policy Never --location=westeurope -e lastfmkey=<LASTFMKEY> storageaccount=<STORAGEACCOUNT> storagekey=<STORAGEKEY> lastfmuser=<USER>
```
