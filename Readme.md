تست ۱: Scalability (مقیاس‌پذیری) — Rate Limiting

1..150 | ForEach-Object {
    $body = '{"customerId":"C001","amount":100}'
    try {
        $response = Invoke-RestMethod -Uri "https://localhost:7096/api/orders" `
            -Method Post `
            -ContentType "application/json" `
            -Body $body
        Write-Host "OK: id=$($response.id)" -ForegroundColor Green
    }
    catch {
        Write-Host "RATE LIMITED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

------
تست ۲: Resilience (تاب‌آوری) — Retry, Circuit Breaker

1..30 | ForEach-Object {
    $i = $_
    $body = '{"customerId":"C001","amount":100}'
    $headers = @{ "X-Correlation-Id" = "test-$i" }
    try {
        Invoke-RestMethod -Uri "https://localhost:7096/api/orders" `
            -Method Post `
            -ContentType "application/json" `
            -Headers $headers `
            -Body $body
        Write-Host "OK: test-$i" -ForegroundColor Green
    }
    catch {
        Write-Host "FAIL: test-$i - $($_.Exception.Message)" -ForegroundColor Red
    }
}


-------
تست ۳: Bulkhead (جداسازی منابع)

1..30 | ForEach-Object {
    $body = '{"customerId":"C001","amount":100}'
    Start-Job -ScriptBlock {
        param($url, $body)
        try {
            Invoke-RestMethod -Uri $url -Method Post `
                -ContentType "application/json" -Body $body
        } catch { }
    } -ArgumentList "https://localhost:7096/api/orders", $body
}
Get-Job | Wait-Job | Remove-Job# Scalability-Resilience-Bulkhead
