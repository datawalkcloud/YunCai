
# FixedAssetChangeDto


## Properties

Name | Type
------------ | -------------
`id` | string
`assetId` | string
`assetCode` | string
`assetName` | string
`kindText` | string
`before` | string
`after` | string
`period` | string
`amount` | number

## Example

```typescript
import type { FixedAssetChangeDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "assetId": null,
  "assetCode": null,
  "assetName": null,
  "kindText": null,
  "before": null,
  "after": null,
  "period": null,
  "amount": null,
} satisfies FixedAssetChangeDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FixedAssetChangeDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


