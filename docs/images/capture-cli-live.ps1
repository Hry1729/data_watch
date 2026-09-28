Set-Location -LiteralPath 'D:\python\device_alarm\data_watch'
$Host.UI.RawUI.WindowTitle = 'Data Watch CLI - Live Query'
$Host.UI.RawUI.BackgroundColor = 'Black'
$Host.UI.RawUI.ForegroundColor = 'Gray'
mode.com con cols=116 lines=54
Clear-Host
Write-Host 'Data Watch CLI | Actual backend query' -ForegroundColor Cyan
Write-Host ('Captured at: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')) -ForegroundColor DarkGray
Write-Host ''
Write-Host 'PS> python -m cli.main --pretty realtime get --device QC100 --point CraneID' -ForegroundColor Cyan
python -m cli.main --pretty realtime get --device QC100 --point CraneID
Write-Host ''
Write-Host 'PS>' -ForegroundColor Cyan
