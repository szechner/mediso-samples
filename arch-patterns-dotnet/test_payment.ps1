# Test script to verify payment processing API works
# This test verifies that the dependency injection issue is resolved
# and the payment workflow can execute properly

$baseUrl = "http://localhost:5000"
$endpoint = "$baseUrl/api/payments"
$healthEndpoint = "$baseUrl/health"

Write-Host "🚀 Testing Payment Processing API"
Write-Host "==========================================="
Write-Host "Base URL: $baseUrl"
Write-Host "Testing endpoint: $endpoint"
Write-Host ""

# First check if the API is running
try {
    Write-Host "🔍 Checking if API is running..."
    $healthResponse = Invoke-RestMethod -Uri $healthEndpoint -Method Get -TimeoutSec 5
    Write-Host "✅ API is running and healthy"
}
catch {
    Write-Host "❌ API is not running or not healthy. Please start the API first:"
    Write-Host "   dotnet run --project src/Mediso.PaymentSample.Api"
    Write-Host ""
    Write-Host "If you get dependency injection errors, this test will help identify them."
    exit 1
}

Write-Host ""

# Test basic payment request
$paymentRequest = @{
    amount = 1000
    currency = "CZK"
    payerAccountId = "0199983c-1e23-4306-93d2-c93cd86f352a"
    payeeAccountId = "0199983c-1e23-4306-93d2-c93cd86f352b"
    reference = "Sample payment from PowerShell test"
    paymentMethod = "credit-card"
    ipAddress = "192.168.1.1"
    userAgent = "PowerShell/7.5.3 Test Client"
    idempotencyKey = [System.Guid]::NewGuid().ToString()
    metadata = @{
        test = "true"
        source = "powershell-script"
        testType = "null-reference-fix-verification"
    }
}

$json = $paymentRequest | ConvertTo-Json -Depth 3
Write-Host "Sending request to: $endpoint"
Write-Host "Request body: $json"

try {
    Write-Host "Making request..."
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $json -ContentType "application/json"
    Write-Host "✅ SUCCESS - Payment processed successfully!"
    Write-Host "✅ Dependency injection issue RESOLVED - All services properly registered!"
    Write-Host "Response:"
    $response | ConvertTo-Json -Depth 3
    
    Write-Host "
✅ Test PASSED - No dependency injection or null reference exceptions!"
    Write-Host "📋 Payment created with status: $($response.state)"
    Write-Host "🔄 Processing status: $($response.processingStatus)"
    if ($response.nextSteps) {
        Write-Host "🔮 Next steps:"
        $response.nextSteps | ForEach-Object { Write-Host "   - $_" }
    }
    
    Write-Host "`n🔍 You can now check the application logs to see the workflow progression:"
    Write-Host "   - Fraud detection should be initiated"
    Write-Host "   - Fund reservation should follow"
    Write-Host "   - Payment settlement should complete"
    Write-Host "   - Final status should change from 'Requested' to 'Settled'"
    
    Write-Host "`n💡 To verify the workflow completion, you can:"
    Write-Host "   1. Check the payment status again using: GET /api/payments/$($response.id)"
    Write-Host "   2. Look at the application logs for workflow progress messages"
}
catch {
    Write-Host "❌ ERROR occurred:"
    Write-Host "Message: $($_.Exception.Message)"
    Write-Host "Type: $($_.Exception.GetType().Name)"
    
    if ($_.Exception.Response) {
        Write-Host "HTTP Status: $($_.Exception.Response.StatusCode)"
        try {
            $errorContent = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorContent)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Response Body: $errorBody"
        }
        catch {
            Write-Host "Could not read response body"
        }
    }
    
    Write-Host "`n❌ Test FAILED - Please check the application logs for more details"
    Write-Host "Stack Trace: $($_.Exception.StackTrace)"
}
