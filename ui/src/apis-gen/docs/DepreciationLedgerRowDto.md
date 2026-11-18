
# DepreciationLedgerRowDto


## Properties

Name | Type
------------ | -------------
`assetId` | string
`assetCode` | string
`categoryName` | string
`attrText` | string
`assetName` | string
`model` | string
`dept` | string
`originValue` | number
`periodDepreciation` | number
`yearAccumulated` | number
`totalAccumulated` | number
`impairment` | number
`netValue` | number

## Example

```typescript
import type { DepreciationLedgerRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "assetId": null,
  "assetCode": null,
  "categoryName": null,
  "attrText": null,
  "assetName": null,
  "model": null,
  "dept": null,
  "originValue": null,
  "periodDepreciation": null,
  "yearAccumulated": null,
  "totalAccumulated": null,
  "impairment": null,
  "netValue": null,
} satisfies DepreciationLedgerRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as DepreciationLedgerRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


