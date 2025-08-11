# Test script to diagnose Configure IP issues
param(
    [string]$AdapterName = "Ethernet",
    [string]$IPAddress = "192.168.1.100",
    [string]$SubnetMask = "255.255.255.0",
    [string]$Gateway = "192.168.1.1"
)

Write-Host "Testing network configuration..." -ForegroundColor Yellow

# Test 1: Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
Write-Host "Running as Administrator: $isAdmin" -ForegroundColor $(if($isAdmin){"Green"}else{"Red"})

# Test 2: Get network adapter
Write-Host "`nFinding network adapter: $AdapterName" -ForegroundColor Yellow
$adapter = Get-NetAdapter -Name $AdapterName -ErrorAction SilentlyContinue
if ($adapter) {
    Write-Host "Adapter found: $($adapter.Name) - Status: $($adapter.Status)" -ForegroundColor Green
    Write-Host "Interface Index: $($adapter.InterfaceIndex)"
    Write-Host "Device ID: $($adapter.DeviceID)"
} else {
    Write-Host "Adapter not found!" -ForegroundColor Red
    Write-Host "Available adapters:"
    Get-NetAdapter | Format-Table Name, Status, InterfaceDescription
}

# Test 3: Try WMI method
Write-Host "`nTesting WMI configuration method..." -ForegroundColor Yellow
try {
    $wmiAdapter = Get-WmiObject Win32_NetworkAdapterConfiguration | Where-Object { $_.Description -like "*$AdapterName*" -and $_.IPEnabled }
    if ($wmiAdapter) {
        Write-Host "WMI Adapter found: $($wmiAdapter.Description)" -ForegroundColor Green
        Write-Host "Current IP: $($wmiAdapter.IPAddress[0])"
        Write-Host "DHCP Enabled: $($wmiAdapter.DHCPEnabled)"
        
        # Try to set static IP
        Write-Host "`nAttempting to set static IP..." -ForegroundColor Yellow
        $result = $wmiAdapter.EnableStatic($IPAddress, $SubnetMask)
        Write-Host "EnableStatic result: $result" -ForegroundColor $(if($result -eq 0){"Green"}else{"Red"})
        
        if ($Gateway) {
            $gatewayResult = $wmiAdapter.SetGateways($Gateway, 1)
            Write-Host "SetGateways result: $gatewayResult" -ForegroundColor $(if($gatewayResult -eq 0){"Green"}else{"Red"})
        }
    } else {
        Write-Host "WMI Adapter not found!" -ForegroundColor Red
    }
} catch {
    Write-Host "WMI Error: $_" -ForegroundColor Red
}

# Test 4: Try netsh method
Write-Host "`nTesting netsh configuration method..." -ForegroundColor Yellow
$netshCommand = "netsh interface ip set address name=`"$AdapterName`" static $IPAddress $SubnetMask $Gateway"
Write-Host "Command: $netshCommand" -ForegroundColor Cyan
try {
    $netshResult = & netsh interface ip set address name="$AdapterName" static $IPAddress $SubnetMask $Gateway 2>&1
    Write-Host "Netsh result: $netshResult" -ForegroundColor $(if($LASTEXITCODE -eq 0){"Green"}else{"Red"})
} catch {
    Write-Host "Netsh Error: $_" -ForegroundColor Red
}

# Test 5: Check current configuration
Write-Host "`nCurrent adapter configuration:" -ForegroundColor Yellow
Get-NetIPAddress -InterfaceAlias $AdapterName -ErrorAction SilentlyContinue | Format-Table IPAddress, PrefixLength, AddressFamily

Write-Host "`nDiagnostics complete!" -ForegroundColor Green