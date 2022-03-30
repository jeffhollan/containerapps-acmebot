# Container Apps Acmebot

Derived with gratitude from @shibayan.  [Please consider sponsoring shibayan](https://github.com/sponsors/shibayan).
<https://github.com/shibayan/keyvault-acmebot>
<https://github.com/shibayan/appservice-acmebot>

This application automates the issuance and renewal of ACME SSL/TLS certificates. This works with Azure Container Apps and the bot will request and generate a certificate, upload it to the container app environment, validate, and map a custom domain to the container app.

## Motivation

This is an effort to extend the projects from shibayan for compatibility with Container Apps.  This is a community created project with no official support from Microsoft.

## Feature Support

- Issuing certificates for Zone Apex and Wildcard (multiple domains)
- Automated certificate renewal
- ACME v2 compliants Certification Authorities
  - [Let's Encrypt](https://letsencrypt.org/)
  - [Buypass Go SSL](https://www.buypass.com/ssl/resources/acme-free-ssl)
  - [ZeroSSL](https://zerossl.com/features/acme/) (Requires EAB Credentials)
- Azure Container Apps

## Deployment

| Azure (Public) | Azure China | Azure Government |
| :---: | :---: | :---: |
| <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="https://aka.ms/deploytoazurebutton" /></a> | <a href="https://portal.azure.cn/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="https://aka.ms/deploytoazurebutton" /></a> | <a href="https://portal.azure.us/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshibayan%2Fkeyvault-acmebot%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="https://aka.ms/deploytoazurebutton" /></a> |

Learn more at <https://github.com/shibayan/keyvault-acmebot/wiki/Getting-Started>

## Thanks

- [KeyVault and App Service ACMEbot](https://github.com/shibayan/keyvault-acmebot) by @shibayan
- [ACMESharp Core](https://github.com/PKISharp/ACMESharpCore) by @ebekker
- [Durable Functions](https://github.com/Azure/azure-functions-durable-extension) by @cgillum and contributors
- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) by @MichaCo

## License

This project is licensed under the [Apache License 2.0](https://github.com/shibayan/keyvault-acmebot/blob/master/LICENSE)
