# AzureTableCopy
services to copy azure tables - within or across storage accounts

## Deploy and and run it
The solution consists of two azure webjobs and a console application. To deploy and configure 
the solution, simply change to the `Deployment` directory and run `.\Deploy-AzureTableCopy.ps1`:

``` .\Deploy-AzureTable <Location> <DeploymentPostFix> <SubscriptionId>``` 

e.g. 

``` .\Deploy-AzureTable "West Europe" cloudprod 11111-2222-3333``` 


Once the service are deployed, you can submit and monitor your copy jobs using the console application
``AzureTableCopy.exe``

Run ``AzureTableCopy.exe`` without arguments to get a list of running and completed jobs.

To submit copy jobs, simply provide the path to a json file which describes the copy jobs. 
``CopyJobTemplate.json`` provides a template json:

```
{
  "sourceStorageConnectionString": "SOURCE_STORAGEACCOUNT_CONNECTION_STRING",
  "destinationStorageConnectionString": "DESTINATION_STORAGEACCOUNT_CONNECTION_STRING",
  "tables": [
    {
      "source": "sourceTable1",
      "destination": "destinationTable1"
    },
    {
      "source": "sourceTable2",
      "destination": "destinationTable2"
    }
  ]
}
```

#How it works
For each table, the console application submits a copy job request to an azure queue called ``copyinputstep1``.
Each message triggers the ``AzureTableCopyStep1Web`` webjob which then splits the table into batches for parallel processing.
It basically submits a copy request with the current position (the continuation token) to a queue called  ``copyinput``.
This triggers the ``AzureTableCopyStep2Web`` webjob which then copies the actual entities for the referenced batch.
To ensure that ``AzureTableCopyStep1Web`` webjob doesn't run too long, we exit the job after configured amount of iterations. 
We re-trigger the job execution by simply adding a continuation message to the  ``copyinputstep1`` before exiting. This will re-trigger 
the webjob and carry over its recent state. 

All monitoring is done through the ``copyjobtracker`` table. 

##Future considerations
- Taking partitions into account for parallel execution 
- re-run faulty batches
