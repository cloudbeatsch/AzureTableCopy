Param(
	[string] [Parameter(Mandatory=$true)] $Location,
	[string] [Parameter(Mandatory=$true)] $WebsiteName,
			 [Parameter(Mandatory=$true)] $WebjobAppSettings,
			 [Parameter(Mandatory=$true)] $WebjobConnectionStrings,
	[string] [Parameter(Mandatory=$true)] $ZipCmd
)
	$JobCollectionName = "$WebsiteName-Collection"

	function DeployAndConfigureCSharpWebjob { 
		 [CmdletBinding()] 
		 param ( 
			[string] [Parameter(Mandatory = $true)]  $exe,  
			[string] [Parameter(Mandatory = $true)]  $path,  
			[string] [Parameter(Mandatory=$true)] $WebJobName,
		 	[string] [Parameter(Mandatory=$true)] $JobType, # e.g. Triggered
			[string] [Parameter(Mandatory=$false)] $Interval, # e.g. 5
			[string] [Parameter(Mandatory=$false)] $Frequency # e.g. Minute
		 ) 
		Write-Host "Deploy and Configure $WebJobName in $JobCollectionName"

		Push-Location
		cd $path

		$cfg = [xml](get-content ".\$exe.config")
		$con= $cfg.configuration.connectionStrings.add|?{$_.name -eq "AzureWebJobsStorage"};
		$con.connectionString = $WebjobAppSettings.STORAGE_CONNECTION_STRING
		$con= $cfg.configuration.connectionStrings.add|?{$_.name -eq "AzureWebJobsDashboard"};
		$con.connectionString = $WebjobAppSettings.STORAGE_CONNECTION_STRING
		# $PWD is needed to call the local variable see: http://powershell.org/wp/2013/09/26/powershell-gotcha-relative-paths-and-net-methods/
		$cfg.Save("$PWD\$exe.config");

		Write-Host "Create zip file for $path"
		$a = "a"
		$r = "-r"
		$f = "$WebJobName.zip"
		&$ZipCmd $a $r $f > output.log
		del output.log
		Write-Host "Done"

		Pop-Location
		$WebjobFilename = "$path\$f"
		# .\Deploy-AzureWebjob.ps1 $Location $WebsiteName $WebJobName $JobCollectionName Triggered $WebjobFilename 5 Minute
		.\Deploy-AzureWebjob.ps1 $Location $WebsiteName $WebJobName $JobCollectionName $JobType $WebjobFilename $Interval $Frequency
		Write-Host "Remove $WebjobFilename"
		Remove-Item $WebjobFilename


		Write-Host "Finished Deploy and Configure $WebJobName in $JobCollectionName"
	}

	Write-Host "Configure Webjob Website $WebsiteName in $Location"
	Set-AzureWebsite -Name $WebsiteName -AppSettings $WebjobAppSettings -ConnectionStrings $WebjobConnectionStrings
	Write-Host "Done"

	DeployAndConfigureCSharpWebjob AzureTableCopyStep1Web.exe "..\AzureTableCopyStep1WebJob\bin\Release" AzureTableCopyStep1WebJob Continuous
	DeployAndConfigureCSharpWebjob AzureTableCopyStep2Web.exe "..\AzureTableCopyStep2WebJob\bin\Release" AzureTableCopyStep2WebJob Continuous
	Push-Location;



