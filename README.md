# Ftl.DigitalMarketing.LandingWorkflow

Marketing Workflow automation built on top of azure serverless offering(functions and durable entities).

## Business case:
Resemble a telco fast shopping cycle that starts from a landing page and the purpose of the workflow is to do pre-sales before an agent can validate the purchase.

![sellDiagram-Workflow drawio](https://user-images.githubusercontent.com/68027721/188891993-f5d9b61f-157c-4b5c-8a64-654d6eb01dd0.png)

## Architecture:
![sellDiagram-Architecture drawio](https://user-images.githubusercontent.com/68027721/188923324-b9e7b813-0524-42ec-92bd-025a2213dd7b.png)



## Workflow Demo:
https://fictitelmarketing.josuefuentesdev.com
## Backoffice Demo:
https://fictitel.josuefuentesdev.com

## Future possibilities:
We use email as the only channel for communication because of its simplicity, but any non-active channel could be used, like WSP-HSM or SMS
Allocate resources to the serverless architecture(always on) in favor of lowering latency on cold starts.


## Related repositories:
- Backoffice WebApi: https://github.com/josuefuentesdev/Ftl.Backoffice.WebApi
- Marketing Landing page: https://github.com/josuefuentesdev/Ftl.DigitalMarketing.5gCampaignLading
