$csvDir = "D:\python\device_alarm\data_watch\collector\sidecars\opcda_push_service\data\AGV801"
Get-ChildItem "$csvDir\*.csv" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $newContent = $content -replace '(\d+)#(\d+)', '$2'
    Set-Content -Path $_.FullName -Value $newContent -NoNewline
    Write-Host "Fixed: $($_.Name)"
}
Write-Host "Done!"
