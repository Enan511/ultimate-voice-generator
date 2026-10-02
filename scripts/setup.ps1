param([ValidateSet('vulkan','cpu','cuda')][string]$Backend='vulkan')
& "$PSScriptRoot/setup-qwen.ps1" -Backend $Backend
