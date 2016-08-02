#
# Deploy_EmotionServices.ps1
# example call:
#
Param(
	[string] [Parameter(Mandatory=$true)] $Location,
	[string] [Parameter(Mandatory=$true)] $NamePostFix,
	[string] [Parameter(Mandatory=$true)] $SubscriptionId,
	[Boolean] [Parameter(Mandatory = $false)] $DeleteExistingSites = $false,  
	[string] [Parameter(Mandatory=$false)] $MsBuildPath = "C:\Program Files (x86)\MSBuild\14.0\bin",
	[string] [Parameter(Mandatory=$false)] $ZipCmd = "C:\Program Files\7-Zip\7z.exe"
)

#configure powershell with Azure 1.7 modules
Import-Module Azure

# Login-AzureRmAccount
# Add-AzureAccount

Set-AzureSubscription -SubscriptionId $SubscriptionId
Select-AzureRmSubscription -SubscriptionId $SubscriptionId
Select-AzureSubscription -SubscriptionId $SubscriptionId

$ResourceGroupName = "AzureTableCopy$NamePostFix" 
$WebJobWebSiteName = "AzureTableCopyWebJobs$NamePostFix"


function Create-StorageAccountIfNotExist {
	[CmdletBinding()] 
	param ( 
		[string] [Parameter(Mandatory = $true)] $StorageRGName,
		[string] [Parameter(Mandatory = $true)] $StorageAccountName
	)

     $StorageAccount = Get-AzureRmStorageAccount | Where-Object {$_.StorageAccountName -eq $StorageAccountName }  
	 if ($StorageAccount -eq $null) { 
         Write-Host "create storage account $StorageAccountName in $Location" 
		 New-AzureRmStorageAccount -ResourceGroup $StorageRGName -AccountName $StorageAccountName -Location $Location -Type "Standard_GRS"
     } 
     else { 
         Write-Host "storage $StorageAccountName already exists" 
     }   
}

function New-WebApp {
	param ( 
		[Parameter(Mandatory = $true)] [String] $ResourceGroupName,
		[Parameter(Mandatory = $true)] [String] $WebAppName,
		[Parameter(Mandatory = $true)] [String] $ServicePlanName
    ) 
	if ((Get-AzureRmWebApp | Where-Object {$_.Name -eq $WebAppName }) -ne $null) { 
		if ($DeleteExistingSites -eq $true) { 
			Write-Host "WebApp $WebAppName already exists and will be deleted" 
			Remove-AzureRmWebApp -Name $WebAppName -ResourceGroupName $ResourceGroupName -Force
		} 
	}
	New-AzureRmWebApp -Name $WebAppName -ResourceGroupName $ResourceGroupName -AppServicePlan $ServicePlanName -Location $Location
}

New-AzureRmResourceGroup -Name $ResourceGroupName -Location $Location
$ServicePlanName = $ResourceGroupName + "-ServicePlan"
New-AzureRmAppServicePlan -ResourceGroupName $ResourceGroupName `
	  -Name $ServicePlanName `
	  -Location $Location `
	  -Tier "Standard" `
	  -NumberofWorkers 4 `
	  -WorkerSize "Medium"

$StorageAccountName = ($ResourceGroupName).ToLower()

Create-StorageAccountIfNotExist $ResourceGroupName $StorageAccountName


$StorageKey = (Get-AzureRmStorageAccountKey -ResourceGroupName $ResourceGroupName -AccountName $StorageAccountName).Value[0]
$StorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=$StorageAccountName;AccountKey=$StorageKey" 

Write-Host "Update AzureTableCopy\bin\release\azuretablecopy.exe.config with newly created storage account"
Push-Location
cd ..\AzureTableCopy
$cfg = [xml](get-content "app.config")
$as= $cfg.configuration.appSettings.add|?{$_.key -eq "QueueStorageAccount"};
$as.value = "$StorageConnectionString"
# $PWD is needed to call the local variable see: http://powershell.org/wp/2013/09/26/powershell-gotcha-relative-paths-and-net-methods/
$cfg.Save("$PWD\app.config");
Pop-Location

&$MsBuildPath\msbuild.exe ..\AzureTableCopy.sln /p:Configuration=Release /t:Clean /verbosity:quiet
Write-Host "Build Solution"
&$MsBuildPath\msbuild.exe ..\AzureTableCopy.sln /p:Configuration=Release /t:Publish /p:TargetProfile=Cloud /verbosity:quiet

$WebjobAppSettings = @{
	"WEBSITE_NODE_DEFAULT_VERSION" = "4.2.3"
	"STORAGE_CONNECTION_STRING" = $StorageConnectionString
}

$WebjobConnectionStrings = @{Name = "AzureWebJobsDashboard"; Type = "Custom"; ConnectionString = $StorageConnectionString; }, 
						   @{Name = "AzureWebJobsStorage"; Type = "Custom"; ConnectionString = $StorageConnectionString ; }


Write-Host "Deploy the WebJobs to $WebJobWebSiteName"
New-WebApp $ResourceGroupName $WebJobWebSiteName $ServicePlanName
.\Deploy-AzureTableCopyWebJobs $Location $WebJobWebSiteName $WebjobAppSettings $WebjobConnectionStrings $ZipCmd




